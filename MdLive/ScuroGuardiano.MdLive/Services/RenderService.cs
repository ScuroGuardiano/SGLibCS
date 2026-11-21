using Markdig;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using ScuroGuardiano.MdLive.Model;
using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class RenderService : BackgroundService
{
    private readonly Channel<DocumentContent> _contentChannel;
    private readonly PreviewHub _hub;
    private readonly ConfiguratorProvider _configuratorProvider;
    private readonly ILogger<RenderService> _logger;

    public RenderService(DocumentContentChannel contentChannel,
        PreviewHub hub,
        ConfiguratorProvider configuratorProvider,
        ILogger<RenderService> logger)
    {
        _contentChannel = contentChannel.Channel;
        _hub = hub;
        _configuratorProvider = configuratorProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var content in _contentChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Przed każdym renderem: config.csx mógł się zmienić albo zostać zaufany
                var active = await _configuratorProvider.GetAsync();
                _hub.PublishDocument(await RenderAsync(content, active));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Błąd w configu użytkownika nie może zabić pętli ani hosta
                _logger.LogError(ex, "Rendering {Path} failed", content.Path);
            }
        }
    }

    private static async Task<RenderedDocument> RenderAsync(DocumentContent content, ActiveConfigurator active)
    {
        var (configurator, pipeline, notice) = active;

        var document = Markdig.Markdown.Parse(content.Text, pipeline);
        await configurator.TransformMarkdownDocument(document);

        var markdownHtml = document.ToHtml(pipeline);

        var title = await configurator.ProvideTitle(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(content.Path);
        }

        title = HtmlEncoder.Default.Encode(title);

        var styles = await configurator.ProvideStyles(document);
        var scripts = await configurator.ProvideScripts(document);

        var template = HtmlTemplate.Parse(await configurator.ProvideHtmlTemplate(document));
        var templateData = new Dictionary<string, string>()
        {
            ["title"] = title,
            ["styles"] = styles,
            ["scripts"] = scripts,
            ["content"] = markdownHtml,
            ["__mdlive_notice"] = RenderNotice(notice)
        };

        return new RenderedDocument(content.Path, title, template.Render(templateData));
    }

    private static string RenderNotice(string? notice)
    {
        if (notice is null)
        {
            return "";
        }

        // Treść to m.in. błędy kompilatora z fragmentami kodu użytkownika, więc zawsze kodowana.
        // Styl inline: ma być czytelny niezależnie od arkusza użytkownika.
        // language=html
        return $"""
                <div id="__mdlive-notice" role="alert" style="position: fixed; left: 0; right: 0; bottom: 0; z-index: 2147483647; margin: 0; padding: 0.75em 1em; max-height: 40vh; overflow: auto; background: #3b1d1d; color: #ffd7d7; font: 13px/1.4 ui-monospace, monospace; white-space: pre-wrap; border-top: 2px solid #e5484d;">{HtmlEncoder.Default.Encode(notice)}</div>
                """;
    }
}
