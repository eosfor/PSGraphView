using PSGraph.Model;
using PSGraphView.Msagl;

namespace PSGraphView.Msagl.Tests;

public class MsaglMdsExporterTests
{
    private readonly MsaglMdsExporter _exporter = new();

    [Fact]
    public void ExportSvg_ReturnsSvgWithClassicNodeLabels()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>()),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("B", "C", null, 1)
            ]);

        var svg = _exporter.Export(graph);

        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">A<", svg, StringComparison.OrdinalIgnoreCase);
    }
}