using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ScuroGuardiano.MdLive.Scripting;

internal enum ScriptFileKind
{
    /// <summary>Main script or a file loaded with <c>#load</c>.</summary>
    Source,

    /// <summary>Assembly referenced with <c>#r "file.dll"</c>.</summary>
    Reference
}

/// <param name="RelativePath">Path relative to the config directory, always with <c>/</c>.</param>
/// <param name="FullPath">Absolute path, used for diagnostics and stack traces.</param>
/// <param name="Content">Bytes read once from disk. Hashed, compiled and loaded from here, never re-read.</param>
internal sealed record ScriptFile(string RelativePath, string FullPath, byte[] Content, ScriptFileKind Kind)
{
    public string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(Content));
}

/// <summary>
/// <para>
/// Everything a config script consists of: <c>config.csx</c>, files pulled in by <c>#load</c> and assemblies
/// referenced by <c>#r</c>, read into memory, and the hash of all of it that the user trusts.
/// </para>
/// <para>
/// Building a bundle only parses the directives, it never compiles or executes anything, so it's safe to do
/// for an untrusted directory. After the trust check the compiler gets exactly these bytes, so a file swapped
/// on disk in the meantime can't sneak in (TOCTOU).
/// </para>
/// </summary>
internal sealed class ScriptBundle
{
    public const string MainFileName = "config.csx";

    // Rozsądne limity, żeby wrogi katalog nie kazał wczytać do pamięci czegoś ogromnego
    // albo nieskończonego (np. /dev/zero przez symlink, który i tak odrzucamy)
    private const long MaxSourceBytes = 1024 * 1024;
    private const long MaxReferenceBytes = 32 * 1024 * 1024;
    private const int MaxFiles = 64;

    internal static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Script);

    /// <summary>
    /// The only NuGet package a script may reference. It's not downloaded: the reference maps to the
    /// <c>ScuroGuardiano.MdLive.ConfigBase</c> MdLive is built with. It exists so that editors which understand
    /// <c>#r "nuget: ..."</c> (e.g. OmniSharp) can restore the package and offer IntelliSense in <c>config.csx</c>.
    /// </summary>
    public const string ConfigBasePackageId = "ScuroGuardiano.MdLive.ConfigBase";

    /// <summary>Version of the ConfigBase assembly MdLive runs with, e.g. <c>0.2.0</c>.</summary>
    public static string ConfigBaseVersion { get; } = GetConfigBaseVersion();

    private ScriptBundle(string configDirectory, IReadOnlyList<ScriptFile> files,
        IReadOnlyList<string> frameworkReferences, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        ConfigDirectory = configDirectory;
        Files = files;
        FrameworkReferences = frameworkReferences;
        Errors = errors;
        Warnings = warnings;
        Manifest = BuildManifest();
        Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Manifest)));
    }

    /// <summary>Absolute path of the config directory with symlinks of the directory itself resolved.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Main script first, then the rest in the order they were found.</summary>
    public IReadOnlyList<ScriptFile> Files { get; }

    public ScriptFile Main => Files[0];

    /// <summary>
    /// Bare-name <c>#r</c> directives (e.g. <c>#r "System.Net.Http"</c>). They point at assemblies MdLive
    /// already ships, so only the name goes into the hash.
    /// </summary>
    public IReadOnlyList<string> FrameworkReferences { get; }

    /// <summary>Problems with the directives. A bundle with errors is never compiled.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Things worth telling the user that don't stop the script, e.g. a ConfigBase version mismatch.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Text listing every input with its SHA-256, the thing the user actually reviews and trusts.</summary>
    public string Manifest { get; }

    /// <summary>SHA-256 of <see cref="Manifest"/>, stored in the trust store.</summary>
    public string Hash { get; }

    public ScriptFile? FindFile(string fullPath, ScriptFileKind kind) =>
        Files.FirstOrDefault(f => f.Kind == kind && PathsEqual(f.FullPath, fullPath));

    /// <summary>
    /// Reads <c>config.csx</c> from <paramref name="configDirectory"/> with everything it references.
    /// Returns null when there is no <c>config.csx</c>.
    /// </summary>
    /// <param name="isKnownAssemblyName">Whether a bare-name <c>#r</c> points at an assembly MdLive provides.</param>
    public static ScriptBundle? Read(string configDirectory, Func<string, bool> isKnownAssemblyName)
    {
        var directory = ResolveDirectory(configDirectory);
        if (directory is null)
        {
            return null;
        }

        var mainPath = Path.Join(directory, MainFileName);
        if (!File.Exists(mainPath) && !IsSymlink(mainPath))
        {
            return null;
        }

        var reader = new Reader(directory, isKnownAssemblyName);
        reader.Run(mainPath);

        return new ScriptBundle(directory, reader.Files, reader.FrameworkReferences, reader.Errors, reader.Warnings);
    }

    private string BuildManifest()
    {
        var sb = new StringBuilder();
        sb.Append("mdlive-config v1\n");

        // Kolejność wpisów jest stała, ale ścieżki są częścią hasha: zmiana nazwy pliku to nowa wersja do zaufania
        foreach (var file in Files.OrderBy(f => f.Kind).ThenBy(f => f.RelativePath, StringComparer.Ordinal))
        {
            var kind = file.Kind == ScriptFileKind.Source ? "source" : "reference";
            sb.Append(kind).Append(' ').Append(file.Sha256).Append(' ').Append(file.RelativePath).Append('\n');
        }

        foreach (var name in FrameworkReferences.Order(StringComparer.Ordinal))
        {
            sb.Append("framework ").Append(name).Append('\n');
        }

        return sb.ToString();
    }

    private static string? ResolveDirectory(string configDirectory)
    {
        var full = Path.GetFullPath(configDirectory);
        var info = new DirectoryInfo(full);

        // Sam katalog .mdlive może być symlinkiem (np. do wspólnej konfiguracji), wtedy kluczem zaufania
        // jest katalog docelowy. W środku symlinków już nie akceptujemy.
        if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is DirectoryInfo target)
        {
            info = target;
        }

        return info.Exists ? Path.TrimEndingDirectorySeparator(info.FullName) : null;
    }

    private static bool IsSymlink(string path) => new FileInfo(path).LinkTarget is not null;

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Resolves a <c>#load</c> / <c>#r</c> path the same way during reading and compilation.
    /// Relative paths are relative to the file containing the directive.
    /// </summary>
    internal static string ResolvePath(string value, string? baseFilePath, string configDirectory)
    {
        var baseDirectory = baseFilePath is null ? configDirectory : Path.GetDirectoryName(baseFilePath)!;
        return Path.GetFullPath(value, baseDirectory);
    }

    /// <summary>
    /// Parses dotnet-script style <c>nuget: Package.Id, 1.2.3</c> (the version is optional).
    /// </summary>
    internal static bool TryParseNuGetReference(string value, out string packageId, out string? version)
    {
        packageId = "";
        version = null;

        if (!value.StartsWith("nuget:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = value["nuget:".Length..].Split(',', StringSplitOptions.TrimEntries);
        packageId = parts[0];
        version = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
        return true;
    }

    internal static bool IsConfigBasePackage(string packageId) =>
        string.Equals(packageId, ConfigBasePackageId, StringComparison.OrdinalIgnoreCase);

    private static string GetConfigBaseVersion()
    {
        var assembly = typeof(ScuroGuardiano.MdLive.ConfigBase.MdLiveConfigurator).Assembly;
        var informational = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        // "0.2.0+<commit>" -> "0.2.0"
        return informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private sealed class Reader(string directory, Func<string, bool> isKnownAssemblyName)
    {
        public List<ScriptFile> Files { get; } = [];
        public List<string> FrameworkReferences { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        private readonly HashSet<string> _visited =
            new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        public void Run(string mainPath)
        {
            var queue = new Queue<(string Path, DirectiveTriviaSyntax? Directive)>();
            queue.Enqueue((mainPath, null));
            _visited.Add(mainPath);

            while (queue.TryDequeue(out var item))
            {
                var path = item.Path;
                var file = ReadFile(path, ScriptFileKind.Source, item.Directive);
                if (file is null)
                {
                    continue;
                }

                // Tylko parsowanie, bez kompilacji: wyciągamy dyrektywy #load i #r
                var text = SourceText.From(file.Content, file.Content.Length, Encoding.UTF8);
                var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, ParseOptions, path).GetRoot();

                foreach (var directive in root.GetLoadDirectives())
                {
                    var target = ResolvePath(directive.File.ValueText, path, directory);
                    if (_visited.Add(target))
                    {
                        if (CheckExtension(target, ".csx", directive))
                        {
                            queue.Enqueue((target, directive));
                        }
                    }
                }

                foreach (var directive in root.GetReferenceDirectives())
                {
                    AddReference(directive.File.ValueText, path, directive);
                }
            }
        }

        private void AddReference(string value, string baseFile, DirectiveTriviaSyntax directive)
        {
            if (TryParseNuGetReference(value, out var packageId, out var version))
            {
                AddNuGetReference(packageId, version, directive);
                return;
            }

            var isPath = value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                         || value.Contains('/') || value.Contains('\\');

            if (!isPath)
            {
                if (isKnownAssemblyName(value))
                {
                    if (!FrameworkReferences.Contains(value))
                    {
                        FrameworkReferences.Add(value);
                    }
                }
                else
                {
                    Error(directive, $"Unknown assembly '{value}'. Only assemblies shipped with MdLive can be referenced " +
                                     "by name, reference other ones by path to a .dll in the config directory");
                }

                return;
            }

            var target = ResolvePath(value, baseFile, directory);
            if (_visited.Add(target) && CheckExtension(target, ".dll", directive))
            {
                ReadFile(target, ScriptFileKind.Reference, directive);
            }
        }

        private void AddNuGetReference(string packageId, string? version, DirectiveTriviaSyntax directive)
        {
            if (!IsConfigBasePackage(packageId))
            {
                Error(directive, $"NuGet references are not supported, except {ConfigBasePackageId}. " +
                                 "Put the assembly in the config directory and reference it with #r \"name.dll\"");
                return;
            }

            // Nic nie pobieramy: to ten sam ConfigBase, z którym działa serwer
            if (!FrameworkReferences.Contains(ConfigBasePackageId))
            {
                FrameworkReferences.Add(ConfigBasePackageId);
            }

            if (version is not null && version != ConfigBaseVersion)
            {
                var span = directive.GetLocation().GetLineSpan();
                Warnings.Add($"{span.Path}({span.StartLinePosition.Line + 1}): {ConfigBasePackageId} {version} is referenced, " +
                             $"but MdLive runs with {ConfigBaseVersion}. The script is compiled against {ConfigBaseVersion}, " +
                             "your editor may show a different API");
            }
        }

        private bool CheckExtension(string path, string extension, DirectiveTriviaSyntax directive)
        {
            if (Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Error(directive, $"'{Path.GetFileName(path)}' must have the {extension} extension");
            return false;
        }

        private ScriptFile? ReadFile(string path, ScriptFileKind kind, DirectiveTriviaSyntax? location)
        {
            if (Files.Count >= MaxFiles)
            {
                Error(location, $"Too many files, at most {MaxFiles} are allowed");
                return null;
            }

            var relative = Path.GetRelativePath(directory, path);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relative))
            {
                // Wszystko, co wchodzi w skład configu, ma leżeć w .mdlive: to jest zakres, który użytkownik
                // przegląda przed zaufaniem. Hash i tak by wykrył zmianę, ale plik spoza katalogu łatwo przeoczyć.
                Error(location, $"'{path}' is outside of the config directory {directory}");
                return null;
            }

            var symlink = FindSymlink(path);
            if (symlink is not null)
            {
                Error(location, $"'{symlink}' is a symbolic link, links aren't allowed in the config directory");
                return null;
            }

            var limit = kind == ScriptFileKind.Source ? MaxSourceBytes : MaxReferenceBytes;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > limit)
                {
                    Error(location, $"'{relative}' is larger than {limit / 1024 / 1024} MiB");
                    return null;
                }

                var content = new byte[stream.Length];
                stream.ReadExactly(content);

                var file = new ScriptFile(relative.Replace('\\', '/'), path, content, kind);
                Files.Add(file);
                return file;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error(location, $"Can't read '{relative}': {ex.Message}");
                return null;
            }
        }

        /// <summary>Returns the first component between the config directory and the file that is a link.</summary>
        private string? FindSymlink(string path)
        {
            for (var current = path;
                 current.Length > directory.Length && !PathsEqual(current, directory);
                 current = Path.GetDirectoryName(current)!)
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (info.LinkTarget is not null)
                {
                    return Path.GetRelativePath(directory, current);
                }
            }

            return null;
        }

        private void Error(DirectiveTriviaSyntax? directive, string message)
        {
            if (directive is null)
            {
                Errors.Add(message);
                return;
            }

            var span = directive.GetLocation().GetLineSpan();
            Errors.Add($"{span.Path}({span.StartLinePosition.Line + 1}): {message}");
        }
    }
}
