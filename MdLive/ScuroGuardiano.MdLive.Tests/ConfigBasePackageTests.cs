using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

/// <summary><c>#r "nuget: ScuroGuardiano.MdLive.ConfigBase"</c> maps to the bundled ConfigBase, nothing else is allowed.</summary>
public class ConfigBasePackageTests
{
    // Tak jak napisze to użytkownik, który chce IntelliSense: jawne usingi zamiast domyślnych MdLive
    private static string IdeFriendlyScript(string reference) =>
        $$"""
        #r "{{reference}}"

        using Markdig;
        using Markdig.Syntax;
        using Microsoft.Extensions.Logging;
        using ScuroGuardiano.MdLive.ConfigBase;

        public class IdeConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
        {
            public override Task<string?> ProvideTitle(MarkdownDocument document) => Task.FromResult<string?>("ide");
        }
        """;

    [Theory]
    [InlineData("nuget: ScuroGuardiano.MdLive.ConfigBase, {version}")]
    [InlineData("nuget:ScuroGuardiano.MdLive.ConfigBase,{version}")]
    [InlineData("nuget: ScuroGuardiano.MdLive.ConfigBase")]
    [InlineData("nuget: scuroguardiano.mdlive.configbase, {version}")]
    public async Task ConfigBasePackage_MapsToBundledAssembly(string reference)
    {
        using var project = new TestProject();
        project.Write("config.csx", IdeFriendlyScript(reference.Replace("{version}", ScriptBundle.ConfigBaseVersion)));
        var bundle = project.ReadRequired();

        Assert.Empty(bundle.Errors);
        Assert.Empty(bundle.Warnings);
        Assert.Equal([ScriptBundle.ConfigBasePackageId], bundle.FrameworkReferences);

        project.TrustCurrent();
        using var provider = project.CreateProvider();
        var active = await provider.GetAsync();

        Assert.Null(active.Notice);
        Assert.Equal("ide", await active.Configurator.ProvideTitle(Markdig.Markdown.Parse("# x")));
    }

    [Fact]
    public async Task DifferentVersion_IsAWarningNotAnError()
    {
        using var project = new TestProject();
        project.Write("config.csx", IdeFriendlyScript("nuget: ScuroGuardiano.MdLive.ConfigBase, 99.0.0"));
        var bundle = project.ReadRequired();

        Assert.Empty(bundle.Errors);
        var warning = Assert.Single(bundle.Warnings);
        Assert.Contains("99.0.0", warning);
        Assert.Contains($"MdLive runs with {ScriptBundle.ConfigBaseVersion}", warning);
        Assert.Contains("config.csx(1)", warning);

        project.TrustCurrent();
        using var provider = project.CreateProvider();
        Assert.Null((await provider.GetAsync()).Notice);
    }

    [Theory]
    [InlineData("nuget: Newtonsoft.Json, 13.0.3")]
    [InlineData("nuget: ScuroGuardiano.MdLive.ConfigBase.Evil, 1.0.0")]
    [InlineData("nuget: Markdig, 1.6.0")]
    public void OtherPackages_AreRejected(string reference)
    {
        using var project = new TestProject();
        project.Write("config.csx", $"#r \"{reference}\"\n");

        var error = Assert.Single(project.ReadRequired().Errors);

        Assert.Contains("NuGet references are not supported, except ScuroGuardiano.MdLive.ConfigBase", error);
    }

    [Fact]
    public void ConfigBaseVersion_IsAPlainVersion()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", ScriptBundle.ConfigBaseVersion);
    }
}
