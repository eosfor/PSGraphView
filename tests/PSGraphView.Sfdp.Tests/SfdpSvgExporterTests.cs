using PSGraph.Model;
using PSGraphView.Sfdp;
using System.Text.Json;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpSvgExporterTests
{
    private readonly SfdpSvgExporter _exporter = new();

    [Fact]
    public void Export_ReturnsSvgWithLabels()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions { ShowLabels = true });

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">A<", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">B<", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_AppliesRequestedDimensions()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions { Width = 300, Height = 200 });

        Assert.Contains("width=\"300\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height=\"200\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("viewBox=", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_BidirectionalEdges_UsesPathRouting()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("B", "A", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions { ShowArrows = true });

        Assert.Contains("<path", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("marker-end=", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" C ", svg, StringComparison.Ordinal);
        Assert.Contains("class=\"edge\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_SelfLoop_UsesCurvedPath()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "A", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions { ShowArrows = true });

        Assert.Contains("<path", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" C ", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_AddsGraphvizLikeNodeMetadata()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions());

        Assert.Contains("class=\"node\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title>A</title>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title>A-&gt;B</title>", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_UsesConfiguredEdgeColorAndArrowSize()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions
        {
            ShowArrows = true,
            EdgeColor = "#00000018",
            ArrowSize = 0.08
        });

        Assert.Contains("stroke=\"#00000018\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fill=\"#00000018\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("markerWidth=\"0.64\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_WithDiagnostics_WritesSvgGeometryEvents()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-svg-geometry.jsonl");

        try
        {
            _exporter.Export(graph, new SfdpOptions
            {
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var exportInputSeen = false;
            var viewBoxSeen = false;

            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !string.Equals(phase.GetString(), "svg", StringComparison.Ordinal) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !string.Equals(name.GetString(), "geometry", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = root.GetProperty("Data");
                var stage = data.GetProperty("stage").GetString();
                switch (stage)
                {
                    case "export_input":
                        exportInputSeen = true;
                        Assert.True(data.GetProperty("width").GetDouble() > 0.0);
                        break;
                    case "viewbox":
                        viewBoxSeen = true;
                        Assert.True(data.GetProperty("outputWidth").GetDouble() > 0.0);
                        Assert.True(data.GetProperty("outputHeight").GetDouble() > 0.0);
                        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("viewBox").GetString()));
                        break;
                }
            }

            Assert.True(exportInputSeen);
            Assert.True(viewBoxSeen);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }
}
