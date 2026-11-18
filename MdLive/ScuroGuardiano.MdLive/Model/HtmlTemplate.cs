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
        const string mdLiveScripts =
            """

              <script id="__jajebie" data-ignore-morph type="module" src="/scripts/datastar.js"></script>
              <script id="__noszkurwa" data-ignore-morph src="/scripts/mdlive.js"></script>
            </head>
            """;

        // language=html
        const string mdLiveSseListener =
            """
            <body>
              <div id="__japierdole" data-ignore-morph style="display: none;" data-on:load__window="@get('/preview/sse')"></div>
            """;

        template = template
            .Replace("</head>", mdLiveScripts)
            .Replace("<body>", mdLiveSseListener);

        return template;
    }
}