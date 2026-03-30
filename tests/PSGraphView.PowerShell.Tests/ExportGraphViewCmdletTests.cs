using System.Management.Automation;
using PSGraph.Model;
using PSGraphView.PowerShell;
using PSGraphView.Sfdp;
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
    public void ExportGraphView_SfdpSvg_ReturnsSvg()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportGraphView_SfdpPng_ReturnsBytes()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("As", ViewOutputKind.Png);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var bytes = Assert.IsType<byte[]>(result[0].BaseObject);
        Assert.True(bytes.Length > 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void ExportGraphView_SfdpPath_InfersPngOutput()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));
        var path = System.IO.Path.Combine(_tempDirectory, "graph.png");

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("Path", path);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void ExportGraphView_SfdpJpgPath_WritesJpeg()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));
        var path = System.IO.Path.Combine(_tempDirectory, "graph.jpg");

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("As", ViewOutputKind.Jpg)
            .AddParameter("Path", path);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 4);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void ExportGraphView_SfdpSvg_AcceptsAlgorithmParameters()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"), ("C", "D"), ("D", "A"));
        var diagnosticsPath = System.IO.Path.Combine(_tempDirectory, "sfdp-diagnostics.jsonl");

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("As", ViewOutputKind.Svg)
            .AddParameter("SfdpSeed", 42)
            .AddParameter("SfdpMaxIterations", 40)
            .AddParameter("SfdpInitialStep", 0.2d)
            .AddParameter("SfdpTolerance", 0.0005d)
            .AddParameter("DisableSfdpBarnesHut", true)
            .AddParameter("SfdpQuadtreeMode", SfdpQuadtreeMode.Fast)
            .AddParameter("SfdpQuadtreeHybridThreshold", 500)
            .AddParameter("SfdpQuadtreeMaxDepth", 8)
            .AddParameter("DisableSfdpMultilevel", true)
            .AddParameter("DisableSfdpOverlapRemoval", true)
            .AddParameter("DisableSfdpPrincipalComponentRotation", true)
            .AddParameter("SfdpOverlapRemovalIterations", 5)
            .AddParameter("SfdpOverlapRemovalPadding", 2d)
            .AddParameter("SfdpOverlapRemovalBoxUnits", SfdpOverlapRemovalBoxUnits.GraphvizPoints)
            .AddParameter("SfdpOverlapRemovalHalfWidth", 1.5d)
            .AddParameter("SfdpOverlapRemovalHalfHeight", 2.5d)
            .AddParameter("SfdpNaturalLength", 12d)
            .AddParameter("SfdpRepulsiveExponent", -1.2d)
            .AddParameter("SfdpSmoothing", SfdpSmoothingMode.Spring)
            .AddParameter("SfdpSmoothingIterations", 10)
            .AddParameter("SfdpRotationDegrees", 15d)
            .AddParameter("EdgeColor", "#00000018")
            .AddParameter("ArrowSize", 0.08d)
            .AddParameter("SfdpDiagnosticsPath", diagnosticsPath)
            .AddParameter("DisableSfdpDiagnosticsIterations", true)
            .AddParameter("ShowArrows", true);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stroke=\"#00000018\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(diagnosticsPath));
        var diagnostics = File.ReadAllText(diagnosticsPath);
        Assert.Contains("\"Phase\":\"layout\"", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"overlapRemovalBoxUnits\":\"GraphvizPoints\"", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"overlapRemovalHalfWidth\":1.5", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"overlapRemovalHalfHeight\":2.5", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"quadtreeMode\":\"None\"", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"quadtreeHybridThreshold\":500", diagnostics, StringComparison.Ordinal);
        Assert.Contains("\"quadtreeMaxDepth\":8", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphView_SfdpParameters_WithMsaglRenderer_ReturnsError()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.MsaglMds)
            .AddParameter("As", ViewOutputKind.Svg)
            .AddParameter("SfdpSeed", 42);

        var error = Assert.Throws<CmdletInvocationException>(() => _powerShell.Invoke());
        Assert.Contains("Sfdp-specific parameters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphView_MsaglParameters_WithSfdpRenderer_ReturnsError()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.Sfdp)
            .AddParameter("As", ViewOutputKind.Svg)
            .AddParameter("SugiyamaDirection", "Vertical");

        var error = Assert.Throws<CmdletInvocationException>(() => _powerShell.Invoke());
        Assert.Contains("MSAGL-specific parameters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphView_SugiyamaParameters_WithNonSugiyamaRenderer_ReturnsError()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.MsaglMds)
            .AddParameter("As", ViewOutputKind.Svg)
            .AddParameter("SugiyamaNodeSeparation", 10d);

        var error = Assert.Throws<CmdletInvocationException>(() => _powerShell.Invoke());
        Assert.Contains("Sugiyama-specific parameters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportGraphView_MsaglSugiyamaSvg_AppliesRequestedDimensions()
    {
        var graph = BuildGraph(("A", "B"), ("B", "C"));

        _powerShell.AddCommand("Export-GraphView")
            .AddParameter("Graph", graph)
            .AddParameter("Renderer", GraphViewRenderer.MsaglSugiyama)
            .AddParameter("As", ViewOutputKind.Svg)
            .AddParameter("Width", 300d)
            .AddParameter("Height", 300d);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("width=\"300\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("height=\"300\"", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("viewBox=", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stroke-width=\"300\"", svg, StringComparison.OrdinalIgnoreCase);
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
