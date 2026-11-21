using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

/// <summary>Temporary project directory with <c>.mdlive</c> and its own trust store.</summary>
internal sealed class TestProject : IDisposable
{
    public TestProject()
    {
        Root = Path.Combine(Path.GetTempPath(), "mdlive-tests", Guid.NewGuid().ToString("N"));
        ConfigDirectory = Path.Combine(Root, ".mdlive");
        Directory.CreateDirectory(ConfigDirectory);
        TrustStore = new TrustStore(Path.Combine(Root, "data", "mdlive", "trusted.json"));
    }

    public string Root { get; }
    public string ConfigDirectory { get; }
    public TrustStore TrustStore { get; }

    /// <summary>File a script creates to prove it ran.</summary>
    public string Marker(string name = "ran") => Path.Combine(Root, name);

    public string Write(string relativePath, string content)
    {
        var path = Path.Combine(ConfigDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public ScriptBundle? Read() => ScriptBundle.Read(ConfigDirectory, ScriptCompiler.IsKnownAssemblyName);

    public ScriptBundle ReadRequired() => Read() ?? throw new InvalidOperationException("No bundle");

    public void TrustCurrent() => TrustStore.Trust(ReadRequired());

    public ConfiguratorProvider CreateProvider(TimeSpan? timeout = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<MdLiveConfigurator>()
            .BuildServiceProvider();

        return new ConfiguratorProvider(services.GetRequiredService<MdLiveConfigurator>(), services, TrustStore,
            new ScriptCompiler(), NullLogger<ConfiguratorProvider>.Instance, ConfigDirectory)
        {
            ScriptTimeout = timeout ?? TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>Script whose configurator sets the title, writing a marker when it runs.</summary>
    public static string TitleScript(string title, string markerPath) =>
        $$"""
        File.WriteAllText(@"{{markerPath}}", "ran");

        public class TitleConfig(ILogger<MdLiveConfigurator> logger) : MdLiveConfigurator(logger)
        {
            public override Task<string?> ProvideTitle(MarkdownDocument document) => Task.FromResult<string?>("{{title}}");
        }
        """;

    /// <summary>Compiles a small library to reference with <c>#r</c>.</summary>
    public static byte[] BuildLibrary(string assemblyName, string code)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal))
            .Select(p => MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create(assemblyName,
            [CSharpSyntaxTree.ParseText(code)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return stream.ToArray();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
