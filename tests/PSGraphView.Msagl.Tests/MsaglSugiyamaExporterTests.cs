using System.Globalization;
using System.Xml.Linq;
using PSGraph.Model;
using PSGraphView.Msagl;

namespace PSGraphView.Msagl.Tests;

public class MsaglSugiyamaExporterTests
{
    private readonly MsaglSugiyamaExporter _exporter = new();

    [Fact]
    public void ExportSvg_ReturnsSvgWithDefaultFlatStyle()
    {
        var graph = CreateGraphView();

        var svg = _exporter.Export(graph);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fill=\"#ffffff\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<polygon", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportSvg_RespectsDirectionOption()
    {
        var graph = CreateChainGraph(length: 8);

        var horizontalSvg = _exporter.Export(graph, new MsaglSugiyamaOptions { Direction = "Horizontal" });
        var verticalSvg = _exporter.Export(graph, new MsaglSugiyamaOptions { Direction = "Vertical" });

        var (horizontalWidth, horizontalHeight) = ReadSvgDimensions(horizontalSvg);
        var (verticalWidth, verticalHeight) = ReadSvgDimensions(verticalSvg);

        Assert.True(horizontalWidth > horizontalHeight);
        Assert.True(verticalHeight > verticalWidth);
    }

    [Fact]
    public void ExportSvg_RespectsLabelOptions()
    {
        var graph = CreateGraphView();

        var svg = _exporter.Export(graph, new MsaglSugiyamaOptions
        {
            ShowLabels = true,
            LabelFontSize = 7.0,
            EdgeRouting = "SugiyamaSplines"
        });

        Assert.Contains("<text", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("font-size=\"7", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">A<", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">B<", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<polygon", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportSvg_PlacesTreeLabelsByLevelAndAvoidsOverlaps()
    {
        var graph = CreateTieredTreeGraph(level1Count: 10, leavesPerLevel1: 3);

        var svg = _exporter.Export(graph, new MsaglSugiyamaOptions
        {
            Direction = "Horizontal",
            ShowLabels = true,
            LabelFontSize = 8.0
        });

        var placements = ReadSvgLabelPlacements(svg);
        Assert.NotEmpty(placements);

        var (depthByLabel, isLeafByLabel) = ComputeTreeDepthAndLeafInfo(graph);
        foreach (var placement in placements)
        {
            Assert.True(depthByLabel.ContainsKey(placement.Label));
            Assert.True(isLeafByLabel.ContainsKey(placement.Label));

            if (isLeafByLabel[placement.Label])
            {
                Assert.True(placement.IsRightOfNode, $"Leaf '{placement.Label}' label should be placed on the right");
            }
            else if (depthByLabel[placement.Label] <= 1)
            {
                Assert.True(placement.IsLeftOfNode, $"Root/level-1 '{placement.Label}' label should be placed on the left");
            }
        }

        var overlaps = CountLabelOverlaps(placements);
        Assert.Equal(0, overlaps);
    }

    private static GraphView CreateGraphView()
    {
        return new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?> { ["group"] = 1 }),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?> { ["group"] = 2 }),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("B", "C", null, 1)
            ]);
    }

    private static GraphView CreateChainGraph(int length)
    {
        var nodes = Enumerable.Range(0, length)
            .Select(index => new GraphViewNode($"N{index}", $"N{index}", null, new Dictionary<string, object?>()))
            .ToList();

        var edges = Enumerable.Range(0, length - 1)
            .Select(index => new GraphViewEdge($"N{index}", $"N{index + 1}", null, 1))
            .ToList();

        return new GraphView(nodes, edges);
    }

    private static GraphView CreateTieredTreeGraph(int level1Count, int leavesPerLevel1)
    {
        var nodes = new List<GraphViewNode>();
        var edges = new List<GraphViewEdge>();

        nodes.Add(new GraphViewNode("root", "root", null, new Dictionary<string, object?>()));
        for (var i = 0; i < level1Count; i++)
        {
            var middleId = $"m{i}";
            nodes.Add(new GraphViewNode(middleId, middleId, null, new Dictionary<string, object?>()));
            edges.Add(new GraphViewEdge("root", middleId, null, 1));

            for (var j = 0; j < leavesPerLevel1; j++)
            {
                var leafId = $"l{i}_{j}";
                nodes.Add(new GraphViewNode(leafId, leafId, null, new Dictionary<string, object?>()));
                edges.Add(new GraphViewEdge(middleId, leafId, null, 1));
            }
        }

        return new GraphView(nodes, edges);
    }

    private static (Dictionary<string, int> DepthByLabel, Dictionary<string, bool> IsLeafByLabel) ComputeTreeDepthAndLeafInfo(GraphView graph)
    {
        var childrenByLabel = graph.Nodes.ToDictionary(node => node.Label, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            var sourceLabel = graph.Nodes.First(node => string.Equals(node.Id, edge.SourceId, StringComparison.Ordinal)).Label;
            var targetLabel = graph.Nodes.First(node => string.Equals(node.Id, edge.TargetId, StringComparison.Ordinal)).Label;
            childrenByLabel[sourceLabel].Add(targetLabel);
        }

        var incomingByLabel = graph.Nodes.ToDictionary(node => node.Label, _ => 0, StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            var targetLabel = graph.Nodes.First(node => string.Equals(node.Id, edge.TargetId, StringComparison.Ordinal)).Label;
            incomingByLabel[targetLabel]++;
        }

        var depthByLabel = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var rootLabel in incomingByLabel.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key))
        {
            depthByLabel[rootLabel] = 0;
            queue.Enqueue(rootLabel);
        }

        if (queue.Count == 0)
        {
            foreach (var label in childrenByLabel.Keys)
            {
                depthByLabel[label] = 0;
                queue.Enqueue(label);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDepth = depthByLabel[current];
            foreach (var child in childrenByLabel[current])
            {
                var nextDepth = currentDepth + 1;
                if (depthByLabel.TryGetValue(child, out var knownDepth) && knownDepth <= nextDepth)
                {
                    continue;
                }

                depthByLabel[child] = nextDepth;
                queue.Enqueue(child);
            }
        }

        var isLeafByLabel = childrenByLabel.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0, StringComparer.Ordinal);
        return (depthByLabel, isLeafByLabel);
    }

    private sealed record SvgLabelPlacement(string Label, double NodeX, double TextX, double TextY, double FontSize, double Width, double Height)
    {
        public bool IsRightOfNode => TextX >= NodeX;
        public bool IsLeftOfNode => TextX < NodeX;
    }

    private static List<SvgLabelPlacement> ReadSvgLabelPlacements(string svgContent)
    {
        var doc = XDocument.Parse(svgContent);
        var svgNs = doc.Root?.Name.Namespace ?? XNamespace.None;
        var group = doc.Descendants(svgNs + "g").FirstOrDefault();
        if (group is null)
        {
            return [];
        }

        var result = new List<SvgLabelPlacement>();
        var elements = group.Elements().ToList();
        for (var i = 0; i < elements.Count - 1; i++)
        {
            var ellipse = elements[i];
            var text = elements[i + 1];
            if (ellipse.Name.LocalName != "ellipse" || text.Name.LocalName != "text")
            {
                continue;
            }

            if (!double.TryParse(ellipse.Attribute("cx")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var nodeX) ||
                !double.TryParse(text.Attribute("x")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var textX) ||
                !double.TryParse(text.Attribute("y")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var textY))
            {
                continue;
            }

            var fontSize = double.TryParse(text.Attribute("font-size")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fs)
                ? fs
                : 8.0;
            var label = text.Elements().FirstOrDefault(e => e.Name.LocalName == "tspan")?.Value ?? string.Empty;
            var width = Math.Max(4.0, label.Length * fontSize * 0.56);
            var height = Math.Max(6.0, fontSize + 2.0);

            result.Add(new SvgLabelPlacement(label, nodeX, textX, textY, fontSize, width, height));
        }

        return result;
    }

    private static int CountLabelOverlaps(IReadOnlyList<SvgLabelPlacement> placements)
    {
        var overlaps = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            var a = placements[i];
            var ax1 = a.TextX;
            var ay1 = a.TextY - a.FontSize;
            var ax2 = a.TextX + a.Width;
            var ay2 = a.TextY + (a.FontSize * 0.25);

            for (var j = i + 1; j < placements.Count; j++)
            {
                var b = placements[j];
                var bx1 = b.TextX;
                var by1 = b.TextY - b.FontSize;
                var bx2 = b.TextX + b.Width;
                var by2 = b.TextY + (b.FontSize * 0.25);

                if (ax1 < bx2 && bx1 < ax2 && ay1 < by2 && by1 < ay2)
                {
                    overlaps++;
                }
            }
        }

        return overlaps;
    }

    private static (double Width, double Height) ReadSvgDimensions(string svgContent)
    {
        var doc = XDocument.Parse(svgContent);
        var root = doc.Root ?? throw new InvalidOperationException("SVG root element is missing");
        var width = ParseDimension(root.Attribute("width")?.Value);
        var height = ParseDimension(root.Attribute("height")?.Value);
        return (width, height);
    }

    private static double ParseDimension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var cleaned = value.Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.Parse(cleaned, CultureInfo.InvariantCulture);
    }
}