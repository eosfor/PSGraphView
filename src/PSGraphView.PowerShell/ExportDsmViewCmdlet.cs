using System.Management.Automation;
using PSGraph.DesignStructureMatrix;
using PSGraph.Model;
using PSGraphView.Dsm;
using PSGraphView.Vega;

namespace PSGraphView.PowerShell;

[Cmdlet(VerbsData.Export, "DSMView", DefaultParameterSetName = PlainDsmParameterSet)]
public sealed class ExportDsmViewCmdlet : PSCmdlet
{
    private const string PlainDsmParameterSet = "PlainDsm";
    private const string PartitionedDsmParameterSet = "PartitionedDsm";
    private const string SequencedDsmParameterSet = "SequencedDsm";

    private readonly DsmSvgExporter _dsmSvgExporter = new();
    private readonly DsmVegaMatrixExporter _dsmVegaMatrixExporter = new();

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PlainDsmParameterSet)]
    [ValidateNotNull]
    public IDsm Dsm { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PartitionedDsmParameterSet)]
    [ValidateNotNull]
    public IDsmPartitionResult Result { get; set; } = null!;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = SequencedDsmParameterSet)]
    [ValidateNotNull]
    public IDsm SequencedDsm { get; set; } = null!;

    [Parameter(Mandatory = true)]
    public DsmViewRenderer Renderer { get; set; }

    [Parameter]
    public ViewOutputKind As { get; set; }

    [Parameter]
    public string? Path { get; set; }

    [Parameter]
    [ValidateRange(10, 200)]
    public int ItemSize { get; set; } = 45;

    protected override void ProcessRecord()
    {
        var dsm = ResolveDsm();
        var partitions = ResolvePartitions();
        var outputKind = CmdletOutputHelpers.ResolveOutputKind(
            MyInvocation.BoundParameters,
            nameof(As),
            As,
            Path,
            Renderer == DsmViewRenderer.DsmMatrixSvg ? ViewOutputKind.Svg : ViewOutputKind.Json);

        var result = Renderer switch
        {
            DsmViewRenderer.DsmMatrixSvg => ExportMatrixSvg(dsm, partitions, outputKind),
            DsmViewRenderer.DsmVegaMatrix => ExportVegaMatrix(dsm, partitions, outputKind),
            _ => throw new NotSupportedException($"Renderer '{Renderer}' is not supported.")
        };

        CmdletOutputHelpers.WriteResult(this, result, Path);
    }

    private IDsm ResolveDsm()
    {
        return ParameterSetName switch
        {
            PlainDsmParameterSet => Dsm,
            PartitionedDsmParameterSet => Result.Dsm ?? throw new InvalidOperationException("Partitioning result is missing DSM."),
            SequencedDsmParameterSet => SequencedDsm,
            _ => throw new InvalidOperationException($"Unknown parameter set '{ParameterSetName}'.")
        };
    }

    private IReadOnlyList<IReadOnlyList<PSVertex>>? ResolvePartitions()
    {
        if (ParameterSetName != PartitionedDsmParameterSet)
        {
            return null;
        }

        return Result.Partitions;
    }

    private string ExportMatrixSvg(IDsm dsm, IReadOnlyList<IReadOnlyList<PSVertex>>? partitions, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Svg);
        return _dsmSvgExporter.ExportString(
            dsm.RowIndex,
            dsm.ColIndex,
            dsm.RowIndex.Count,
            dsm.ColIndex.Count,
            (row, column) => dsm.DsmMatrixView[row, column],
            partitions,
            ItemSize);
    }

    private string ExportVegaMatrix(IDsm dsm, IReadOnlyList<IReadOnlyList<PSVertex>>? partitions, ViewOutputKind outputKind)
    {
        CmdletOutputHelpers.ValidateSupportedOutputs(this, Renderer.ToString(), outputKind, ViewOutputKind.Json, ViewOutputKind.Html);
        var orderedVertices = dsm.RowIndex
            .OrderBy(static item => item.Value)
            .Select(static item => item.Key)
            .ToList();

        return _dsmVegaMatrixExporter.Export(
            orderedVertices,
            (row, column) => dsm.DsmMatrixView[row, column],
            CmdletOutputHelpers.ToVegaExportType(outputKind),
            partitions);
    }
}