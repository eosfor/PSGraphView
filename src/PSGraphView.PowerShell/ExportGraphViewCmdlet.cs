using System.Management.Automation;
using Microsoft.Msagl.Core;
using PSGraph.Model;
using PSGraphView.Msagl;
using PSGraphView.Sfdp;
using PSGraphView.Vega;

namespace PSGraphView.PowerShell;

/// <summary>
/// Exports a graph using one of the PSGraphView renderers.
/// For the managed Sfdp renderer, parameter notes and Graphviz-style examples
/// are documented in the repository README.
/// </summary>
[Cmdlet(VerbsData.Export, "GraphView", DefaultParameterSetName = GraphParameterSet, HelpUri = "https://github.com/eosfor/PSGraphView#export-graphview-sfdp")]
public sealed class ExportGraphViewCmdlet : PSCmdlet
{
    private const string GraphParameterSet = "Graph";
    private const string SfdpParameterSet = "Sfdp";
    private const string MsaglParameterSet = "Msagl";

    private readonly CancelToken _cancelToken = new();
    private readonly VegaForceDirectedExporter _vegaForceDirectedExporter = new();
    private readonly VegaAdjacencyMatrixExporter _vegaAdjacencyMatrixExporter = new();
    private readonly VegaTreeLayoutExporter _vegaTreeLayoutExporter = new();
    private readonly SfdpSvgExporter _sfdpSvgExporter = new();
    private readonly MsaglMdsExporter _msaglMdsExporter = new();
    private readonly MsaglFastIncrementalExporter _msaglFastIncrementalExporter = new();
    private readonly MsaglSugiyamaExporter _msaglSugiyamaExporter = new();

    [Parameter(Mandatory = true, ParameterSetName = GraphParameterSet)]
    [Parameter(Mandatory = true, ParameterSetName = SfdpParameterSet)]
    [Parameter(Mandatory = true, ParameterSetName = MsaglParameterSet)]
    [ValidateNotNull]
    public PsBidirectionalGraph Graph { get; set; } = null!;

    [Parameter(Mandatory = true, ParameterSetName = GraphParameterSet)]
    [Parameter(Mandatory = true, ParameterSetName = SfdpParameterSet)]
    [Parameter(Mandatory = true, ParameterSetName = MsaglParameterSet)]
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

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public string EdgeColor { get; set; } = "#c0c0c0";

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.05, 10.0)]
    public double ArrowSize { get; set; } = 1.0;

    [Parameter]
    [ValidateRange(4.0, 64.0)]
    public double LabelFontSize { get; set; } = 8.0;

    [Parameter]
    [ValidateRange(1.0, 10000.0)]
    public double? Width { get; set; }

    [Parameter]
    [ValidateRange(1.0, 10000.0)]
    public double? Height { get; set; }

    [Parameter]
    public string GroupMetadataKey { get; set; } = "group";

    [Parameter]
    public SwitchParameter DisableGroupColors { get; set; }

    [Parameter(ParameterSetName = MsaglParameterSet)]
    [ValidateSet("Horizontal", "Vertical")]
    public string SugiyamaDirection { get; set; } = "Horizontal";

    [Parameter(ParameterSetName = MsaglParameterSet)]
    [ValidateRange(1.0, 5000.0)]
    public double SugiyamaLayerSeparation { get; set; } = 90.0;

    [Parameter(ParameterSetName = MsaglParameterSet)]
    [ValidateRange(0.0, 5000.0)]
    public double SugiyamaNodeSeparation { get; set; } = 24.0;

    [Parameter(ParameterSetName = MsaglParameterSet)]
    [ValidateSet("SugiyamaSplines", "Spline", "StraightLine", "Rectilinear", "RectilinearToCenter", "None", "SplineBundling")]
    public string SugiyamaEdgeRouting { get; set; } = "SugiyamaSplines";

    [Parameter]
    [ValidateRange(0.0, 200.0)]
    public double LabelOffsetX { get; set; } = 6.0;

    [Parameter]
    [ValidateRange(-100.0, 100.0)]
    public double LabelOffsetY { get; set; } = 0.0;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public int? SfdpSeed { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, 100000)]
    public int SfdpMaxIterations { get; set; } = 500;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.0001, 10.0)]
    public double SfdpInitialStep { get; set; } = 0.1;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.000001, 1.0)]
    public double SfdpTolerance { get; set; } = 0.001;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpAdaptiveCooling { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpBarnesHut { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SfdpQuadtreeMode SfdpQuadtreeMode { get; set; } = SfdpQuadtreeMode.Normal;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(2, 100000)]
    public int SfdpBarnesHutThreshold { get; set; } = 45;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.1, 2.0)]
    public double SfdpBarnesHutTheta { get; set; } = 0.6;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, int.MaxValue)]
    public int SfdpQuadtreeHybridThreshold { get; set; } = 10000;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, 64)]
    public int SfdpQuadtreeMaxDepth { get; set; } = 10;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpMultilevel { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(2, 100000)]
    public int SfdpMultilevelThreshold { get; set; } = 4;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, int.MaxValue)]
    public int SfdpMaxMultilevelDepth { get; set; } = int.MaxValue;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, 100000)]
    public int SfdpRefinementIterations { get; set; } = 100;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.0001, 100000.0)]
    public double? SfdpNaturalLength { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(-10.0, -0.000001)]
    public double? SfdpRepulsiveExponent { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SfdpSmoothingMode SfdpSmoothing { get; set; } = SfdpSmoothingMode.None;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, 100000)]
    public int SfdpSmoothingIterations { get; set; } = 50;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpPrincipalComponentRotation { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(-360.0, 360.0)]
    public double SfdpRotationDegrees { get; set; } = 0.0;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpOverlapRemoval { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(1, 100000)]
    public int SfdpOverlapRemovalIterations { get; set; } = 32;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.0, 1000.0)]
    public double SfdpOverlapRemovalPadding { get; set; } = 1.0;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SfdpOverlapRemovalBoxUnits SfdpOverlapRemovalBoxUnits { get; set; } = SfdpOverlapRemovalBoxUnits.OutputUnits;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.0, 100000.0)]
    public double? SfdpOverlapRemovalHalfWidth { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateRange(0.0, 100000.0)]
    public double? SfdpOverlapRemovalHalfHeight { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    [ValidateNotNullOrEmpty]
    public string? SfdpDiagnosticsPath { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SfdpDiagnosticFormat SfdpDiagnosticsFormat { get; set; } = SfdpDiagnosticFormat.JsonLines;

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter DisableSfdpDiagnosticsIterations { get; set; }

    [Parameter(ParameterSetName = SfdpParameterSet)]
    public SwitchParameter SfdpDiagnosticsIncludeCoordinates { get; set; }

    protected override void ProcessRecord()
    {
        try
        {
            ValidateRendererParameterSetCompatibility();

            var graphView = Graph.ToGraphView();
            var outputKind = CmdletOutputHelpers.ResolveOutputKind(
                MyInvocation.BoundParameters,
                nameof(As),
                As,
                Path,
                Renderer is GraphViewRenderer.Sfdp or GraphViewRenderer.MsaglMds or GraphViewRenderer.MsaglFastIncremental or GraphViewRenderer.MsaglSugiyama
                    ? ViewOutputKind.Svg
                    : ViewOutputKind.Json);

            var result = Renderer switch
            {
                GraphViewRenderer.VegaForceDirected => ExportVegaForceDirected(graphView, outputKind),
                GraphViewRenderer.VegaAdjacencyMatrix => ExportVegaAdjacencyMatrix(graphView, outputKind),
                GraphViewRenderer.VegaTreeLayout => ExportVegaTreeLayout(graphView, outputKind),
                GraphViewRenderer.Sfdp => ExportSfdp(graphView, outputKind),
                GraphViewRenderer.MsaglMds => ExportMsaglMds(graphView, outputKind),
                GraphViewRenderer.MsaglFastIncremental => ExportMsaglFastIncremental(graphView, outputKind),
                GraphViewRenderer.MsaglSugiyama => ExportMsaglSugiyama(graphView, outputKind),
                _ => throw new NotSupportedException($"Renderer '{Renderer}' is not supported.")
            };

            CmdletOutputHelpers.WriteResult(this, result, Path);
        }
        catch (OperationCanceledException ex)
        {
            ThrowTerminatingError(new ErrorRecord(
                ex,
                "PSGraphView.LayoutCanceled",
                ErrorCategory.OperationStopped,
                Graph));
        }
    }

    private void ValidateRendererParameterSetCompatibility()
    {
        switch (ParameterSetName)
        {
            case SfdpParameterSet when Renderer != GraphViewRenderer.Sfdp:
                throw new PSArgumentException(
                    $"Sfdp-specific parameters can only be used with renderer '{GraphViewRenderer.Sfdp}'. Received '{Renderer}'.",
                    nameof(Renderer));
            case MsaglParameterSet when !IsMsaglRenderer(Renderer):
                throw new PSArgumentException(
                    "MSAGL-specific parameters can only be used with MSAGL renderers.",
                    nameof(Renderer));
        }

        if (Renderer != GraphViewRenderer.MsaglSugiyama &&
            HasAnyBound(nameof(SugiyamaDirection), nameof(SugiyamaLayerSeparation), nameof(SugiyamaNodeSeparation), nameof(SugiyamaEdgeRouting)))
        {
            throw new PSArgumentException(
                $"Sugiyama-specific parameters can only be used with renderer '{GraphViewRenderer.MsaglSugiyama}'. Received '{Renderer}'.",
                nameof(Renderer));
        }
    }

    private bool HasAnyBound(params string[] parameterNames)
    {
        return parameterNames.Any(MyInvocation.BoundParameters.ContainsKey);
    }

    private static bool IsMsaglRenderer(GraphViewRenderer renderer)
    {
        return renderer is GraphViewRenderer.MsaglMds or GraphViewRenderer.MsaglFastIncremental or GraphViewRenderer.MsaglSugiyama;
    }

    protected override void StopProcessing()
    {
        _cancelToken.Canceled = true;
        base.StopProcessing();
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
        return _msaglMdsExporter.Export(graph, Width, Height, _cancelToken);
    }

    private string ExportSfdp(GraphView graph, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Svg);
        if (_cancelToken.Canceled)
        {
            throw new OperationCanceledException();
        }

        return _sfdpSvgExporter.Export(graph, new SfdpOptions
        {
            Seed = SfdpSeed,
            MaxIterations = SfdpMaxIterations,
            InitialStep = SfdpInitialStep,
            Tolerance = SfdpTolerance,
            AdaptiveCooling = !DisableSfdpAdaptiveCooling.IsPresent,
            UseBarnesHut = !DisableSfdpBarnesHut.IsPresent,
            QuadtreeMode = DisableSfdpBarnesHut.IsPresent ? SfdpQuadtreeMode.None : SfdpQuadtreeMode,
            BarnesHutThreshold = SfdpBarnesHutThreshold,
            BarnesHutTheta = SfdpBarnesHutTheta,
            QuadtreeHybridThreshold = SfdpQuadtreeHybridThreshold,
            QuadtreeMaxDepth = SfdpQuadtreeMaxDepth,
            EnableMultilevel = !DisableSfdpMultilevel.IsPresent,
            MultilevelThreshold = SfdpMultilevelThreshold,
            MaxMultilevelDepth = SfdpMaxMultilevelDepth,
            RefinementIterations = SfdpRefinementIterations,
            NaturalLength = SfdpNaturalLength,
            RepulsiveExponent = SfdpRepulsiveExponent,
            Smoothing = SfdpSmoothing,
            SmoothingIterations = SfdpSmoothingIterations,
            ApplyPrincipalComponentRotation = !DisableSfdpPrincipalComponentRotation.IsPresent,
            RotationDegrees = SfdpRotationDegrees,
            EnableOverlapRemoval = !DisableSfdpOverlapRemoval.IsPresent,
            OverlapRemovalIterations = SfdpOverlapRemovalIterations,
            OverlapRemovalPadding = SfdpOverlapRemovalPadding,
            OverlapRemovalBoxUnits = SfdpOverlapRemovalBoxUnits,
            OverlapRemovalHalfWidth = SfdpOverlapRemovalHalfWidth,
            OverlapRemovalHalfHeight = SfdpOverlapRemovalHalfHeight,
            BackgroundColor = BackgroundColor,
            ShowLabels = ShowLabels.IsPresent,
            ShowArrows = ShowArrows.IsPresent,
            NodeRadius = NodeRadius,
            EdgeLineWidth = EdgeLineWidth,
            EdgeColor = EdgeColor,
            ArrowSize = ArrowSize,
            LabelFontSize = LabelFontSize,
            LabelOffsetX = LabelOffsetX,
            LabelOffsetY = LabelOffsetY,
            GroupMetadataKey = GroupMetadataKey,
            DisableGroupColors = DisableGroupColors.IsPresent,
            Width = Width,
            Height = Height,
            Diagnostics = string.IsNullOrWhiteSpace(SfdpDiagnosticsPath)
                ? null
                : new SfdpDiagnosticsOptions
                {
                    Path = SfdpDiagnosticsPath,
                    Format = SfdpDiagnosticsFormat,
                    IncludeIterations = !DisableSfdpDiagnosticsIterations.IsPresent,
                    IncludeCoordinates = SfdpDiagnosticsIncludeCoordinates.IsPresent
                }
        });
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
            DisableGroupColors = DisableGroupColors.IsPresent,
            Width = Width,
            Height = Height
        }, _cancelToken);
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
            LabelOffsetY = LabelOffsetY,
            Width = Width,
            Height = Height
        }, _cancelToken);
    }
}
