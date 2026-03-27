using System.Text.RegularExpressions;
using Microsoft.Msagl.Drawing;

namespace PSGraphView.Msagl;

internal static class MsaglSvgRenderer
{
    public static string RenderRawSvg(Graph drawingGraph)
    {
        using var stream = new MemoryStream();
        var svgWriter = new SvgGraphWriter(stream, drawingGraph);
        svgWriter.Write();
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string RenderSvg(Graph drawingGraph, string backgroundColor, bool showArrows, double? labelFontSize = null)
    {
        var svg = RenderRawSvg(drawingGraph);

        var background = string.IsNullOrWhiteSpace(backgroundColor) ? "#ffffff" : backgroundColor;
        var insertion = $"\n  <rect x=\"0\" y=\"0\" width=\"100%\" height=\"100%\" fill=\"{background}\" />\n";
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
}