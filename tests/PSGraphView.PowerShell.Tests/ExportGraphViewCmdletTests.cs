using System.Management.Automation;
using PSGraph.Model;
using PSGraphView.PowerShell;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace PSGraphView.PowerShell.Tests;

public sealed class ExportGraphViewCmdletTests : IDisposable
{
    private readonly PowerShellInstance _powerShell;
    private readonly string _tempDirectory;

    public ExportGraphViewCmdletTests()
    {
        _powerShell = PowerShellInstance.Create();
        _powerShell.AddCommand("Import-Module")
            .AddParameter("Assembly", typeof(ExportGraphViewCmdlet).Assembly);
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
    public void ExportGraphView_VegaForceDirectedJson_ReturnsSpec()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.VegaForceDirected)
            .AddParameter("As", ViewOutputKind.Json);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var json = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("force", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphView_MsaglFastIncrementalSvg_ReturnsSvg()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.MsaglFastIncremental)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportGraphView_MsaglMdsSvg_ReturnsSvg()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.MsaglMds)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportGraphView_VegaForceDirectedPath_InfersHtmlOutput()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));
        var path = System.IO.Path.Combine(_tempDirectory, "graph.html");

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.VegaForceDirected)
            .AddParameter("Path", path);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(path));
        var html = File.ReadAllText(path);
        Assert.Contains("vegaEmbed", html, StringComparison.Ordinal);
    }

    private static PsBidirectionalGraph BuildGraph(params (string from, string to)[] edges)
    {
        var graph = new PsBidirectionalGraph();
        foreach (var (from, to) in edges)
        {
            var source = graph.Vertices.FirstOrDefault(vertex => vertex.Label == from) ?? new PSVertex(from);
            var target = graph.Vertices.FirstOrDefault(vertex => vertex.Label == to) ?? new PSVertex(to);
            graph.AddEdge(new PSEdge(source, target));
        }

        return graph;
    }
}