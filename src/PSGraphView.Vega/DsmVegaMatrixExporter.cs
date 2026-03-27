using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PSGraph;
using PSGraph.Model;

namespace PSGraphView.Vega;

public sealed class DsmVegaMatrixExporter
{
    private const string TemplateName = "vega.dsm.matrix.json";
    private const string HtmlTitle = "PSGraphView Vega DSM Matrix";
    private readonly DsmNodeAndEdgeViewBuilder _builder = new();

    public string Export(
        IReadOnlyList<PSVertex> orderedVertices,
        Func<int, int, double> getMatrixValue,
        VegaExportTypes exportType,
        IReadOnlyList<IReadOnlyList<PSVertex>>? partitions = null)
    {
        ArgumentNullException.ThrowIfNull(orderedVertices);
        ArgumentNullException.ThrowIfNull(getMatrixValue);

        var template = VegaTemplateLoader.LoadTemplate(TemplateName);
        var data = template["data"] as JArray
            ?? throw new InvalidOperationException("The Vega template is missing the expected data array.");

        var payload = _builder.Build(orderedVertices, getMatrixValue, partitions);
        ReplaceNamedValues(data, "nodes", payload["nodes"]?["values"]);
        ReplaceNamedValues(data, "edges", payload["edges"]?["values"]);

        var json = template.ToString(Formatting.None);
        return exportType switch
        {
            VegaExportTypes.HTML => VegaHtmlPageRenderer.Render(json, HtmlTitle),
            VegaExportTypes.JSON => json,
            _ => throw new NotSupportedException($"Export type '{exportType}' is not supported by the DSM matrix exporter.")
        };
    }

    private static void ReplaceNamedValues(JArray dataSets, string name, JToken? values)
    {
        var dataSet = dataSets
            .OfType<JObject>()
            .FirstOrDefault(item => string.Equals((string?)item["name"], name, StringComparison.Ordinal));

        if (dataSet is null)
        {
            throw new InvalidOperationException($"The Vega template is missing the expected '{name}' dataset.");
        }

        dataSet["values"] = values?.DeepClone()
            ?? throw new InvalidOperationException($"The DSM payload is missing the expected '{name}' values.");
    }
}