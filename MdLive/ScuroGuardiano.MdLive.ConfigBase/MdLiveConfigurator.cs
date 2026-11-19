using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdown.ColorCode;
using Microsoft.Extensions.Logging;

namespace ScuroGuardiano.MdLive.ConfigBase;

public class MdLiveConfigurator
{
    protected ILogger Logger { get; private init; }

    /// <summary>
    /// Template used when <c>.mdlive/template.html</c> doesn't exist, also written by <c>mdlive init</c>.
    /// </summary>
    public static string DefaultHtmlTemplate =>
        // language=html
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <%styles%>
            <title><%title%></title>
        </head>
        <body>
            <main>
                <%content%>
            </main>
            
            <%scripts%>
        </body>
        </html>
        """;

    public MdLiveConfigurator(ILogger<MdLiveConfigurator> logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// <para>
    /// Creates <see cref="MarkdownPipeline"/> using <see cref="MarkdownPipelineBuilder"/>
    /// </para>
    /// <para>
    /// When overriding don't call base method.
    /// </para>
    /// </summary>
    /// <returns>Configured Markdown Pipeline</returns>
    public virtual Task<MarkdownPipeline> CreateMarkdownPipeline()
    {
        return Task.FromResult(
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseYamlFrontMatter()
                .UseColorCode(HtmlFormatterType.Css)
                .Build()
        );
    }

    /// <summary>
    /// <para>
    /// Transforms Markdown document, by default adding <c>data-line</c> attributes do every <see cref="Block"/>
    /// of <see cref="MarkdownDocument"/>
    /// </para>
    /// <para>
    /// You should call base method when overriding, so <c>data-line</c> are set
    /// </para>
    /// </summary>
    /// <param name="document">Parsed document</param>
    /// <returns></returns>
    public virtual Task TransformMarkdownDocument(MarkdownDocument document)
    {
        foreach (var block in document.Descendants<Block>())
        {
            // Line liczone od 0, w edytorach od 1
            var line = (block.Line + 1).ToString(CultureInfo.InvariantCulture);
            block.GetAttributes().AddPropertyIfNotExist("data-line", line);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// <para>
    /// Provides HTML template, by default trying to read from path <c>.mdlive/template.html</c>,
    /// falling back to <see cref="DefaultHtmlTemplate"/>
    /// </para>
    /// <para>
    /// You can call base method when overriding to get html template from mentioned path of default template
    /// and transform it at your will.
    /// </para>
    /// <para>
    /// Use <c>&lt;%styles%&gt;</c>, <c>&lt;%content%&gt;</c>, <c>&lt;%title%&gt;</c> and <c>&lt;%scripts%&gt;</c>
    /// in your template, MdLive will substitute those values.
    /// </para>
    /// </summary>
    /// <returns></returns>
    public virtual async Task<string> ProvideHtmlTemplate(MarkdownDocument markdownDocument)
    {
        var templatePath = Path.Join(".mdlive", "template.html");
        var template = await TryReadAllText(templatePath);

        if (template is null)
        {
            Logger.LogWarning("Falling back to default template");
            return DefaultHtmlTemplate;
        }

        return template;
    }

    /// <summary>
    /// <para>
    /// Returns HTML fragment with styles, by default trying to read from <c>.mdlive/style.css</c>,
    /// wrapping it in style tag or <c>.mdlive/styles.html</c>. If both are present, both will be added.
    /// <c>styles.html</c> will be added first, so rules from <c>style.css</c> can override third party styles.
    /// Fallbacks to no styles.
    /// </para>
    /// <para>
    /// When overriding this method remember that you must wrap any CSS in style tag
    /// or reference it with link tag.
    /// In this way you can include as many third party stylesheets as you want.
    /// Usually it's better to just create <c>.mdlive/styles.html</c> than overriding this method.
    /// </para>
    /// </summary>
    /// <param name="markdownDocument"></param>
    /// <returns>HTML fragment with styles</returns>
    public virtual async Task<string> ProvideStyles(MarkdownDocument markdownDocument)
    {
        var styleCssPath = Path.Join(".mdlive", "style.css");
        var stylesHtmlPath = Path.Join(".mdlive", "styles.html");
        var styleCss = await TryReadAllText(styleCssPath);
        var stylesHtml = await TryReadAllText(stylesHtmlPath);

        StringBuilder sb = new();

        if (stylesHtml is not null)
        {
            sb.Append(stylesHtml);
        }

        if (styleCss is not null)
        {
            sb.Append("<style>");
            sb.Append(styleCss);
            sb.Append("</style>");
        }

        return sb.ToString();
    }


    /// <summary>
    /// Provides title for Markdown document
    /// </summary>
    /// <returns></returns>
    public virtual Task<string?> ProvideTitle(MarkdownDocument markdownDocument)
    {
        var title = markdownDocument
            .OfType<HeadingBlock>()
            .FirstOrDefault(h => h.Level == 1);

        return Task.FromResult(title is not null ? GetPlainText(title.Inline) : null);
    }

    /// <summary>
    /// <para>
    /// Returns HTML fragment with scripts, by default trying to read from <c>.mdlive/script.js</c>,
    /// wrapping it in script tag or <c>.mdlive/scripts.html</c>. If both are present, both will be added.
    /// <c>scripts.html</c> will be added first.
    /// Fallbacks to no scripts.
    /// </para>
    /// <para>
    /// When overriding this method remember that you must wrap any JS in script tag.
    /// In this way you can include as many third party scripts as you want.
    /// Usually it's better to just create <c>.mdlive/scripts.html</c> than overriding this method.
    /// </para>
    /// </summary>
    /// <param name="markdownDocument"></param>
    /// <returns>HTML code with scripts</returns>
    public virtual async Task<string> ProvideScripts(MarkdownDocument markdownDocument)
    {
        var scriptJsPath = Path.Join(".mdlive", "script.js");
        var scriptsHtmlPath =  Path.Join(".mdlive", "scripts.html");
        var scriptJs = await TryReadAllText(scriptJsPath);
        var scriptsHtml = await TryReadAllText(scriptsHtmlPath);

        StringBuilder sb = new();

        if (scriptsHtml is not null)
        {
            sb.Append(scriptsHtml);
        }

        if (scriptJs is not null)
        {
            sb.Append("<script>");
            sb.Append(scriptJs);
            sb.Append("</script>");
        }

        return sb.ToString();
    }

    protected static string GetPlainText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var inline in container.Descendants<Inline>())
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.AsSpan());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    sb.Append(entity.Transcoded.AsSpan());
                    break;
                case LineBreakInline:
                    sb.Append(' ');
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Tries to read content from provided file, logging any exception other than <see cref="FileNotFoundException"/>
    /// or <see cref="DirectoryNotFoundException"/> and retuning null in case of failure
    /// </summary>
    /// <param name="path">Path to file</param>
    /// <returns></returns>
    protected async Task<string?> TryReadAllText(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {}
        catch (Exception ex)
        {
            Logger.LogError(ex, "Can't read {FilePath}. {ExceptionType}: {ExceptionMessage}", path,
                ex.GetType().FullName, ex.Message);
        }

        return null;
    }
}
