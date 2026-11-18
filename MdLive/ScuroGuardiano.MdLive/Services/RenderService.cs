using System.Threading.Channels;
using Markdig;
using Markdig.Renderers;
using ScuroGuardiano.MdLive.ConfigBase;
using ScuroGuardiano.MdLive.Model;

namespace ScuroGuardiano.MdLive.Services;

internal sealed class RenderService(
    Channel<DocumentContent> contentChannel,
    PreviewHub hub,
    MdLiveConfigurator configurator,
    ILogger<RenderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeline = await configurator.CreateMarkdownPipeline();

        await foreach (var content in contentChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                hub.PublishDocument(await RenderAsync(content, pipeline));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Błąd w configu użytkownika nie może zabić pętli ani hosta
                logger.LogError(ex, "Rendering {Path} failed", content.Path);
            }
        }
    }

    private async Task<RenderedDocument> RenderAsync(DocumentContent content, MarkdownPipeline pipeline)
    {
        // TODO: Scuro Guardiano SPRAWDŹ W SZCZEGÓLNOŚCI TEN KOD
        // Pełna nazwa, bo namespace Markdown.ColorCode przesłania klasę Markdig.Markdown
        var document = Markdig.Markdown.Parse(content.Text, pipeline);
        await configurator.TransformMarkdownDocument(document);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        renderer.Render(document);

        var title = await configurator.ProvideTitle(document);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = Path.GetFileNameWithoutExtension(content.Path);
        }

        return new RenderedDocument(content.Path, title, writer.ToString());
    }
}
