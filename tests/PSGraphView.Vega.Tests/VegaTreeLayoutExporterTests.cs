using PSGraph;
using PSGraph.Model;
using PSGraphView.Vega;

namespace PSGraphView.Vega.Tests;

public class VegaTreeLayoutExporterTests
{
    private readonly VegaTreeLayoutExporter _exporter = new();

    [Fact]
    public void ExportJson_EmbedsTreeLayoutSpec()
    {
        var graph = CreateTreeGraphView();

        var json = _exporter.Export(graph, VegaExportTypes.JSON);

        Assert.Contains("hierarchical data", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root", json, StringComparison.Ordinal);
        Assert.Contains("child-a", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportHtml_RendersTreeLayoutPage()
    {
        var graph = CreateTreeGraphView();

        var html = _exporter.Export(graph, VegaExportTypes.HTML);

        Assert.Contains("<html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vegaEmbed", html, StringComparison.Ordinal);
        Assert.Contains("root", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportJson_MultipleRootsWithoutVirtualRoot_Throws()
    {
        var graph = CreateForestGraphView();

        var error = Assert.Throws<InvalidOperationException>(() => _exporter.Export(graph, VegaExportTypes.JSON));

        Assert.Contains("multiple roots", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportJson_MultipleRootsWithVirtualRoot_EmbedsVirtualRoot()
    {
        var graph = CreateForestGraphView();

        var json = _exporter.Export(graph, VegaExportTypes.JSON, useVirtualRoot: true);

        Assert.Contains("VirtualRoot Node", json, StringComparison.Ordinal);
    }

    private static GraphView CreateTreeGraphView()
    {
        return new GraphView(
            [
                new GraphViewNode("root", "root", null, new Dictionary<string, object?>()),
                new GraphViewNode("child-a", "child-a", null, new Dictionary<string, object?>()),
                new GraphViewNode("child-b", "child-b", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("root", "child-a", null, 1),
                new GraphViewEdge("root", "child-b", null, 1)
            ]);
    }

    private static GraphView CreateForestGraphView()
    {
        return new GraphView(
            [
                new GraphViewNode("r1", "r1", null, new Dictionary<string, object?>()),
                new GraphViewNode("r2", "r2", null, new Dictionary<string, object?>()),
                new GraphViewNode("leaf", "leaf", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("r1", "leaf", null, 1)
            ]);
    }
}