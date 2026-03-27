using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PSGraph;
using PSGraph.Model;

namespace PSGraphView.Vega;

public sealed class VegaAdjacencyMatrixExporter
{
    private const string TemplateName = "vega.adj.matrix.json";
    private const string HtmlTitle = "PSGraphView Vega Adjacency Matrix";

    public string Export(GraphView graph, VegaExportTypes exportType)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var template = VegaTemplateLoader.LoadTemplate(TemplateName);
        var records = graph.ToForceDirectedRecords();

        var data = template["data"] as JArray
            ?? throw new InvalidOperationException("The Vega template is missing the expected data array.");

        ((JObject?)data[0] ?? throw new InvalidOperationException("The Vega template is missing the expected node dataset."))["values"] = JArray.FromObject(records.Nodes);
        ((JObject?)data[1] ?? throw new InvalidOperationException("The Vega template is missing the expected edge dataset."))["values"] = JArray.FromObject(records.Links);

        var json = template.ToString(Formatting.None);

        return exportType switch
        {
            VegaExportTypes.HTML => VegaHtmlPageRenderer.Render(json, HtmlTitle),
            VegaExportTypes.JSON => json,
            _ => throw new NotSupportedException($"Export type '{exportType}' is not supported by the scaffolded adjacency-matrix exporter.")
        };
    }
}