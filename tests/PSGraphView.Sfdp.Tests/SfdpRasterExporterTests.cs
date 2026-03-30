using System.Text.Json;
using PSGraph.Model;
using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpRasterExporterTests
{
    private readonly SfdpRasterExporter _exporter = new();

    [Fact]
    public void ExportPng_ReturnsPngBytes()
    {
        var graph = BuildGraph();

        var bytes = _exporter.ExportPng(graph, new SfdpOptions());

        Assert.True(bytes.Length > 8);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void ExportJpg_WithDiagnostics_WritesRasterEvent()
    {
        var graph = BuildGraph();
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-raster.jsonl");

        try
        {
            var bytes = _exporter.ExportJpg(graph, new SfdpOptions
            {
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            Assert.True(bytes.Length > 4);
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xD8, bytes[1]);

            var rasterSeen = false;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!string.Equals(root.GetProperty("Phase").GetString(), "render", StringComparison.Ordinal) ||
                    !string.Equals(root.GetProperty("Name").GetString(), "raster", StringComparison.Ordinal))
                {
                    continue;
                }

                rasterSeen = true;
                var data = root.GetProperty("Data");
                Assert.Equal("Jpg", data.GetProperty("format").GetString());
                Assert.Equal("SkiaSharp", data.GetProperty("backend").GetString());
                Assert.True(data.GetProperty("pixelWidth").GetInt32() > 0);
                Assert.True(data.GetProperty("pixelHeight").GetInt32() > 0);
                Assert.True(data.GetProperty("flattenedForOpaqueOutput").GetBoolean());
            }

            Assert.True(rasterSeen);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    private static GraphView BuildGraph()
    {
        return new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);
    }
}
