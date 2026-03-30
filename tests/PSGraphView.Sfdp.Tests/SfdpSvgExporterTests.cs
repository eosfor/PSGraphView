using PSGraph.Model;
using PSGraphView.Sfdp;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

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

        Assert.Contains("width=\"300pt\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height=\"200pt\"", svg, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("C", svg, StringComparison.Ordinal);
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
        Assert.Contains("C", svg, StringComparison.Ordinal);
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

        Assert.Contains("id=\"graph0\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"graph\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"node\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title>A</title>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title>A-&gt;B</title>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title>G</title>", svg, StringComparison.OrdinalIgnoreCase);
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
            var renderSceneSeen = false;
            var renderViewportSeen = false;
            var svgStructureSeen = false;

            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name))
                {
                    continue;
                }

                var phaseName = phase.GetString();
                var eventName = name.GetString();
                var data = root.GetProperty("Data");

                if (string.Equals(phaseName, "render", StringComparison.Ordinal) &&
                    string.Equals(eventName, "scene", StringComparison.Ordinal))
                {
                    renderSceneSeen = true;
                    Assert.Equal(2, data.GetProperty("nodeCount").GetInt32());
                    Assert.Equal(1, data.GetProperty("edgeCount").GetInt32());
                    Assert.Equal(0, data.GetProperty("labelCount").GetInt32());
                    continue;
                }

                if (string.Equals(phaseName, "render", StringComparison.Ordinal) &&
                    string.Equals(eventName, "viewport", StringComparison.Ordinal))
                {
                    renderViewportSeen = true;
                    Assert.True(data.GetProperty("outputWidth").GetDouble() > 0.0);
                    Assert.True(data.GetProperty("outputHeight").GetDouble() > 0.0);
                    Assert.True(data.GetProperty("viewBoxWidth").GetDouble() > 0.0);
                    Assert.True(data.GetProperty("viewBoxHeight").GetDouble() > 0.0);
                    continue;
                }

                if (string.Equals(phaseName, "svg", StringComparison.Ordinal) &&
                    string.Equals(eventName, "structure", StringComparison.Ordinal))
                {
                    svgStructureSeen = true;
                    Assert.Equal(2, data.GetProperty("nodeGroupCount").GetInt32());
                    Assert.Equal(1, data.GetProperty("edgeGroupCount").GetInt32());
                    Assert.True(data.GetProperty("graphGroupPresent").GetBoolean());
                    Assert.Equal("graph0", data.GetProperty("graphGroupId").GetString());
                    Assert.Equal(0, data.GetProperty("rectCount").GetInt32());
                    continue;
                }

                if (!string.Equals(phaseName, "svg", StringComparison.Ordinal) ||
                    !string.Equals(eventName, "geometry", StringComparison.Ordinal))
                {
                    continue;
                }

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
            Assert.True(renderSceneSeen);
            Assert.True(renderViewportSeen);
            Assert.True(svgStructureSeen);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Export_WithGraphvizPointViewport_UsesZeroBasedViewBox()
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
            NodeRadius = 0.72,
            OverlapRemovalBoxUnits = SfdpOverlapRemovalBoxUnits.GraphvizPoints,
            OverlapRemovalPadding = 4.0
        });

        Assert.Contains("viewBox=\"0.00 0.00", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("width=\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height=\"", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelfLoopRoute_BoundsExtendBeyondNodeCircle()
    {
        var options = new SfdpOptions
        {
            NodeRadius = 0.72,
            OverlapRemovalBoxUnits = SfdpOverlapRemovalBoxUnits.GraphvizPoints,
            OverlapRemovalPadding = 4.0
        };
        var x = new[] { 4.72 };
        var y = new[] { 4.72 };

        var points = SfdpEdgeRouter.BuildRoutePoints(0, 0, hasReverse: false, x, y, options);
        var bounds = SfdpEdgeRouter.ComputeBounds(points);

        Assert.True(bounds.Width > options.NodeRadius * 2.0);
        Assert.True(bounds.Height > options.NodeRadius * 2.0);
    }

    [Fact]
    public void Prepare_SelfLoop_UsesExpandedViewportAndNegativeLocalYCoordinates()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "A", null, 1)
            ]);
        var options = new SfdpOptions
        {
            NodeRadius = 0.72,
            OverlapRemovalBoxUnits = SfdpOverlapRemovalBoxUnits.GraphvizPoints,
            OverlapRemovalPadding = 4.0,
            ShowArrows = true
        };

        var prepared = SfdpRenderScenePipeline.Prepare(graph, options, SfdpDiagnosticsWriter.Disabled, CancellationToken.None);

        var node = Assert.Single(prepared.Scene.Nodes);
        Assert.True(node.Y < 0.0);
        Assert.True(prepared.Scene.Viewport.ViewBoxWidth > options.NodeRadius * 6.0);
        Assert.True(prepared.Scene.Viewport.ViewBoxHeight > options.NodeRadius * 6.0);
    }

    [Fact]
    public void Export_TriangleCycle_UsesGraphvizLikeNegativeLocalYCoordinates()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>()),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("B", "C", null, 1),
                new GraphViewEdge("C", "A", null, 1)
            ]);

        var svg = _exporter.Export(graph, new SfdpOptions
        {
            NodeRadius = 0.72,
            OverlapRemovalBoxUnits = SfdpOverlapRemovalBoxUnits.GraphvizPoints,
            OverlapRemovalPadding = 4.0
        });

        var document = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var ellipses = document.Descendants(ns + "ellipse").ToArray();

        Assert.NotEmpty(ellipses);
        Assert.All(ellipses, ellipse =>
        {
            var cy = double.Parse(ellipse.Attribute("cy")!.Value, CultureInfo.InvariantCulture);
            Assert.True(cy <= 0.0, $"Expected graph-local cy <= 0, got {cy.ToString(CultureInfo.InvariantCulture)}.");
        });
    }
}
