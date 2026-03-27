using PSGraph.Model;
using PSGraphView.Msagl;

namespace PSGraphView.Msagl.Tests;

public class MsaglFastIncrementalExporterTests
{
    private readonly MsaglFastIncrementalExporter _exporter = new();

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
    public void ExportSvg_RespectsLabelAndBackgroundOptions()
    {
        var graph = CreateGraphView();

        var svg = _exporter.Export(graph, new MsaglFastIncrementalOptions
        {
            BackgroundColor = "#000000",
            ShowLabels = true,
            ShowArrows = true,
            LabelFontSize = 7.0
        });

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fill=\"#000000\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<text", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportSvg_AcceptsCustomForceLayoutOptions()
    {
        var graph = CreateGraphView();

        var svg = _exporter.Export(graph, new MsaglFastIncrementalOptions
        {
            RepulsiveForceConstant = 9.0,
            AttractiveForceConstant = 0.045,
            AttractiveInterClusterForceConstant = 0.08,
            GravityConstant = 0.2,
            NodeSeparation = 8.0,
            MaxIterations = 450,
            MinorIterations = 8,
            ProjectionIterations = 16
        });

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
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
}