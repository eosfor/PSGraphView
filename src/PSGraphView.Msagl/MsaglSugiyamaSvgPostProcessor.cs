using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Msagl.Core.Routing;
using PSGraph.Model;

namespace PSGraphView.Msagl;

internal static class MsaglSugiyamaSvgPostProcessor
{
    private static readonly Regex MsaglPrefixRegex = new(@"^\s*\d+\s*:\s*", RegexOptions.Compiled);
    private static readonly Regex LineEdgePathRegex = new(
        @"^M\s+(?<x1>-?\d+(?:\.\d+)?)\s+(?<y1>-?\d+(?:\.\d+)?)\s+L\s+(?<x2>-?\d+(?:\.\d+)?)\s+(?<y2>-?\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);
    private static readonly Regex TranslateRegex = new(
        @"translate\((?<x>-?\d+(?:\.\d+)?),(?<y>-?\d+(?:\.\d+)?)\)",
        RegexOptions.Compiled);

    public static string PostProcess(string svg, GraphView graph, MsaglSugiyamaOptions options)
    {
        var output = svg;

        if (options.ShowLabels)
        {
            output = TuneFlatSvgLabels(output, graph, options);
        }

        var routingMode = ResolveEdgeRouting(options.EdgeRouting);
        if (routingMode == EdgeRoutingMode.SugiyamaSplines || routingMode == EdgeRoutingMode.Spline)
        {
            output = TuneFlatSvgEdgesToBezier(output);
        }

        if (string.Equals(options.Direction, "Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            output = FlipHorizontalSvg(output, graph);
        }

        output = NormalizeFlatSvgViewport(output);
        return ApplyRequestedDimensions(output, options);
    }

    private static string TuneFlatSvgLabels(string svg, GraphView graph, MsaglSugiyamaOptions options)
    {
        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;
        var layoutInfoByLabel = BuildNodeLayoutInfoByLabel(graph);

        var group = doc.Descendants(svgNs + "g").FirstOrDefault();
        if (group is null)
        {
            return svg;
        }

        var placements = new Dictionary<XElement, FlatLabelPlacement>();
        var elements = group.Elements().ToList();
        for (var i = 0; i < elements.Count - 1; i++)
        {
            var ellipse = elements[i];
            var text = elements[i + 1];
            if (ellipse.Name.LocalName != "ellipse" || text.Name.LocalName != "text")
            {
                continue;
            }

            if (!double.TryParse(ellipse.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx) ||
                !double.TryParse(ellipse.Attribute("cy")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy))
            {
                continue;
            }

            var tspan = text.Elements().FirstOrDefault(e => e.Name.LocalName == "tspan");
            var label = NormalizeMsaglLabel((tspan?.Value ?? string.Empty).Trim());
            var width = Math.Max(6.0, label.Length * options.LabelFontSize * 0.56);
            var height = Math.Max(6.0, options.LabelFontSize + 2.0);
            var side = GetLabelPlacementSide(label, layoutInfoByLabel);
            var x = side == LabelPlacementSide.Left
                ? cx - options.NodeRadius - options.LabelOffsetX - width
                : cx + options.NodeRadius + options.LabelOffsetX;
            var desiredY = cy + (options.LabelFontSize * 0.35) + options.LabelOffsetY;

            text.SetAttributeValue("font-size", options.LabelFontSize.ToString("0.###", CultureInfo.InvariantCulture));
            if (tspan is not null)
            {
                tspan.Value = label;
            }

            placements[text] = new FlatLabelPlacement
            {
                Side = side,
                X = x,
                Width = width,
                DesiredY = desiredY,
                Y = desiredY,
                Height = height
            };
        }

        ResolveVerticalLabelOverlaps(placements.Values.ToList(), options.LabelFontSize);
        foreach (var (text, placement) in placements)
        {
            var x = placement.X.ToString("0.###", CultureInfo.InvariantCulture);
            var y = placement.Y.ToString("0.###", CultureInfo.InvariantCulture);
            text.SetAttributeValue("x", x);
            text.SetAttributeValue("y", y);
            var tspan = text.Elements().FirstOrDefault(e => e.Name.LocalName == "tspan");
            tspan?.SetAttributeValue("x", x);
        }

        return Save(doc);
    }

    private static string TuneFlatSvgEdgesToBezier(string svg)
    {
        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var path in doc.Descendants(svgNs + "path"))
        {
            if (!string.Equals(path.Attribute("fill")?.Value, "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var d = path.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(d))
            {
                continue;
            }

            var match = LineEdgePathRegex.Match(d.Trim());
            if (!match.Success)
            {
                continue;
            }

            var x1 = double.Parse(match.Groups["x1"].Value, CultureInfo.InvariantCulture);
            var y1 = double.Parse(match.Groups["y1"].Value, CultureInfo.InvariantCulture);
            var x2 = double.Parse(match.Groups["x2"].Value, CultureInfo.InvariantCulture);
            var y2 = double.Parse(match.Groups["y2"].Value, CultureInfo.InvariantCulture);
            var controlX = (x1 + x2) / 2.0;

            path.SetAttributeValue(
                "d",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "M {0:0.###} {1:0.###} C {2:0.###} {1:0.###} {2:0.###} {3:0.###} {4:0.###} {3:0.###}",
                    x1, y1, controlX, y2, x2));
        }

        return Save(doc);
    }

    private static string FlipHorizontalSvg(string svg, GraphView graph)
    {
        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;

        var rootLabels = graph.Nodes
            .Where(node => !graph.Edges.Any(edge => string.Equals(edge.TargetId, node.Id, StringComparison.Ordinal)))
            .Select(node => NormalizeMsaglLabel(node.Label))
            .ToHashSet(StringComparer.Ordinal);

        var textNodes = doc.Descendants(svgNs + "text")
            .Select(t => new
            {
                X = double.TryParse(t.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tx) ? tx : (double?)null,
                Label = NormalizeMsaglLabel(t.Elements().FirstOrDefault(e => e.Name.LocalName == "tspan")?.Value)
            })
            .Where(t => t.X.HasValue)
            .ToList();

        var rootX = textNodes.FirstOrDefault(t => rootLabels.Contains(t.Label))?.X;
        var avgX = textNodes.Count == 0 ? (double?)null : textNodes.Average(t => t.X!.Value);
        if (!rootX.HasValue || !avgX.HasValue || rootX.Value <= avgX.Value)
        {
            return svg;
        }

        var xValues = new List<double>();
        foreach (var ellipse in doc.Descendants(svgNs + "ellipse"))
        {
            if (double.TryParse(ellipse.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx))
            {
                xValues.Add(cx);
            }
        }

        foreach (var text in doc.Descendants(svgNs + "text"))
        {
            if (double.TryParse(text.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tx))
            {
                xValues.Add(tx);
            }
        }

        foreach (var path in doc.Descendants(svgNs + "path"))
        {
            var d = path.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(d))
            {
                continue;
            }

            xValues.AddRange(ExtractXCoordinatesFromPath(d));
        }

        if (xValues.Count == 0)
        {
            return svg;
        }

        var minX = xValues.Min();
        var maxX = xValues.Max();
        double Mirror(double x) => minX + maxX - x;

        foreach (var ellipse in doc.Descendants(svgNs + "ellipse"))
        {
            if (double.TryParse(ellipse.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx))
            {
                ellipse.SetAttributeValue("cx", Mirror(cx).ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        foreach (var text in doc.Descendants(svgNs + "text"))
        {
            if (double.TryParse(text.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tx))
            {
                var mirroredX = Mirror(tx).ToString("0.###", CultureInfo.InvariantCulture);
                text.SetAttributeValue("x", mirroredX);
                foreach (var tspan in text.Elements().Where(e => e.Name.LocalName == "tspan"))
                {
                    if (double.TryParse(tspan.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        tspan.SetAttributeValue("x", mirroredX);
                    }
                }
            }
        }

        foreach (var path in doc.Descendants(svgNs + "path"))
        {
            var d = path.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(d))
            {
                continue;
            }

            path.SetAttributeValue("d", MirrorPathX(d, Mirror));
        }

        return Save(doc);
    }

    private static string NormalizeFlatSvgViewport(string svg)
    {
        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;
        var group = doc.Descendants(svgNs + "g").FirstOrDefault();
        if (group is null)
        {
            return svg;
        }

        var transform = group.Attribute("transform")?.Value ?? string.Empty;
        var transformMatch = TranslateRegex.Match(transform);
        if (!transformMatch.Success)
        {
            return svg;
        }

        var tx = double.Parse(transformMatch.Groups["x"].Value, CultureInfo.InvariantCulture);
        var ty = double.Parse(transformMatch.Groups["y"].Value, CultureInfo.InvariantCulture);

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        static void Include(ref double minX, ref double minY, ref double maxX, ref double maxY, double x, double y)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        foreach (var ellipse in doc.Descendants(svgNs + "ellipse"))
        {
            if (!double.TryParse(ellipse.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cx) ||
                !double.TryParse(ellipse.Attribute("cy")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cy))
            {
                continue;
            }

            var rx = double.TryParse(ellipse.Attribute("rx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rvx) ? rvx : 0.0;
            var ry = double.TryParse(ellipse.Attribute("ry")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rvy) ? rvy : 0.0;
            Include(ref minX, ref minY, ref maxX, ref maxY, cx - rx, cy - ry);
            Include(ref minX, ref minY, ref maxX, ref maxY, cx + rx, cy + ry);
        }

        foreach (var text in doc.Descendants(svgNs + "text"))
        {
            if (!double.TryParse(text.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(text.Attribute("y")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            var fontSize = double.TryParse(text.Attribute("font-size")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFontSize)
                ? parsedFontSize
                : 8.0;
            var label = text.Elements().FirstOrDefault(e => e.Name.LocalName == "tspan")?.Value ?? string.Empty;
            var width = Math.Max(4.0, label.Length * fontSize * 0.56);
            Include(ref minX, ref minY, ref maxX, ref maxY, x, y - fontSize);
            Include(ref minX, ref minY, ref maxX, ref maxY, x + width, y + (fontSize * 0.25));
        }

        foreach (var path in doc.Descendants(svgNs + "path"))
        {
            var d = path.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(d))
            {
                continue;
            }

            var tokens = Regex.Matches(d, @"[A-Za-z]|-?\d+(?:\.\d+)?")
                .Select(m => m.Value);
            var expectX = true;
            double? x = null;
            foreach (var token in tokens)
            {
                if (token.Length == 1 && char.IsLetter(token[0]))
                {
                    expectX = true;
                    x = null;
                    continue;
                }

                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                if (expectX)
                {
                    x = value;
                }
                else if (x.HasValue)
                {
                    Include(ref minX, ref minY, ref maxX, ref maxY, x.Value, value);
                }

                expectX = !expectX;
            }
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
        {
            return svg;
        }

        var renderedMinX = minX + tx;
        var renderedMinY = minY + ty;
        var renderedMaxX = maxX + tx;
        var renderedMaxY = maxY + ty;
        const double pad = 8.0;

        tx += pad - renderedMinX;
        ty += pad - renderedMinY;
        group.SetAttributeValue(
            "transform",
            string.Format(CultureInfo.InvariantCulture, "translate({0:0.###},{1:0.###})", tx, ty));

        var widthValue = (renderedMaxX - renderedMinX) + (2 * pad);
        var heightValue = (renderedMaxY - renderedMinY) + (2 * pad);
        doc.Root?.SetAttributeValue("width", widthValue.ToString("0.###", CultureInfo.InvariantCulture));
        doc.Root?.SetAttributeValue("height", heightValue.ToString("0.###", CultureInfo.InvariantCulture));
        doc.Root?.SetAttributeValue(
            "viewBox",
            string.Format(
                CultureInfo.InvariantCulture,
                "0 0 {0:0.###} {1:0.###}",
                widthValue,
                heightValue));

        return Save(doc);
    }

    private static string ApplyRequestedDimensions(string svg, MsaglSugiyamaOptions options)
    {
        if (options.Width is null && options.Height is null)
        {
            return svg;
        }

        var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
        if (doc.Root is null)
        {
            return svg;
        }

        if (options.Width is not null)
        {
            doc.Root.SetAttributeValue("width", options.Width.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (options.Height is not null)
        {
            doc.Root.SetAttributeValue("height", options.Height.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        return Save(doc);
    }

    private static IReadOnlyDictionary<string, NodeLayoutInfo> BuildNodeLayoutInfoByLabel(GraphView graph)
    {
        var nodesById = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var childrenById = graph.Nodes.ToDictionary(node => node.Id, _ => new List<string>(), StringComparer.Ordinal);
        var incomingById = graph.Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);

        foreach (var edge in graph.Edges)
        {
            if (!childrenById.ContainsKey(edge.SourceId) || !incomingById.ContainsKey(edge.TargetId))
            {
                continue;
            }

            childrenById[edge.SourceId].Add(edge.TargetId);
            incomingById[edge.TargetId]++;
        }

        var depthById = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var rootId in incomingById.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key))
        {
            depthById[rootId] = 0;
            queue.Enqueue(rootId);
        }

        if (queue.Count == 0)
        {
            foreach (var node in graph.Nodes)
            {
                depthById[node.Id] = 0;
                queue.Enqueue(node.Id);
            }
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var currentDepth = depthById[currentId];
            foreach (var childId in childrenById[currentId])
            {
                var nextDepth = currentDepth + 1;
                if (depthById.TryGetValue(childId, out var knownDepth) && knownDepth <= nextDepth)
                {
                    continue;
                }

                depthById[childId] = nextDepth;
                queue.Enqueue(childId);
            }
        }

        var result = new Dictionary<string, NodeLayoutInfo>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            var normalizedLabel = NormalizeMsaglLabel(node.Label);
            var depth = depthById.TryGetValue(node.Id, out var knownDepth) ? knownDepth : 0;
            var isLeaf = childrenById[node.Id].Count == 0;
            result[normalizedLabel] = new NodeLayoutInfo(depth, isLeaf);
        }

        return result;
    }

    private static LabelPlacementSide GetLabelPlacementSide(string label, IReadOnlyDictionary<string, NodeLayoutInfo> layoutInfoByLabel)
    {
        var normalizedLabel = NormalizeMsaglLabel(label);
        if (!layoutInfoByLabel.TryGetValue(normalizedLabel, out var info))
        {
            return LabelPlacementSide.Right;
        }

        if (info.IsLeaf)
        {
            return LabelPlacementSide.Right;
        }

        return info.Depth <= 1 ? LabelPlacementSide.Left : LabelPlacementSide.Right;
    }

    private static void ResolveVerticalLabelOverlaps(List<FlatLabelPlacement> placements, double labelFontSize)
    {
        if (placements.Count < 2)
        {
            return;
        }

        var minGap = Math.Max(1.0, labelFontSize * 0.3);
        foreach (var group in placements.GroupBy(placement => placement.Side))
        {
            var ordered = group.OrderBy(placement => placement.DesiredY).ToList();
            if (ordered.Count == 0)
            {
                continue;
            }

            var placed = new List<FlatLabelPlacement>(ordered.Count);
            foreach (var current in ordered)
            {
                var y = current.DesiredY;
                while (true)
                {
                    var requiredY = y;
                    foreach (var previous in placed)
                    {
                        if (!RangesOverlap(current.X, current.X + current.Width, previous.X, previous.X + previous.Width))
                        {
                            continue;
                        }

                        var minY = previous.Y + ((previous.Height + current.Height) / 2.0) + minGap;
                        if (requiredY < minY)
                        {
                            requiredY = minY;
                        }
                    }

                    if (requiredY <= y + 0.001)
                    {
                        break;
                    }

                    y = requiredY;
                }

                current.Y = y;
                placed.Add(current);
            }
        }
    }

    private static bool RangesOverlap(double a1, double a2, double b1, double b2)
    {
        return a1 < b2 && b1 < a2;
    }

    private static IEnumerable<double> ExtractXCoordinatesFromPath(string d)
    {
        var tokens = Regex.Matches(d, @"[A-Za-z]|-?\d+(?:\.\d+)?")
            .Select(match => match.Value);

        var expectX = true;
        foreach (var token in tokens)
        {
            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                expectX = true;
                continue;
            }

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (expectX)
            {
                yield return value;
            }

            expectX = !expectX;
        }
    }

    private static string MirrorPathX(string d, Func<double, double> mirror)
    {
        var tokens = Regex.Matches(d, @"[A-Za-z]|-?\d+(?:\.\d+)?")
            .Select(match => match.Value)
            .ToList();

        var output = new List<string>(tokens.Count);
        var expectX = true;
        foreach (var token in tokens)
        {
            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                output.Add(token);
                expectX = true;
                continue;
            }

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                output.Add(token);
                continue;
            }

            var transformed = expectX ? mirror(value) : value;
            output.Add(transformed.ToString("0.###", CultureInfo.InvariantCulture));
            expectX = !expectX;
        }

        return string.Join(" ", output);
    }

    private static EdgeRoutingMode ResolveEdgeRouting(string edgeRouting)
    {
        return Enum.TryParse<EdgeRoutingMode>(edgeRouting, true, out var mode)
            ? mode
            : EdgeRoutingMode.SugiyamaSplines;
    }

    private static string NormalizeMsaglLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        return MsaglPrefixRegex.Replace(label, string.Empty);
    }

    private static string Save(XDocument document)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder, CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return builder.ToString();
    }

    private enum LabelPlacementSide
    {
        Left,
        Right
    }

    private sealed class FlatLabelPlacement
    {
        public required LabelPlacementSide Side { get; init; }
        public required double X { get; init; }
        public required double Width { get; init; }
        public required double DesiredY { get; init; }
        public required double Height { get; init; }
        public double Y { get; set; }
    }

    private readonly struct NodeLayoutInfo
    {
        public NodeLayoutInfo(int depth, bool isLeaf)
        {
            Depth = depth;
            IsLeaf = isLeaf;
        }

        public int Depth { get; }
        public bool IsLeaf { get; }
    }
}
