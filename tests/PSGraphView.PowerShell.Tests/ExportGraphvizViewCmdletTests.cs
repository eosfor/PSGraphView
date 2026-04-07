using PSGraphView.PowerShell;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace PSGraphView.PowerShell.Tests;

public sealed class ExportGraphvizViewCmdletTests : IDisposable
{
    private const string BasicDot = "digraph G { A -> B; }";
    private const string GraphvizDotPathEnvironmentVariable = "PSGRAPHVIEW_GRAPHVIZ_DOT_PATH";

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
        Assert.Contains("<ellipse", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<path", svg, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("<ellipse", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<path", svg, StringComparison.OrdinalIgnoreCase);
    }

    [GraphvizNativeFact]
    public void ExportGraphvizView_InputObjectSvg_DoesNotRequireDotExecutable()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        var missingDotPath = System.IO.Path.Combine(_tempDirectory, "missing-dot");
        using var _ = new EnvironmentVariableScope(GraphvizDotPathEnvironmentVariable, missingDotPath);

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("As", ViewOutputKind.Svg);

        var result = _powerShell.Invoke();

        Assert.False(_powerShell.HadErrors);
        Assert.Empty(_powerShell.Streams.Error);
        Assert.Single(result);
        var svg = Assert.IsType<string>(result[0].BaseObject);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ellipse", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<path", svg, StringComparison.OrdinalIgnoreCase);
    }

    [GraphvizNativeFact]
    public void ExportGraphvizView_InputObjectPngOutputPath_WritesPng()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        var missingDotPath = System.IO.Path.Combine(_tempDirectory, "missing-dot");
        using var _ = new EnvironmentVariableScope(GraphvizDotPathEnvironmentVariable, missingDotPath);

        var pngPath = System.IO.Path.Combine(_tempDirectory, "graph.png");

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("As", ViewOutputKind.Png)
            .AddParameter("OutputPath", pngPath);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.False(_powerShell.HadErrors);
        Assert.Empty(_powerShell.Streams.Error);
        Assert.True(File.Exists(pngPath));
        var bytes = File.ReadAllBytes(pngPath);
        Assert.True(bytes.Length >= 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [GraphvizNativeFact]
    public void ExportGraphvizView_InputObjectJpgOutputPath_WritesJpg()
    {
        GraphvizNativeTestEnvironment.EnsureNativeLibraryAvailable();

        var missingDotPath = System.IO.Path.Combine(_tempDirectory, "missing-dot");
        using var _ = new EnvironmentVariableScope(GraphvizDotPathEnvironmentVariable, missingDotPath);

        var jpgPath = System.IO.Path.Combine(_tempDirectory, "graph.jpg");

        _powerShell.AddCommand("Export-GraphvizView")
            .AddParameter("InputObject", BasicDot)
            .AddParameter("Renderer", GraphvizLayoutEngine.Dot)
            .AddParameter("As", ViewOutputKind.Jpg)
            .AddParameter("OutputPath", jpgPath);

        var result = _powerShell.Invoke();

        Assert.Empty(result);
        Assert.False(_powerShell.HadErrors);
        Assert.Empty(_powerShell.Streams.Error);
        Assert.True(File.Exists(jpgPath));
        var bytes = File.ReadAllBytes(jpgPath);
        Assert.True(bytes.Length >= 4);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
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
        Assert.Contains("\"_draw_\"", json, StringComparison.Ordinal);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
