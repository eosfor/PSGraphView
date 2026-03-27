using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PSGraph;
using PSGraph.Model;

namespace PSGraphView.Vega;

public sealed class VegaForceDirectedExporter
{
    private const string TemplateName = "vega.force.directed.layout.json";
    private const string HtmlTitle = "PSGraphView Vega";

    public string Export(GraphView graph, VegaExportTypes exportType)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var template = VegaTemplateLoader.LoadTemplate(TemplateName);
        var records = graph.ToForceDirectedRecords();

        var data = template["data"] as JArray
            ?? throw new InvalidOperationException("The Vega template is missing the expected data array.");

        ((JObject?)data[2] ?? throw new InvalidOperationException("The Vega template is missing the expected node dataset."))["values"] = JArray.FromObject(records.Nodes);
        ((JObject?)data[0] ?? throw new InvalidOperationException("The Vega template is missing the expected link dataset."))["values"] = JArray.FromObject(records.Links);

        var json = template.ToString(Formatting.None);

        return exportType switch
        {
            VegaExportTypes.HTML => VegaHtmlPageRenderer.Render(json, HtmlTitle),
            VegaExportTypes.JSON => json,
            _ => throw new NotSupportedException($"Export type '{exportType}' is not supported by the scaffolded PSGraphView Vega project.")
        };
    }
}