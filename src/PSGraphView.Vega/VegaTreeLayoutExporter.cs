using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PSGraph;
using PSGraph.Model;

namespace PSGraphView.Vega;

public sealed class VegaTreeLayoutExporter
{
    private const string TemplateName = "vega.tree.layout.json";
    private const string HtmlTitle = "PSGraphView Vega Tree Layout";

    public string Export(GraphView graph, VegaExportTypes exportType, bool useVirtualRoot = false)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var template = VegaTemplateLoader.LoadTemplate(TemplateName);
        var records = graph.ToTreeLayoutRecords(useVirtualRoot);

        var data = template["data"] as JArray
            ?? throw new InvalidOperationException("The Vega template is missing the expected data array.");

        ((JObject?)data[0] ?? throw new InvalidOperationException("The Vega template is missing the expected tree dataset."))["values"] = JArray.FromObject(records);

        var json = template.ToString(Formatting.None);

        return exportType switch
        {
            VegaExportTypes.HTML => VegaHtmlPageRenderer.Render(json, HtmlTitle),
            VegaExportTypes.JSON => json,
            _ => throw new NotSupportedException($"Export type '{exportType}' is not supported by the scaffolded tree-layout exporter.")
        };
    }
}