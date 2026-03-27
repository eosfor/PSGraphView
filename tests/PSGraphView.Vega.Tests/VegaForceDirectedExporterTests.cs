using PSGraph;
using PSGraph.Model;
using PSGraphView.Vega;

namespace PSGraphView.Vega.Tests;

public class VegaForceDirectedExporterTests
{
    private readonly VegaForceDirectedExporter _exporter = new();

    [Fact]
    public void ExportJson_EmbedsForceDirectedSpec()
    {
        var graph = CreateGraphView();

        var json = _exporter.Export(graph, VegaExportTypes.JSON);

        Assert.Contains("force", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("values", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportHtml_RendersEmbeddablePage()
    {
        var graph = CreateGraphView();

        var html = _exporter.Export(graph, VegaExportTypes.HTML);

        Assert.Contains("<html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vegaEmbed", html, StringComparison.Ordinal);
        Assert.Contains("api", html, StringComparison.Ordinal);
    }

    private static GraphView CreateGraphView()
    {
        return new GraphView(
            [
                new GraphViewNode("api", "api", typeof(Uri).FullName, new Dictionary<string, object?> { ["group"] = 2 }),
                new GraphViewNode("db", "db", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("api", "db", "depends-on", 1)
            ]);
    }
}