using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

/// <summary><c>mdlive serve --trust &lt;dir&gt;</c></summary>
public class TrustOptionTests
{
    [Fact]
    public void Parse_TakesTrustOut_AndPassesTheRestToConfiguration()
    {
        using var project = new TestProject();
        var other = Directory.CreateDirectory(Path.Combine(project.Root, "other")).FullName;

        var parsed = ServeArguments.Parse(
            ["--AllowedHosts=example.test", "--trust", project.Root, $"--trust={other}", "--trust", project.Root],
            out var errors);

        Assert.Empty(errors);
        Assert.Equal([project.Root, other], parsed.TrustedDirectories);
        Assert.Equal(["--AllowedHosts=example.test"], parsed.ConfigurationArgs);
    }

    [Theory]
    [InlineData(new[] { "--trust" }, "--trust needs a directory")]
    [InlineData(new[] { "--trust=" }, "--trust needs a directory")]
    [InlineData(new[] { "--trust", "/definitely/not/here" }, "no such directory")]
    public void Parse_RejectsBadValues(string[] args, string expected)
    {
        ServeArguments.Parse(args, out var errors);

        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void Parse_ResolvesSymlinksAndDots()
    {
        using var project = new TestProject();
        var link = Path.Combine(project.Root, "link");
        Directory.CreateSymbolicLink(link, project.ConfigDirectory);

        var parsed = ServeArguments.Parse(["--trust", Path.Combine(link, "..", "link")], out _);

        Assert.Equal([project.ConfigDirectory], parsed.TrustedDirectories);
    }

    [Fact]
    public void TooBroad_FlagsRootAndHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parsed = ServeArguments.Parse(["--trust", "/", "--trust", home], out _);

        Assert.Equal(2, parsed.TooBroad().Count());
    }

    [Fact]
    public async Task ConfigInTrustedDirectory_RunsWithoutTrustStoreEntry()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("trusted by option", project.Marker()));
        using var provider = project.CreateProvider(trustedDirectories: project.Root);

        var active = await provider.GetAsync();

        Assert.Null(active.Notice);
        Assert.True(File.Exists(project.Marker()));
        Assert.False(File.Exists(project.TrustStore.FilePath), "--trust must not write to the trust store");
    }

    [Fact]
    public async Task ConfigFoundFromSubdirectory_IsCoveredByTrustedAncestor()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("x", project.Marker()));
        var notes = Directory.CreateDirectory(Path.Combine(project.Root, "notes", "2026")).FullName;
        using var provider = project.CreateProvider(workingDirectory: notes, trustedDirectories: project.Root);

        Assert.Null((await provider.GetAsync()).Notice);
    }

    [Fact]
    public async Task SiblingWithSamePrefix_IsNotCovered()
    {
        using var project = new TestProject();
        var notes = Directory.CreateDirectory(Path.Combine(project.Root, "notes")).FullName;
        var evil = Directory.CreateDirectory(Path.Combine(project.Root, "notes-evil")).FullName;
        Directory.CreateDirectory(Path.Combine(evil, ".mdlive"));
        File.WriteAllText(Path.Combine(evil, ".mdlive", "config.csx"), TestProject.TitleScript("evil", project.Marker()));
        using var provider = project.CreateProvider(workingDirectory: evil, trustedDirectories: notes);

        var active = await provider.GetAsync();

        Assert.Contains("is not trusted", active.Notice);
        Assert.False(File.Exists(project.Marker()));
    }

    [Fact]
    public async Task SymlinkIntoTrustedDirectory_FromOutside_IsNotCovered()
    {
        // Katalog spoza zaufanego, podlinkowany do środka: liczy się prawdziwa ścieżka configu, nie droga do niego
        using var project = new TestProject();
        var trusted = Directory.CreateDirectory(Path.Combine(project.Root, "trusted")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(project.Root, "outside")).FullName;
        Directory.CreateDirectory(Path.Combine(outside, ".mdlive"));
        File.WriteAllText(Path.Combine(outside, ".mdlive", "config.csx"), TestProject.TitleScript("x", project.Marker()));
        var link = Path.Combine(trusted, "linked");
        Directory.CreateSymbolicLink(link, outside);
        using var provider = project.CreateProvider(workingDirectory: link, trustedDirectories: trusted);

        Assert.Contains("is not trusted", (await provider.GetAsync()).Notice);
        Assert.False(File.Exists(project.Marker()));
    }

    [Fact]
    public async Task TrustOption_DoesNotRelaxOtherChecks()
    {
        using var project = new TestProject();
        File.WriteAllText(Path.Combine(project.Root, "outside.csx"), "");
        project.Write("config.csx", "#load \"../outside.csx\"\n");
        using var provider = project.CreateProvider(trustedDirectories: project.Root);

        Assert.Contains("outside of the config directory", (await provider.GetAsync()).Notice);
    }
}
