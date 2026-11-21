using System.Reflection;
using Markdig;
using ScuroGuardiano.MdLive.ConfigBase;

namespace ScuroGuardiano.MdLive.Scripting;

/// <param name="Notice">Plain text shown in the preview when the config script isn't used, e.g. not trusted.</param>
internal sealed record ActiveConfigurator(MdLiveConfigurator Configurator, MarkdownPipeline Pipeline, string? Notice);

/// <summary>
/// <para>
/// Provides the configurator for the next render: the one from <c>.mdlive/config.csx</c> when it exists and
/// is trusted, otherwise the default <see cref="MdLiveConfigurator"/> with a notice explaining why.
/// </para>
/// <para>
/// The bundle is re-read and re-hashed before every render (it's a few small files), so a change to any
/// part of the config, or to the trust store, is picked up right away. Recompilation only happens when the
/// hash changes. Not thread-safe, meant for the single render loop.
/// </para>
/// </summary>
internal sealed class ConfiguratorProvider : IDisposable
{
    public const string DefaultConfigDirectory = ".mdlive";

    // Synchronicznej nieskończonej pętli i tak nie przerwiemy (w .NET nie ma Thread.Abort),
    // ale render wraca do domyślnego configu z czytelnym błędem zamiast wisieć w nieskończoność
    internal TimeSpan ScriptTimeout { get; init; } = TimeSpan.FromSeconds(15);

    private readonly MdLiveConfigurator _defaultConfigurator;
    private readonly IServiceProvider _services;
    private readonly TrustStore _trustStore;
    private readonly ScriptCompiler _compiler;
    private readonly ILogger<ConfiguratorProvider> _logger;
    private readonly string _configDirectory;

    private string? _currentKey;
    private ActiveConfigurator? _current;
    private ScriptLoadContext? _currentContext;
    private MarkdownPipeline? _defaultPipeline;

    public ConfiguratorProvider(MdLiveConfigurator defaultConfigurator,
        IServiceProvider services,
        TrustStore trustStore,
        ScriptCompiler compiler,
        ILogger<ConfiguratorProvider> logger,
        string configDirectory = DefaultConfigDirectory)
    {
        _defaultConfigurator = defaultConfigurator;
        _services = services;
        _trustStore = trustStore;
        _compiler = compiler;
        _logger = logger;
        _configDirectory = configDirectory;
    }

    /// <summary>Weak reference to the load context of the script that was replaced last, for tests.</summary>
    internal WeakReference? LastUnloadedContext { get; private set; }

    public async Task<ActiveConfigurator> GetAsync()
    {
        var bundle = ScriptBundle.Read(_configDirectory, ScriptCompiler.IsKnownAssemblyName);
        var trust = bundle is null ? null : _trustStore.GetState(bundle);
        var key = bundle is null ? "none" : $"{bundle.Hash}:{trust!.Status}";

        if (_current is not null && key == _currentKey)
        {
            return _current;
        }

        ScriptLoadContext? context = null;
        ActiveConfigurator next;

        if (bundle is null)
        {
            next = await CreateDefault(null);
        }
        else if (trust!.Status != TrustStatus.Trusted)
        {
            var notice = DescribeUntrusted(bundle, trust);
            _logger.LogWarning("{Notice}", notice);
            next = await CreateDefault(notice);
        }
        else
        {
            try
            {
                (next, context) = await LoadScript(bundle);
                _logger.LogInformation("Loaded {Path} ({Hash})", bundle.Main.FullPath, bundle.Hash[..12]);
            }
            catch (Exception ex)
            {
                var notice = ex is ScriptCompilationException or ScriptLoadException
                    ? ex.Message
                    : $"{ScriptBundle.MainFileName} failed: {ex}";

                _logger.LogError("{Notice}", notice);
                next = await CreateDefault(notice);
            }
        }

        Replace(next, context, key);
        return next;
    }

    private async Task<(ActiveConfigurator, ScriptLoadContext)> LoadScript(ScriptBundle bundle)
    {
        var compiled = _compiler.Compile(bundle);
        foreach (var warning in compiled.Warnings)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        var context = new ScriptLoadContext($"mdlive-config-{bundle.Hash[..12]}", compiled.ReferencedAssemblies);

        try
        {
            var assembly = context.LoadFromStream(new MemoryStream(compiled.Assembly), new MemoryStream(compiled.Pdb));

            // Kontrakt kompilatora skryptów Roslyn (tak samo robi CSharpScript.RunAsync):
            // static Task<object> <Factory>(object[] submissionArray), gdzie [0] to obiekt globals,
            // a pod kolejnymi indeksami zapisują się instancje kolejnych submission. Mamy jedną, bez globals.
            var factory = assembly.GetType(compiled.EntryPointType, throwOnError: true)!
                .GetMethod(compiled.EntryPointMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
                .CreateDelegate<Func<object?[], Task<object?>>>();

            var result = await Task.Run(() => factory(new object?[2])).WaitAsync(ScriptTimeout);
            var configurator = CreateConfigurator(result, assembly);
            var pipeline = await Task.Run(configurator.CreateMarkdownPipeline).WaitAsync(ScriptTimeout);

            return (new ActiveConfigurator(configurator, pipeline, null), context);
        }
        catch (TimeoutException)
        {
            context.Unload();
            throw new ScriptLoadException($"{ScriptBundle.MainFileName} didn't finish in {ScriptTimeout.TotalSeconds} s");
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    /// <summary>
    /// The script may return a configurator instance, a configurator type to be created with dependency
    /// injection, or nothing, in which case it has to declare exactly one non-abstract configurator class.
    /// </summary>
    private MdLiveConfigurator CreateConfigurator(object? result, Assembly assembly)
    {
        switch (result)
        {
            case MdLiveConfigurator configurator:
                return configurator;

            case Type type when type.IsAssignableTo(typeof(MdLiveConfigurator)) && !type.IsAbstract:
                return (MdLiveConfigurator)ActivatorUtilities.CreateInstance(_services, type);

            case null:
                var candidates = assembly.GetTypes()
                    .Where(t => t.IsAssignableTo(typeof(MdLiveConfigurator)) && !t.IsAbstract)
                    .ToList();

                return candidates.Count switch
                {
                    1 => (MdLiveConfigurator)ActivatorUtilities.CreateInstance(_services, candidates[0]),
                    0 => throw new ScriptLoadException(
                        $"{ScriptBundle.MainFileName} doesn't declare a class deriving from {nameof(MdLiveConfigurator)}"),
                    _ => throw new ScriptLoadException(
                        $"{ScriptBundle.MainFileName} declares {candidates.Count} configurator classes " +
                        $"({string.Join(", ", candidates.Select(c => c.Name))}), end the script with `return typeof(TheOne);`")
                };

            default:
                throw new ScriptLoadException(
                    $"{ScriptBundle.MainFileName} returned {result.GetType()}, expected a {nameof(MdLiveConfigurator)}, " +
                    "its type or nothing");
        }
    }

    private static string DescribeUntrusted(ScriptBundle bundle, TrustState trust) => trust.Status switch
    {
        TrustStatus.Changed => $"{bundle.Main.FullPath} has changed since it was trusted, so the default config is used. " +
                               "Review the changes and run `mdlive trust` (:MdLiveTrust in Neovim).",
        TrustStatus.StoreRejected => $"{bundle.Main.FullPath} is not used: {trust.Problem}",
        _ => $"{bundle.Main.FullPath} is not trusted, so the default config is used. " +
             "Review it and run `mdlive trust` (:MdLiveTrust in Neovim)."
    };

    private async Task<ActiveConfigurator> CreateDefault(string? notice)
    {
        _defaultPipeline ??= await _defaultConfigurator.CreateMarkdownPipeline();
        return new ActiveConfigurator(_defaultConfigurator, _defaultPipeline, notice);
    }

    private void Replace(ActiveConfigurator next, ScriptLoadContext? context, string key)
    {
        var previous = _currentContext;

        _current = next;
        _currentContext = context;
        _currentKey = key;

        if (previous is not null)
        {
            // Unload jest kooperacyjny: ALC zniknie, gdy nic już nie trzyma jego typów.
            // Render loop jest jeden, więc poprzedni configurator nie jest już w użyciu.
            LastUnloadedContext = new WeakReference(previous);
            previous.Unload();
        }
    }

    public void Dispose()
    {
        _currentContext?.Unload();
        _currentContext = null;
    }
}

internal sealed class ScriptLoadException(string message) : Exception(message);
