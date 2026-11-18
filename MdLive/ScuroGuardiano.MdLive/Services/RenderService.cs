using System.Text.Encodings.Web;
using System.Threading.Channels;
using Markdig;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class RenderService : BackgroundService
{
    private readonly Channel<DocumentContent> _contentChannel;
    private readonly PreviewHub _hub;
    private readonly MdLiveConfigurator _configurator;
    private readonly ILogger<RenderService> _logger;

    public RenderService(DocumentContentChannel contentChannel,
        PreviewHub hub,
        MdLiveConfigurator configurator,
        ILogger<RenderService> logger)
    {
        _contentChannel = contentChannel.Channel;
        _hub = hub;
        _configurator = configurator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeline = await _configurator.CreateMarkdownPipeline();

        await foreach (var content in _contentChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _hub.PublishDocument(await RenderAsync(content, pipeline));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Błąd w configu użytkownika nie może zabić pętli ani hosta
                _logger.LogError(ex, "Rendering {Path} failed", content.Path);
            }
        }
    }

    private async Task<RenderedDocument> RenderAsync(DocumentContent content, MarkdownPipeline pipeline)
    {
        var document = Markdig.Markdown.Parse(content.Text, pipeline);
        await _configurator.TransformMarkdownDocument(document);

        var markdownHtml = document.ToHtml(pipeline);

        var title = await _configurator.ProvideTitle(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(content.Path);
        }

        title = HtmlEncoder.Default.Encode(title);

        var styles = await _configurator.ProvideStyles(document);
        var scripts = await _configurator.ProvideScripts(document);

        var template = HtmlTemplate.Parse(await _configurator.ProvideHtmlTemplate(document));
        var templateData = new Dictionary<string, string>()
        {
            ["title"] = title,
            ["styles"] = styles,
            ["scripts"] = scripts,
            ["content"] = markdownHtml
        };

        return new RenderedDocument(content.Path, title, template.Render(templateData));
    }
}
