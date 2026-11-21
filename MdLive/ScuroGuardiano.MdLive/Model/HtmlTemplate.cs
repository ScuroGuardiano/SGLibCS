using System.Text;

namespace ScuroGuardiano.MdLive.Model;

internal sealed class HtmlTemplate
{
    private readonly (string Text, bool IsPlaceholder)[] _segments;

    private HtmlTemplate((string, bool)[] segments) => _segments = segments;

    public static HtmlTemplate Parse(string template)
    {
        // Dirty solution
        template = InjectMdLiveElements(template);

        var segments = new List<(string, bool)>();
        var position = 0;

        while (position < template.Length)
        {
            var start = template.IndexOf("<%", position, StringComparison.Ordinal);
            var end = start < 0 ? -1 : template.IndexOf("%>", start + 2, StringComparison.Ordinal);

            if (end < 0)
            {
                segments.Add((template[position..], false));
                break;
            }

            segments.Add((template[position..start], false));
            segments.Add((template[(start + 2)..end], true));
            position = end + 2;
        }

        return new HtmlTemplate([.. segments]);
    }

    public string Render(IReadOnlyDictionary<string, string> values)
    {
        var sb = new StringBuilder();

        foreach (var (text, isPlaceholder) in _segments)
        {
            if (!isPlaceholder)
            {
                sb.Append(text);
            }
            else if (values.TryGetValue(text, out var value))
            {
                sb.Append(value);
            }
            else
            {
                // Nieznany placeholder zostawiamy jak był, łatwiej zauważyć literówkę
                sb.Append("<%").Append(text).Append("%>");
            }
        }

        return sb.ToString();
    }

    private static string InjectMdLiveElements(string template)
    {
        // language=html
        const string headElements =
            """
              <script id="__jajebie" data-ignore-morph type="module" src="/scripts/datastar.js"></script>
              <script id="__noszkurwa" data-ignore-morph src="/scripts/mdlive.js"></script>
              <style id="__mdlive-style" data-ignore-morph>
                /* Odstęp na dole, żeby ostatnie bloki też dało się przewinąć na środek ekranu */
                body::after { content: ""; display: block; height: 50vh; }
              </style>

            """;

        // language=html
        const string bodyElements =
            """
              <div id="__japierdole" data-ignore-morph style="display: none;" data-init="@get('/preview/sse')"></div>
              <%__mdlive_notice%>

            """;

        template = InsertBefore(template, "</head>", headElements, lastOccurrence: false);
        template = InsertBefore(template, "</body>", bodyElements, lastOccurrence: true);

        return template;
    }

    /// <summary>
    /// Inserts content before a closing tag. Closing tags have no attributes, so unlike <c>&lt;body&gt;</c>
    /// they match regardless of how the user's template opens the element.
    /// </summary>
    private static string InsertBefore(string template, string closingTag, string content, bool lastOccurrence)
    {
        var index = lastOccurrence
            ? template.LastIndexOf(closingTag, StringComparison.OrdinalIgnoreCase)
            : template.IndexOf(closingTag, StringComparison.OrdinalIgnoreCase);

        // Szablon bez tego tagu: przeglądarka i tak przyjmie elementy na końcu dokumentu
        return index < 0 ? template + content : template.Insert(index, content);
    }
}