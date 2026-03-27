namespace PSGraphView.Vega;

internal static class VegaHtmlPageRenderer
{
    public static string Render(string json, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=\"utf-8\">
            <title>{title}</title>
            <script src=\"https://cdn.jsdelivr.net/npm/vega@6\"></script>
            <script src=\"https://cdn.jsdelivr.net/npm/vega-lite@6\"></script>
            <script src=\"https://cdn.jsdelivr.net/npm/vega-embed@7\"></script>
        </head>
        <body>
            <div id=\"vis\"></div>
            <script type=\"text/javascript\">
                const spec = {json};
                vegaEmbed(\"#vis\", spec).catch(console.error);
            </script>
        </body>
        </html>
        """;
    }
}