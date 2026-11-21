using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

public class ScriptBundleTests
{
    [Fact]
    public void NoConfigScript_ReturnsNull()
    {
        using var project = new TestProject();
        project.Write("style.css", "body {}");

        Assert.Null(project.Read());
    }

    [Fact]
    public void CollectsLoadChainAndReferences_RelativeToContainingFile()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"lib/a.csx\"\n#r \"System.Net.Http\"\n");
        project.Write("lib/a.csx", "#load \"../b.csx\"\n#r \"bin/x.dll\"\n");
        project.Write("b.csx", "// b");
        File.WriteAllBytes(Path.Combine(project.ConfigDirectory, "lib", "bin", "x.dll").Also(p => Directory.CreateDirectory(Path.GetDirectoryName(p)!)), [1, 2, 3]);

        var bundle = project.ReadRequired();

        Assert.Empty(bundle.Errors);
        Assert.Equal(["config.csx", "lib/a.csx", "lib/bin/x.dll", "b.csx"], bundle.Files.Select(f => f.RelativePath));
        Assert.Equal(ScriptFileKind.Reference, bundle.Files[2].Kind);
        Assert.Equal(["System.Net.Http"], bundle.FrameworkReferences);
    }

    [Fact]
    public void LoadCycle_Terminates()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"a.csx\"\n");
        project.Write("a.csx", "#load \"config.csx\"\n#load \"a.csx\"\n");

        var bundle = project.ReadRequired();

        Assert.Empty(bundle.Errors);
        Assert.Equal(2, bundle.Files.Count);
    }

    [Theory]
    [InlineData("#load \"../outside.csx\"")]
    [InlineData("#r \"../outside.dll\"")]
    [InlineData("#load \"sub/../../outside.csx\"")]
    public void FilesOutsideConfigDirectory_AreRejected(string directive)
    {
        using var project = new TestProject();
        File.WriteAllText(Path.Combine(project.Root, "outside.csx"), "System.Console.WriteLine();");
        File.WriteAllBytes(Path.Combine(project.Root, "outside.dll"), [1]);
        project.Write("config.csx", directive + "\n");

        var bundle = project.ReadRequired();

        Assert.Contains(bundle.Errors, e => e.Contains("outside of the config directory"));
        Assert.Single(bundle.Files);
    }

    [Fact]
    public void AbsolutePathOutsideConfigDirectory_IsRejected()
    {
        using var project = new TestProject();
        var outside = Path.Combine(project.Root, "abs.csx");
        File.WriteAllText(outside, "");
        project.Write("config.csx", $"#load \"{outside}\"\n");

        Assert.Contains(project.ReadRequired().Errors, e => e.Contains("outside of the config directory"));
    }

    [Fact]
    public void SymlinkedFile_IsRejected()
    {
        using var project = new TestProject();
        var secret = Path.Combine(project.Root, "secret.csx");
        File.WriteAllText(secret, "// not reviewed");
        File.CreateSymbolicLink(Path.Combine(project.ConfigDirectory, "helper.csx"), secret);
        project.Write("config.csx", "#load \"helper.csx\"\n");

        var bundle = project.ReadRequired();

        Assert.Contains(bundle.Errors, e => e.Contains("symbolic link"));
        Assert.DoesNotContain(bundle.Files, f => f.RelativePath == "helper.csx");
    }

    [Fact]
    public void FileInSymlinkedSubdirectory_IsRejected()
    {
        using var project = new TestProject();
        var elsewhere = Directory.CreateDirectory(Path.Combine(project.Root, "elsewhere")).FullName;
        File.WriteAllText(Path.Combine(elsewhere, "helper.csx"), "");
        Directory.CreateSymbolicLink(Path.Combine(project.ConfigDirectory, "lib"), elsewhere);
        project.Write("config.csx", "#load \"lib/helper.csx\"\n");

        Assert.Contains(project.ReadRequired().Errors, e => e.Contains("'lib' is a symbolic link"));
    }

    [Fact]
    public void SymlinkedMainScript_IsRejected()
    {
        using var project = new TestProject();
        var target = Path.Combine(project.Root, "real.csx");
        File.WriteAllText(target, "");
        File.CreateSymbolicLink(Path.Combine(project.ConfigDirectory, "config.csx"), target);

        var bundle = project.ReadRequired();

        Assert.Contains(bundle.Errors, e => e.Contains("symbolic link"));
        Assert.Empty(bundle.Files);
    }

    [Fact]
    public void SymlinkedConfigDirectory_IsResolvedToTarget()
    {
        using var project = new TestProject();
        project.Write("config.csx", "");
        var link = Path.Combine(project.Root, "link-to-mdlive");
        Directory.CreateSymbolicLink(link, project.ConfigDirectory);

        var bundle = ScriptBundle.Read(link, ScriptCompiler.IsKnownAssemblyName)!;

        Assert.Empty(bundle.Errors);
        Assert.Equal(project.ConfigDirectory, bundle.ConfigDirectory);
    }

    [Theory]
    [InlineData("#r \"nuget: Newtonsoft.Json, 13.0.3\"", "NuGet references are not supported")]
    [InlineData("#r \"SomeRandomAssembly\"", "Unknown assembly")]
    [InlineData("#load \"notes.txt\"", "must have the .csx extension")]
    [InlineData("#r \"lib/native.so\"", "must have the .dll extension")]
    [InlineData("#load \"missing.csx\"", "Can't read")]
    public void InvalidDirectives_AreReportedWithLocation(string directive, string expected)
    {
        using var project = new TestProject();
        project.Write("notes.txt", "");
        project.Write("config.csx", "// first line\n" + directive + "\n");

        var error = Assert.Single(project.ReadRequired().Errors);

        Assert.Contains(expected, error);
        Assert.Contains("config.csx(2)", error);
    }

    [Fact]
    public void OversizedSource_IsRejected()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"big.csx\"\n");
        project.Write("big.csx", new string('/', 1024 * 1024 + 1));

        Assert.Contains(project.ReadRequired().Errors, e => e.Contains("larger than"));
    }

    [Fact]
    public void Hash_IsStableAndIndependentOfTimestamps()
    {
        using var project = new TestProject();
        var path = project.Write("config.csx", "#load \"a.csx\"\n");
        project.Write("a.csx", "// a");

        var first = project.ReadRequired().Hash;
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-3));

        Assert.Equal(first, project.ReadRequired().Hash);
    }

    [Fact]
    public void Hash_ChangesWithLoadedFileContent()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"a.csx\"\n");
        project.Write("a.csx", "// a");
        var before = project.ReadRequired().Hash;

        project.Write("a.csx", "// b");

        Assert.NotEqual(before, project.ReadRequired().Hash);
    }

    [Fact]
    public void Hash_ChangesWithReferencedAssemblyContent()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#r \"x.dll\"\n");
        var dll = Path.Combine(project.ConfigDirectory, "x.dll");
        File.WriteAllBytes(dll, [1]);
        var before = project.ReadRequired().Hash;

        File.WriteAllBytes(dll, [2]);

        Assert.NotEqual(before, project.ReadRequired().Hash);
    }

    [Fact]
    public void Manifest_ListsEveryInputWithPathAndHash()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"a.csx\"\n#r \"System.Net.Http\"\n");
        project.Write("a.csx", "// a");

        var bundle = project.ReadRequired();

        // Ścieżki są częścią hasha: ta sama treść pod inną nazwą to inna wersja configu
        foreach (var file in bundle.Files)
        {
            Assert.Contains($"source {file.Sha256} {file.RelativePath}\n", bundle.Manifest);
        }

        Assert.Contains("framework System.Net.Http\n", bundle.Manifest);
    }
}

internal static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
