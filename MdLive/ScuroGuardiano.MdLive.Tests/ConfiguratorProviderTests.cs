using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Markdig;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

public class ConfiguratorProviderTests
{
    private static async Task<string?> Title(ActiveConfigurator active) =>
        await active.Configurator.ProvideTitle(Markdig.Markdown.Parse("# Default title", active.Pipeline));

    [Fact]
    public async Task NoScript_UsesDefaultWithoutNotice()
    {
        using var project = new TestProject();
        using var provider = project.CreateProvider();

        var active = await provider.GetAsync();

        Assert.Equal(typeof(MdLiveConfigurator), active.Configurator.GetType());
        Assert.Null(active.Notice);
    }

    [Fact]
    public async Task UntrustedScript_IsNeverExecuted()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("evil", project.Marker()));
        using var provider = project.CreateProvider();

        var active = await provider.GetAsync();

        Assert.False(File.Exists(project.Marker()), "untrusted script ran");
        Assert.Equal(typeof(MdLiveConfigurator), active.Configurator.GetType());
        Assert.Contains("is not trusted", active.Notice);
        Assert.Equal("Default title", await Title(active));
    }

    [Fact]
    public async Task TrustedScript_IsExecuted()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("from script", project.Marker()));
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        var active = await provider.GetAsync();

        Assert.Null(active.Notice);
        Assert.True(File.Exists(project.Marker()));
        Assert.Equal("from script", await Title(active));
    }

    [Fact]
    public async Task ChangeInLoadedFileAfterTrust_StopsExecution()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"helper.csx\"\n" + TestProject.TitleScript("trusted", project.Marker()));
        project.Write("helper.csx", "// innocent");
        project.TrustCurrent();
        using var provider = project.CreateProvider();
        Assert.Equal("trusted", await Title(await provider.GetAsync()));

        // Podmiana tylko pliku z #load, config.csx bez zmian
        project.Write("helper.csx", $"File.WriteAllText(@\"{project.Marker("evil")}\", \"\");");
        var active = await provider.GetAsync();

        Assert.False(File.Exists(project.Marker("evil")), "changed helper ran");
        Assert.Contains("has changed since it was trusted", active.Notice);
        Assert.Equal("Default title", await Title(active));
    }

    [Fact]
    public async Task ChangeInReferencedAssemblyAfterTrust_StopsExecution()
    {
        using var project = new TestProject();
        var dll = Path.Combine(project.ConfigDirectory, "Greeter.dll");
        File.WriteAllBytes(dll, TestProject.BuildLibrary("Greeter", "public static class Greeter { public static string Hi => \"hi\"; }"));
        project.Write("config.csx", "#r \"Greeter.dll\"\n" + TestProject.TitleScript("trusted", project.Marker()));
        project.TrustCurrent();
        using var provider = project.CreateProvider();
        Assert.Null((await provider.GetAsync()).Notice);

        File.WriteAllBytes(dll, TestProject.BuildLibrary("Greeter", "public static class Greeter { public static string Hi => \"evil\"; }"));

        Assert.Contains("has changed since it was trusted", (await provider.GetAsync()).Notice);
    }

    [Fact]
    public void Compiler_UsesBundleBytes_NotFilesOnDisk()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"helper.csx\"\n" + TestProject.TitleScript("x", project.Marker()) +
                                    "\npublic static class Probe { public static string Value => Helper.Value; }");
        var helper = project.Write("helper.csx", "public static class Helper { public static string Value => \"reviewed\"; }");
        var bundle = project.ReadRequired();

        // TOCTOU: plik zmienia się po zhashowaniu, a przed kompilacją
        File.WriteAllText(helper, "public static class Helper { public static string Value => \"swapped\"; }");
        File.Delete(Path.Combine(project.ConfigDirectory, "config.csx"));

        var compiled = new ScriptCompiler().Compile(bundle);
        var context = new ScriptLoadContext("toctou", compiled.ReferencedAssemblies);
        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(compiled.Assembly));
            var value = assembly.GetTypes().Single(t => t.Name == "Probe").GetProperty("Value")!.GetValue(null);
            Assert.Equal("reviewed", value);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public async Task ReferencedAssembly_IsLoadedFromBundleIntoCollectibleContext()
    {
        using var project = new TestProject();
        Directory.CreateDirectory(Path.Combine(project.ConfigDirectory, "lib"));
        File.WriteAllBytes(Path.Combine(project.ConfigDirectory, "lib", "Greeter.dll"),
            TestProject.BuildLibrary("Greeter", "public static class Greeter { public static string Hi(string n) => \"hi \" + n; }"));
        project.Write("config.csx",
            """
            #r "lib/Greeter.dll"

            public class GreeterConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
            {
                public override Task<string?> ProvideTitle(MarkdownDocument document) => Task.FromResult<string?>(Greeter.Hi("there"));
                public System.Reflection.Assembly GreeterAssembly => typeof(Greeter).Assembly;
            }
            """);
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        var active = await provider.GetAsync();

        Assert.Null(active.Notice);
        Assert.Equal("hi there", await Title(active));
        var greeter = (System.Reflection.Assembly)active.Configurator.GetType().GetProperty("GreeterAssembly")!.GetValue(active.Configurator)!;
        Assert.True(AssemblyLoadContext.GetLoadContext(greeter)!.IsCollectible);
        Assert.Empty(greeter.Location); // z bajtów, nie z pliku
    }

    [Fact]
    public async Task ReferencedAssemblyShadowingMdLiveDependency_IsRejected()
    {
        using var project = new TestProject();
        File.WriteAllBytes(Path.Combine(project.ConfigDirectory, "Markdig.dll"),
            TestProject.BuildLibrary("Markdig", "namespace Markdig { public static class Fake {} }"));
        project.Write("config.csx", "#r \"Markdig.dll\"\n");
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        Assert.Contains("already provided by MdLive", (await provider.GetAsync()).Notice);
    }

    [Fact]
    public async Task NotAnAssembly_IsRejected()
    {
        using var project = new TestProject();
        File.WriteAllBytes(Path.Combine(project.ConfigDirectory, "fake.dll"), "MZ not really"u8.ToArray());
        project.Write("config.csx", "#r \"fake.dll\"\n");
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        Assert.Contains("not a .NET assembly", (await provider.GetAsync()).Notice);
    }

    [Fact]
    public async Task CompilationError_IsShownWithLocation()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"helper.csx\"\n");
        project.Write("helper.csx", "\nint x = \"string\";");
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        var active = await provider.GetAsync();

        Assert.Equal(typeof(MdLiveConfigurator), active.Configurator.GetType());
        Assert.Contains("helper.csx(2,9): error CS0029", active.Notice);
    }

    [Fact]
    public async Task RuntimeError_IsShownWithScriptLine()
    {
        using var project = new TestProject();
        project.Write("config.csx", "// 1\n// 2\nthrow new InvalidOperationException(\"boom\");\n");
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        var notice = (await provider.GetAsync()).Notice;

        Assert.Contains("boom", notice);
        Assert.Contains("config.csx:line 3", notice);
    }

    [Fact]
    public async Task HangingScript_TimesOut()
    {
        using var project = new TestProject();
        project.Write("config.csx", "await Task.Delay(Timeout.Infinite);\n");
        project.TrustCurrent();
        using var provider = project.CreateProvider(TimeSpan.FromMilliseconds(500));

        Assert.Contains("didn't finish", (await provider.GetAsync()).Notice);
    }

    [Theory]
    [InlineData("return typeof(B);", "B")]
    [InlineData("return new A(Microsoft.Extensions.Logging.Abstractions.NullLogger<MdLiveConfigurator>.Instance);", "A")]
    public async Task ScriptCanChooseConfigurator(string ending, string expected)
    {
        using var project = new TestProject();
        project.Write("config.csx",
            $$"""
            public class A(ILogger<MdLiveConfigurator> l) : MdLiveConfigurator(l)
            {
                public override Task<string?> ProvideTitle(MarkdownDocument d) => Task.FromResult<string?>("A");
            }
            public class B(ILogger<MdLiveConfigurator> l) : MdLiveConfigurator(l)
            {
                public override Task<string?> ProvideTitle(MarkdownDocument d) => Task.FromResult<string?>("B");
            }
            {{ending}}
            """);
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        Assert.Equal(expected, await Title(await provider.GetAsync()));
    }

    [Theory]
    [InlineData("public class A(ILogger<MdLiveConfigurator> l) : MdLiveConfigurator(l) {}\npublic class B(ILogger<MdLiveConfigurator> l) : MdLiveConfigurator(l) {}", "declares 2 configurator classes")]
    [InlineData("var x = 1;", "doesn't declare a class")]
    [InlineData("return 42;", "returned System.Int32")]
    public async Task AmbiguousOrWrongResult_IsReported(string script, string expected)
    {
        using var project = new TestProject();
        project.Write("config.csx", script);
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        Assert.Contains(expected, (await provider.GetAsync()).Notice);
    }

    [Fact]
    public async Task ReplacedScript_IsUnloaded()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("v1", project.Marker()));
        project.TrustCurrent();
        using var provider = project.CreateProvider();
        Assert.Equal("v1", await Title(await provider.GetAsync()));

        project.Write("config.csx", TestProject.TitleScript("v2", project.Marker()));
        project.TrustCurrent();
        Assert.Equal("v2", await Title(await provider.GetAsync()));

        Assert.True(await WaitForCollection(provider.LastUnloadedContext!), "old script load context wasn't collected");
    }

    [Fact]
    public async Task UnchangedScript_IsNotRecompiled()
    {
        using var project = new TestProject();
        project.Write("config.csx", TestProject.TitleScript("v1", project.Marker()));
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        var first = await provider.GetAsync();
        var second = await provider.GetAsync();

        Assert.Same(first, second);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<bool> WaitForCollection(WeakReference reference)
    {
        for (var i = 0; i < 20 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50);
        }

        return !reference.IsAlive;
    }
}
