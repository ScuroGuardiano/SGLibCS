using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

public class ConfigDirectoryLocatorTests
{
    private static string Dir(TestProject project, string relative) =>
        Directory.CreateDirectory(Path.Combine(project.Root, relative)).FullName;

    [Fact]
    public void FindsInWorkingDirectory()
    {
        using var project = new TestProject();

        Assert.Equal(project.ConfigDirectory, ConfigDirectoryLocator.Find(project.Root));
    }

    [Fact]
    public void FindsInParentDirectories()
    {
        using var project = new TestProject();

        Assert.Equal(project.ConfigDirectory, ConfigDirectoryLocator.Find(Dir(project, "docs/guides/deep")));
    }

    [Fact]
    public void NearestOneWins()
    {
        using var project = new TestProject();
        var nested = Dir(project, "sub/.mdlive");

        Assert.Equal(nested, ConfigDirectoryLocator.Find(Dir(project, "sub/notes")));
    }

    [Fact]
    public void NothingFound_ReturnsNull_AndDefaultIsInWorkingDirectory()
    {
        using var project = new TestProject();
        Directory.Delete(project.ConfigDirectory);
        var working = Dir(project, "a/b");

        Assert.Null(ConfigDirectoryLocator.Find(working));
        Assert.Equal(Path.Combine(working, ".mdlive"), ConfigDirectoryLocator.Default(working));
    }

    [Fact]
    public void DoesNotClimbIntoWorldWritableDirectory()
    {
        if (OperatingSystem.IsWindows()) return; // uprawnienia Unixowe

        // shared/ jak /tmp: każdy może tam założyć .mdlive, które przejęłoby wszystko pod spodem
        using var project = new TestProject();
        Directory.Delete(project.ConfigDirectory);
        var shared = Dir(project, "shared");
        Dir(project, "shared/.mdlive");
        File.SetUnixFileMode(shared, (UnixFileMode)Convert.ToInt32("777", 8));
        var mine = Dir(project, "shared/me/project");

        Assert.Null(ConfigDirectoryLocator.Find(mine));

        // Bezpośrednio w takim katalogu .mdlive jest używane, tak jak wcześniej
        Assert.Equal(Path.Combine(shared, ".mdlive"), ConfigDirectoryLocator.Find(shared));
    }

    [Fact]
    public void SymlinkedWorkingDirectory_ReturnsRealPath()
    {
        using var project = new TestProject();
        var link = Path.Combine(project.Root, "..", Path.GetFileName(project.Root) + "-link");
        Directory.CreateSymbolicLink(link, project.Root);
        try
        {
            Assert.Equal(project.ConfigDirectory, ConfigDirectoryLocator.Find(Path.Combine(link, "x", "..")));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Provider_UsesParentConfigForStylesAndScript()
    {
        using var project = new TestProject();
        project.Write("style.css", "main { color: red; }");
        project.Write("config.csx", TestProject.TitleScript("from parent", project.Marker()));
        project.TrustCurrent();
        using var provider = project.CreateProvider(workingDirectory: Dir(project, "notes/2026"));

        var active = await provider.GetAsync();
        var document = Markdig.Markdown.Parse("# x");

        Assert.Null(active.Notice);
        Assert.Equal("from parent", await active.Configurator.ProvideTitle(document));
        Assert.Equal(project.ConfigDirectory, active.Configurator.ConfigDirectory);
        Assert.Contains("color: red", await active.Configurator.ProvideStyles(document));
    }

    [Fact]
    public async Task Provider_DefaultConfiguratorReadsStylesFromParent()
    {
        using var project = new TestProject();
        project.Write("style.css", "main { color: blue; }");
        using var provider = project.CreateProvider(workingDirectory: Dir(project, "notes"));

        var active = await provider.GetAsync();

        Assert.Contains("color: blue", await active.Configurator.ProvideStyles(Markdig.Markdown.Parse("x")));
    }

    [Fact]
    public async Task Provider_PicksUpConfigCreatedLater()
    {
        using var project = new TestProject();
        Directory.Delete(project.ConfigDirectory);
        var notes = Dir(project, "notes");
        using var provider = project.CreateProvider(workingDirectory: notes);
        Assert.DoesNotContain("green", await (await provider.GetAsync()).Configurator.ProvideStyles(Markdig.Markdown.Parse("x")));

        // np. mdlive init w katalogu projektu w trakcie pracy
        project.Write("style.css", "main { color: green; }");

        Assert.Contains("green", await (await provider.GetAsync()).Configurator.ProvideStyles(Markdig.Markdown.Parse("x")));
    }
}
