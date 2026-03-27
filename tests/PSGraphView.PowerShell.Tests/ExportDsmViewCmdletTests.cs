using System.Management.Automation;
using PSGraph.DesignStructureMatrix;
using PSGraph.Model;
using PSGraphView.PowerShell;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace PSGraphView.PowerShell.Tests;

public sealed class ExportDsmViewCmdletTests : IDisposable
{
    private readonly PowerShellInstance _powerShell;
    private readonly string _tempDirectory;

    public ExportDsmViewCmdletTests()
    {
        _powerShell = PowerShellInstance.Create();
        _powerShell.AddCommand("Import-Module")
            .AddParameter("Assembly", typeof(ExportDsmViewCmdlet).Assembly);
        _powerShell.Invoke();
        _powerShell.Commands.Clear();

        _tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        _powerShell.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void ExportDsmView_DsmVegaMatrixJson_ReturnsSpec()
    {
        var dsm = BuildDsm(("A", "B"), ("B", "A"), ("B", "C"));

        _powerShell.AddCommand("Export-DSMView")
            .AddParameter("Dsm", dsm)
            .AddParameter("Renderer", DsmViewRenderer.DsmVegaMatrix)
            .AddParameter("As", ViewOutputKind.Json);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var json = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("nodes", json, StringComparison.Ordinal);
        Assert.Contains("edges", json, StringComparison.Ordinal);
        Assert.Contains("A", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDsmView_DsmMatrixSvg_ReturnsSvg()
    {
        var dsm = BuildDsm(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-DSMView")
            .AddParameter("Dsm", dsm)
            .AddParameter("Renderer", DsmViewRenderer.DsmMatrixSvg)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportDsmView_PartitioningResult_UsesPartitionsInVegaPayload()
    {
        var dsm = BuildDsm(("A", "B"), ("B", "A"), ("B", "C"));
        var ordered = dsm.RowIndex.OrderBy(item => item.Value).Select(item => item.Key).ToList();
        var resultObject = new StubPartitionResult(
            dsm,
            [
                [ordered[0], ordered[1]],
                [ordered[2]]
            ]);

        _powerShell.AddCommand("Export-DSMView")
            .AddParameter("Result", resultObject)
            .AddParameter("Renderer", DsmViewRenderer.DsmVegaMatrix)
            .AddParameter("As", ViewOutputKind.Json);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var json = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("\"group\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"group\":2", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportDsmView_DsmMatrixSvgPath_InfersSvgOutput()
    {
        var dsm = BuildDsm(("A", "B"), ("B", "C"));
        var path = System.IO.Path.Combine(_tempDirectory, "matrix.svg");

        _powerShell.AddCommand("Export-DSMView")
            .AddParameter("Dsm", dsm)
            .AddParameter("Renderer", DsmViewRenderer.DsmMatrixSvg)
            .AddParameter("Path", path);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(path));
        var svg = File.ReadAllText(path);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    private static IDsm BuildDsm(params (string from, string to)[] edges)
    {
        var graph = new PsBidirectionalGraph();
        foreach (var (from, to) in edges)
        {
            var source = graph.Vertices.FirstOrDefault(vertex => vertex.Label == from) ?? new PSVertex(from);
            var target = graph.Vertices.FirstOrDefault(vertex => vertex.Label == to) ?? new PSVertex(to);
            graph.AddEdge(new PSEdge(source, target));
        }

        return new DsmClassic(graph);
    }

    private sealed class StubPartitionResult : IDsmPartitionResult
    {
        public StubPartitionResult(IDsm dsm, IReadOnlyList<IReadOnlyList<PSVertex>> partitions)
        {
            Dsm = dsm;
            Partitions = partitions;
        }

        public IDsm Dsm { get; }

        public IReadOnlyList<IReadOnlyList<PSVertex>> Partitions { get; }
    }

}