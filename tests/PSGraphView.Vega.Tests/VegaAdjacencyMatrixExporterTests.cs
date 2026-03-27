using PSGraph;
using PSGraph.Model;
using PSGraphView.Vega;

namespace PSGraphView.Vega.Tests;

public class VegaAdjacencyMatrixExporterTests
{
    private readonly VegaAdjacencyMatrixExporter _exporter = new();

    [Fact]
    public void ExportJson_EmbedsAdjacencyMatrixSpec()
    {
        var graph = CreateGraphView();

        var json = _exporter.Export(graph, VegaExportTypes.JSON);

        Assert.Contains("adjacency matrix", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nodes", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edges", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportHtml_RendersAdjacencyMatrixPage()
    {
        var graph = CreateGraphView();

        var html = _exporter.Export(graph, VegaExportTypes.HTML);

        Assert.Contains("<html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vegaEmbed", html, StringComparison.Ordinal);
        Assert.Contains("adjacency matrix", html, StringComparison.OrdinalIgnoreCase);
    }

    private static GraphView CreateGraphView()
    {
        return new GraphView(
            [
                new GraphViewNode("api", "api", typeof(Uri).FullName, new Dictionary<string, object?> { ["group"] = 2 }),
                new GraphViewNode("db", "db", null, new Dictionary<string, object?>()),
                new GraphViewNode("cache", "cache", null, new Dictionary<string, object?> { ["group"] = 3 })
            ],
            [
                new GraphViewEdge("api", "db", "depends-on", 1),
                new GraphViewEdge("api", "cache", "reads", 1)
            ]);
    }
}