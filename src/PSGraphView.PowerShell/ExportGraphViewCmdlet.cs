using System.Management.Automation;
using PSGraph.Model;
using PSGraphView.Msagl;
using PSGraphView.Vega;

namespace PSGraphView.PowerShell;

[Cmdlet(VerbsData.Export, "GraphView")]
public sealed class ExportGraphViewCmdlet : PSCmdlet
{
    private readonly VegaForceDirectedExporter _vegaForceDirectedExporter = new();
    private readonly VegaAdjacencyMatrixExporter _vegaAdjacencyMatrixExporter = new();
    private readonly VegaTreeLayoutExporter _vegaTreeLayoutExporter = new();
    private readonly MsaglMdsExporter _msaglMdsExporter = new();
    private readonly MsaglFastIncrementalExporter _msaglFastIncrementalExporter = new();
    private readonly MsaglSugiyamaExporter _msaglSugiyamaExporter = new();

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PsBidirectionalGraph Graph { get; set; } = null!;

    [Parameter(Mandatory = true)]
    public GraphViewRenderer Renderer { get; set; }

    [Parameter]
    public ViewOutputKind As { get; set; }

    [Parameter]
    public string? Path { get; set; }

    [Parameter]
    public SwitchParameter UseVirtualTreeRoot { get; set; }

    [Parameter]
    public string BackgroundColor { get; set; } = "#ffffff";

    [Parameter]
    public SwitchParameter ShowLabels { get; set; }

    [Parameter]
    public SwitchParameter ShowArrows { get; set; }

    [Parameter]
    [ValidateRange(0.1, 1000.0)]
    public double NodeRadius { get; set; } = 4.0;

    [Parameter]
    [ValidateRange(0.1, 100.0)]
    public double EdgeLineWidth { get; set; } = 0.8;

    [Parameter]
    [ValidateRange(4.0, 64.0)]
    public double LabelFontSize { get; set; } = 8.0;

    [Parameter]
    public string GroupMetadataKey { get; set; } = "group";

    [Parameter]
    public SwitchParameter DisableGroupColors { get; set; }

    [Parameter]
    [ValidateSet("Horizontal", "Vertical")]
    public string SugiyamaDirection { get; set; } = "Horizontal";

    [Parameter]
    [ValidateRange(1.0, 5000.0)]
    public double SugiyamaLayerSeparation { get; set; } = 90.0;

    [Parameter]
    [ValidateRange(0.0, 5000.0)]
    public double SugiyamaNodeSeparation { get; set; } = 24.0;

    [Parameter]
    [ValidateSet("SugiyamaSplines", "Spline", "StraightLine", "Rectilinear", "RectilinearToCenter", "None", "SplineBundling")]
    public string SugiyamaEdgeRouting { get; set; } = "SugiyamaSplines";

    [Parameter]
    [ValidateRange(0.0, 200.0)]
    public double LabelOffsetX { get; set; } = 6.0;

    [Parameter]
    [ValidateRange(-100.0, 100.0)]
    public double LabelOffsetY { get; set; } = 0.0;

    protected override void ProcessRecord()
    {
        var graphView = Graph.ToGraphView();
        var outputKind = CmdletOutputHelpers.ResolveOutputKind(
            MyInvocation.BoundParameters,
            nameof(As),
            As,
            Path,
            Renderer is GraphViewRenderer.MsaglMds or GraphViewRenderer.MsaglFastIncremental or GraphViewRenderer.MsaglSugiyama
                ? ViewOutputKind.Svg
                : ViewOutputKind.Json);

        var result = Renderer switch
        {
            GraphViewRenderer.VegaForceDirected => ExportVegaForceDirected(graphView, outputKind),
            GraphViewRenderer.VegaAdjacencyMatrix => ExportVegaAdjacencyMatrix(graphView, outputKind),
            GraphViewRenderer.VegaTreeLayout => ExportVegaTreeLayout(graphView, outputKind),
            GraphViewRenderer.MsaglMds => ExportMsaglMds(graphView, outputKind),
            GraphViewRenderer.MsaglFastIncremental => ExportMsaglFastIncremental(graphView, outputKind),
            GraphViewRenderer.MsaglSugiyama => ExportMsaglSugiyama(graphView, outputKind),
            _ => throw new NotSupportedException($"Renderer '{Renderer}' is not supported.")
        };

        CmdletOutputHelpers.WriteResult(this, result, Path);
    }

    private string ExportVegaForceDirected(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Json, ViewOutputKind.Html);
        return _vegaForceDirectedExporter.Export(graph, CmdletOutputHelpers.ToVegaExportType(outputKind));
    }

    private string ExportVegaAdjacencyMatrix(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Json, ViewOutputKind.Html);
        return _vegaAdjacencyMatrixExporter.Export(graph, CmdletOutputHelpers.ToVegaExportType(outputKind));
    }

    private string ExportVegaTreeLayout(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Json, ViewOutputKind.Html);
        return _vegaTreeLayoutExporter.Export(graph, CmdletOutputHelpers.ToVegaExportType(outputKind), UseVirtualTreeRoot.IsPresent);
    }

    private string ExportMsaglMds(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Svg);
        return _msaglMdsExporter.Export(graph);
    }

    private string ExportMsaglFastIncremental(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Svg);
        return _msaglFastIncrementalExporter.Export(graph, new MsaglFastIncrementalOptions
        {
            BackgroundColor = BackgroundColor,
            ShowLabels = ShowLabels.IsPresent,
            ShowArrows = ShowArrows.IsPresent,
            NodeRadius = NodeRadius,
            EdgeLineWidth = EdgeLineWidth,
            LabelFontSize = LabelFontSize,
            GroupMetadataKey = GroupMetadataKey,
            DisableGroupColors = DisableGroupColors.IsPresent
        });
    }

    private string ExportMsaglSugiyama(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Svg);
        return _msaglSugiyamaExporter.Export(graph, new MsaglSugiyamaOptions
        {
            BackgroundColor = BackgroundColor,
            ShowLabels = ShowLabels.IsPresent,
            ShowArrows = ShowArrows.IsPresent,
            NodeRadius = NodeRadius,
            EdgeLineWidth = EdgeLineWidth,
            LabelFontSize = LabelFontSize,
            GroupMetadataKey = GroupMetadataKey,
            DisableGroupColors = DisableGroupColors.IsPresent,
            Direction = SugiyamaDirection,
            LayerSeparation = SugiyamaLayerSeparation,
            NodeSeparation = SugiyamaNodeSeparation,
            EdgeRouting = SugiyamaEdgeRouting,
            LabelOffsetX = LabelOffsetX,
            LabelOffsetY = LabelOffsetY
        });
    }
}