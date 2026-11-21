using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using ScuroGuardiano.MdLive.ConfigBase;

namespace ScuroGuardiano.MdLive.Scripting;

internal sealed record CompiledScript(
    byte[] Assembly,
    byte[] Pdb,
    string EntryPointType,
    string EntryPointMethod,
    IReadOnlyDictionary<string, byte[]> ReferencedAssemblies,
    IReadOnlyList<string> Warnings);

internal sealed class ScriptCompilationException(IReadOnlyList<string> errors)
    : Exception("Config script compilation failed:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// <para>
/// Compiles a <see cref="ScriptBundle"/> with Roslyn as a C# script. Doesn't use
/// <c>Microsoft.CodeAnalysis.CSharp.Scripting</c>: its <c>CSharpScript</c> loads every compiled submission into
/// an assembly loader that can't be unloaded (dotnet/roslyn#41722) and loads <c>#r</c> assemblies from disk
/// after compilation, so a file could be swapped between the trust check and loading. Here the result is just
/// bytes, loaded by <see cref="ScriptLoadContext"/>.
/// </para>
/// <para>
/// Compilation runs no user code: no analyzers or source generators are passed to the compiler.
/// It's still done only for trusted bundles.
/// </para>
/// </summary>
internal sealed class ScriptCompiler
{
    /// <summary>Namespaces imported in every config script.</summary>
    public static readonly ImmutableArray<string> DefaultUsings =
    [
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Text",
        "System.Threading",
        "System.Threading.Tasks",
        "Markdig",
        "Markdig.Syntax",
        "Microsoft.Extensions.Logging",
        "ScuroGuardiano.MdLive.ConfigBase"
    ];

    private static readonly Lazy<BaseReferences> SharedBaseReferences = new(BaseReferences.Create);

    private int _compilationCounter;

    /// <summary>Whether a bare-name <c>#r</c> refers to an assembly the script is compiled against anyway.</summary>
    public static bool IsKnownAssemblyName(string name) => SharedBaseReferences.Value.ByName.ContainsKey(name);

    public CompiledScript Compile(ScriptBundle bundle)
    {
        if (bundle.Errors.Count > 0)
        {
            throw new ScriptCompilationException(bundle.Errors);
        }

        var baseReferences = SharedBaseReferences.Value;
        var referencedAssemblies = ReadReferencedAssemblies(bundle, baseReferences);

        var main = bundle.Main;
        var tree = CSharpSyntaxTree.ParseText(CreateText(main.Content), ScriptBundle.ParseOptions, main.FullPath);

        var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                usings: DefaultUsings,
                nullableContextOptions: NullableContextOptions.Enable,
                optimizationLevel: OptimizationLevel.Debug)
            .WithSourceReferenceResolver(new BundleSourceResolver(bundle))
            .WithMetadataReferenceResolver(new BundleMetadataResolver(bundle, baseReferences));

        // Unikalna nazwa: kolejne wersje configu siedzą w osobnych ALC, ale nazwa trafia do stack trace'ów
        var name = $"mdlive-config-{bundle.Hash[..12]}-{Interlocked.Increment(ref _compilationCounter)}";

        var compilation = CSharpCompilation.CreateScriptCompilation(name, tree, baseReferences.All, options,
            returnType: typeof(object));

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var result = compilation.Emit(peStream, pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(Format).ToList();
        if (!result.Success)
        {
            // Roslyn nie emituje skryptu bez żadnego kodu (same komentarze, same #load pustych plików) i nie podaje
            // przy tym żadnej diagnostyki
            throw new ScriptCompilationException(errors.Count > 0 ? errors : [$"{main.FullPath}: the script contains no code"]);
        }

        var entryPoint = compilation.GetEntryPoint(CancellationToken.None)
                         ?? throw new ScriptCompilationException(["Script has no entry point"]);

        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(Format).ToList();

        return new CompiledScript(peStream.ToArray(), pdbStream.ToArray(),
            entryPoint.ContainingType.MetadataName, entryPoint.MetadataName, referencedAssemblies, warnings);
    }

    private static string Format(Diagnostic diagnostic) =>
        diagnostic.ToString();

    // Bez Encoding Roslyn nie zapisze PDB (CS8055), a bez canBeEmbedded nie ma źródła w debuggerze
    internal static SourceText CreateText(byte[] content) =>
        SourceText.From(content, content.Length, Encoding.UTF8, canBeEmbedded: true);

    /// <summary>
    /// Reads assembly names of <c>#r</c> assemblies. Only done for a trusted bundle: until then the bytes
    /// aren't even parsed as PE files.
    /// </summary>
    private static Dictionary<string, byte[]> ReadReferencedAssemblies(ScriptBundle bundle, BaseReferences baseReferences)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var file in bundle.Files.Where(f => f.Kind == ScriptFileKind.Reference))
        {
            string? name;
            try
            {
                using var pe = new PEReader(ImmutableArray.Create(file.Content));
                name = pe.HasMetadata ? pe.GetMetadataReader().GetString(pe.GetMetadataReader().GetAssemblyDefinition().Name) : null;
            }
            catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
            {
                name = null;
            }

            if (name is null)
            {
                errors.Add($"{file.RelativePath}: not a .NET assembly");
            }
            else if (baseReferences.ByName.ContainsKey(name) || name.Equals(typeof(ScriptCompiler).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
            {
                // Drugi Markdig obok tego z MdLive dałby dwa różne typy MarkdownDocument i dziwne błędy rzutowania
                errors.Add($"{file.RelativePath}: assembly '{name}' is already provided by MdLive, remove it");
            }
            else if (!result.TryAdd(name, file.Content))
            {
                errors.Add($"{file.RelativePath}: assembly '{name}' is referenced twice");
            }
        }

        if (errors.Count > 0)
        {
            throw new ScriptCompilationException(errors);
        }

        return result;
    }

    /// <summary>
    /// Assemblies every script is compiled against: the shared framework (<c>Microsoft.NETCore.App</c>) and
    /// <c>ScuroGuardiano.MdLive.ConfigBase</c> with its dependencies (Markdig, ColorCode, Humanizer...).
    /// At runtime they all come from the default load context, so <c>MdLiveConfigurator</c> in the script
    /// is the same type the server uses.
    /// </summary>
    private sealed class BaseReferences
    {
        public required ImmutableArray<MetadataReference> All { get; init; }
        public required IReadOnlyDictionary<string, MetadataReference> ByName { get; init; }

        public static BaseReferences Create()
        {
            var byName = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

            var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            foreach (var path in Directory.EnumerateFiles(frameworkDirectory, "*.dll"))
            {
                if (IsManagedAssembly(path))
                {
                    byName.TryAdd(Path.GetFileNameWithoutExtension(path), MetadataReference.CreateFromFile(path));
                }
            }

            // Domknięcie zależności ConfigBase, ładowane przez domyślny ALC i tak, bo serwer z nich korzysta
            var pending = new Stack<Assembly>([typeof(MdLiveConfigurator).Assembly]);
            while (pending.TryPop(out var assembly))
            {
                var name = assembly.GetName().Name!;
                if (byName.ContainsKey(name) || string.IsNullOrEmpty(assembly.Location))
                {
                    continue;
                }

                byName[name] = MetadataReference.CreateFromFile(assembly.Location);

                foreach (var dependency in assembly.GetReferencedAssemblies())
                {
                    if (dependency.Name is not null && !byName.ContainsKey(dependency.Name))
                    {
                        pending.Push(Assembly.Load(dependency));
                    }
                }
            }

            return new BaseReferences { All = [.. byName.Values], ByName = byName };
        }

        private static bool IsManagedAssembly(string path)
        {
            try
            {
                using var reader = new PEReader(File.OpenRead(path));
                return reader.HasMetadata && reader.GetMetadataReader().IsAssembly;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }

    /// <summary>Serves <c>#load</c> files from the bundle only. Anything else doesn't exist.</summary>
    private sealed class BundleSourceResolver(ScriptBundle bundle) : SourceReferenceResolver
    {
        public override string? NormalizePath(string path, string? baseFilePath) =>
            ScriptBundle.ResolvePath(path, baseFilePath, bundle.ConfigDirectory);

        public override string? ResolveReference(string path, string? baseFilePath)
        {
            var full = ScriptBundle.ResolvePath(path, baseFilePath, bundle.ConfigDirectory);
            return bundle.FindFile(full, ScriptFileKind.Source) is not null ? full : null;
        }

        public override Stream OpenRead(string resolvedPath) =>
            new MemoryStream(GetFile(resolvedPath).Content, writable: false);

        public override SourceText ReadText(string resolvedPath) => CreateText(GetFile(resolvedPath).Content);

        private ScriptFile GetFile(string resolvedPath) =>
            bundle.FindFile(resolvedPath, ScriptFileKind.Source)
            ?? throw new FileNotFoundException("Not part of the config bundle", resolvedPath);

        public override bool Equals(object? other) =>
            other is BundleSourceResolver resolver && ReferenceEquals(resolver.Bundle, Bundle);

        public override int GetHashCode() => Bundle.GetHashCode();

        private ScriptBundle Bundle => bundle;
    }

    /// <summary>
    /// Serves <c>#r</c> from the bundle (by path) or from the base references (by name). Nothing is read from
    /// disk here, the bytes are the ones that were hashed.
    /// </summary>
    private sealed class BundleMetadataResolver(ScriptBundle bundle, BaseReferences baseReferences)
        : MetadataReferenceResolver
    {
        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference,
            string? baseFilePath, MetadataReferenceProperties properties)
        {
            if (baseReferences.ByName.TryGetValue(reference, out var known) && known is PortableExecutableReference pe)
            {
                return [pe];
            }

            // #r "nuget: ScuroGuardiano.MdLive.ConfigBase, x.y.z" to ConfigBase z serwera, nic nie jest pobierane.
            // Inne paczki odrzuca już ScriptBundle, tu po prostu ich nie ma.
            if (ScriptBundle.TryParseNuGetReference(reference, out var packageId, out _))
            {
                return ScriptBundle.IsConfigBasePackage(packageId)
                       && baseReferences.ByName.TryGetValue(ScriptBundle.ConfigBasePackageId, out var configBase)
                       && configBase is PortableExecutableReference configBaseReference
                    ? [configBaseReference]
                    : [];
            }

            var full = ScriptBundle.ResolvePath(reference, baseFilePath, bundle.ConfigDirectory);
            var file = bundle.FindFile(full, ScriptFileKind.Reference);

            return file is null
                ? []
                : [MetadataReference.CreateFromImage(file.Content, properties, filePath: file.FullPath)];
        }

        public override bool Equals(object? other) =>
            other is BundleMetadataResolver resolver && ReferenceEquals(resolver.Bundle, Bundle);

        public override int GetHashCode() => Bundle.GetHashCode();

        private ScriptBundle Bundle => bundle;
    }
}
