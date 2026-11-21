using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

/// <summary>Attempts to get code from outside the reviewed config to run.</summary>
public class AdversarialTests
{
    [Fact]
    public async Task UntrustedReferencedAssembly_IsNotEvenParsed()
    {
        using var project = new TestProject();
        File.WriteAllBytes(Path.Combine(project.ConfigDirectory, "garbage.dll"), "MZ garbage"u8.ToArray());
        project.Write("config.csx", "#r \"garbage.dll\"\n");
        using var provider = project.CreateProvider();

        var notice = (await provider.GetAsync()).Notice;

        // Gdyby PE był czytany przed sprawdzeniem zaufania, byłby tu błąd "not a .NET assembly"
        Assert.Contains("is not trusted", notice);
        Assert.DoesNotContain("not a .NET assembly", notice);
    }

    // Różne zapisy wyjścia poza .mdlive. Niektóre odrzuca już czytnik, inne skończą się błędem kompilacji,
    // bo kompilator dostaje pliki wyłącznie z bundla. W żadnym przypadku kod spoza katalogu nie może ruszyć.
    [Theory]
    [InlineData("#load \"../outside.csx\"")]
    [InlineData("#load \"./sub/../../outside.csx\"")]
    [InlineData("#load \"..\\\\outside.csx\"")]
    [InlineData("#load \"..//outside.csx\"")]
    [InlineData("#load \"\\u002E\\u002E/outside.csx\"")]
    [InlineData("#load \"..%2Foutside.csx\"")]
    [InlineData("#load @\"../outside.csx\"")]
    public async Task OutsideCodeNeverRuns_EvenAfterTrustingTheBundle(string directive)
    {
        using var project = new TestProject();
        File.WriteAllText(Path.Combine(project.Root, "outside.csx"),
            $"System.IO.File.WriteAllText(@\"{project.Marker()}\", \"\");");
        Directory.CreateDirectory(Path.Combine(project.ConfigDirectory, "sub"));
        project.Write("config.csx", directive + "\n");

        var bundle = project.ReadRequired();
        Assert.All(bundle.Files, f => Assert.StartsWith(project.ConfigDirectory + Path.DirectorySeparatorChar, f.FullPath));

        // Nawet gdyby użytkownik zaufał (np. nie zauważył błędu), kod spoza katalogu nie może się wykonać
        if (bundle.Errors.Count == 0)
        {
            project.TrustCurrent();
        }
        else
        {
            new TrustStore(project.TrustStore.FilePath).Trust(bundle);
        }

        using var provider = project.CreateProvider();
        await provider.GetAsync();

        Assert.False(File.Exists(project.Marker()), $"code outside .mdlive ran for: {directive}");
    }

    [Fact]
    public async Task SwappingFileAfterTrustCheck_DoesNotRunSwappedCode()
    {
        // Prawdziwy wyścig jest trudny do trafienia, więc symulujemy go na granicy: bundle przeczytany
        // i zaufany, plik podmieniony na dysku, kompilacja i uruchomienie z bundla
        using var project = new TestProject();
        project.Write("config.csx", "#load \"helper.csx\"\n");
        var helper = project.Write("helper.csx", "var reviewed = true;");
        var bundle = project.ReadRequired();
        project.TrustStore.Trust(bundle);

        File.WriteAllText(helper, $"System.IO.File.WriteAllText(@\"{project.Marker()}\", \"\");");

        var compiled = new ScriptCompiler().Compile(bundle);
        var context = new ScriptLoadContext("swap", compiled.ReferencedAssemblies);
        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(compiled.Assembly));
            var factory = assembly.GetType(compiled.EntryPointType)!.GetMethod(compiled.EntryPointMethod)!;
            await (Task<object?>)factory.Invoke(null, [new object?[2]])!;
        }
        finally
        {
            context.Unload();
        }

        Assert.False(File.Exists(project.Marker()));
    }

    [Fact]
    public async Task NoticeIsHtmlEncoded()
    {
        using var project = new TestProject();
        project.Write("config.csx", "throw new Exception(\"<img src=x onerror=alert(1)>\");");
        project.TrustCurrent();
        using var provider = project.CreateProvider();

        // Provider daje czysty tekst, kodowanie robi RenderService; tu sprawdzamy, że payload dociera
        // dosłownie, a test E2E sprawdza, że w stronie nie powstaje element
        Assert.Contains("<img src=x onerror=alert(1)>", (await provider.GetAsync()).Notice);
    }
}
