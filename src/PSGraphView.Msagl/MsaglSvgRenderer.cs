using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Msagl.Drawing;

namespace PSGraphView.Msagl;

internal static class MsaglSvgRenderer
{
    public static string RenderRawSvg(Graph drawingGraph, double? width = null, double? height = null)
    {
        using var stream = new MemoryStream();
        var svgWriter = new SvgGraphWriter(stream, drawingGraph);
        svgWriter.Write();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var svg = reader.ReadToEnd();
        return ApplySize(svg, width, height);
    }

    public static string RenderSvg(
        Graph drawingGraph,
        string backgroundColor,
        bool showArrows,
        double? labelFontSize = null,
        double? width = null,
        double? height = null)
    {
        var svg = RenderRawSvg(drawingGraph, width, height);

        var background = string.IsNullOrWhiteSpace(backgroundColor) ? "#ffffff" : backgroundColor;
        var backgroundWidth = width?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "100%";
        var backgroundHeight = height?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "100%";
        var insertion = $"\n  <rect x=\"0\" y=\"0\" width=\"{backgroundWidth}\" height=\"{backgroundHeight}\" fill=\"{background}\" />\n";
        var svgStart = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgStart >= 0)
        {
            var svgTagEnd = svg.IndexOf('>', svgStart);
            if (svgTagEnd >= 0)
            {
                svg = svg.Insert(svgTagEnd + 1, insertion);
            }
        }

        if (!showArrows)
        {
            svg = Regex.Replace(svg, @"^\s*<polygon\b[^>]*/>\s*$\r?\n?", string.Empty, RegexOptions.Multiline);
        }

        if (labelFontSize is double fontSize)
        {
            svg = Regex.Replace(
                svg,
                @"<text(?![^>]*\bfont-size=)",
                $"<text font-size=\"{fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"",
                RegexOptions.IgnoreCase);
        }

        return svg;
    }

    private static string ApplySize(string svg, double? width, double? height)
    {
        if (width is null && height is null)
        {
            return svg;
        }

        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        if (doc.Root is null)
        {
            return svg;
        }

        var currentWidth = ParseSvgLength(doc.Root.Attribute("width")?.Value);
        var currentHeight = ParseSvgLength(doc.Root.Attribute("height")?.Value);
        if (doc.Root.Attribute("viewBox") is null && currentWidth is not null && currentHeight is not null)
        {
            doc.Root.SetAttributeValue(
                "viewBox",
                $"0 0 {currentWidth.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} {currentHeight.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (width is not null)
        {
            doc.Root.SetAttributeValue("width", width.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (height is not null)
        {
            doc.Root.SetAttributeValue("height", height.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static double? ParseSvgLength(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var numericPart = Regex.Match(rawValue, @"-?\d+(?:\.\d+)?").Value;
        return double.TryParse(numericPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
