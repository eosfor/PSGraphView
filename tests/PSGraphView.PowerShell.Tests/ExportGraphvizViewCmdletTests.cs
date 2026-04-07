using PSGraphView.PowerShell;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace PSGraphView.PowerShell.Tests;

public sealed class ExportGraphvizViewCmdletTests : IDisposable
{
    private const string BasicDot = "digraph G { A -> B; }";

    private readonly PowerShellInstance _powerShell;
    private readonly string _tempDirectory;

    public ExportGraphvizViewCmdletTests()
    {
        _powerShell = PowerShellInstance.Create();
        _powerShell.AddCommand("Import-Module")
            .AddParameter("Assembly", typeof(ExportGraphvizViewCmdlet).Assembly);
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

    [GraphvizNativeFact]
    public void ExportGraphvizView_InputObjectSvg_ReturnsSvg()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Sfdp)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<g", svg, StringComparison.OrdinalIgnoreCase);
    }

    [GraphvizNativeFact]
    public void ExportGraphvizView_DotPathSvgOutputPath_WritesSvg()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        var dotPath = System.IO.Path.Combine(_tempDirectory, "graph.dot");
        var svgPath = System.IO.Path.Combine(_tempDirectory, "graph.svg");
        File.WriteAllText(dotPath, BasicDot);

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("DotPath", dotPath)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("OutputPath", svgPath);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(svgPath));
        var svg = File.ReadAllText(svgPath);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportGraphvizView_InputObjectPngOutputPath_WritesPng()
    {
        var pngPath = System.IO.Path.Combine(_tempDirectory, "graph.png");

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("As", ViewOutputKind.Png)
            .AddParameter("OutputPath", pngPath);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.True(File.Exists(pngPath));
        var bytes = File.ReadAllBytes(pngPath);
        Assert.True(bytes.Length >= 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [GraphvizNativeFact]
    public void ExportGraphvizView_InputObjectJson_ReturnsXdotJson()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("As", ViewOutputKind.Json);

        var result = _powerShell.Invoke();

        Assert.Single(result);
        var json = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("\"name\": \"G\"", json, StringComparison.Ordinal);
        Assert.Contains("\"edges\"", json, StringComparison.Ordinal);
    }
}
