using System.Text.Json;
using PSGraph.Model;
using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpRasterExporterTests
{
    private readonly SfdpRasterExporter _exporter = new();
    private readonly SfdpExportBundleExporter _bundleExporter = new();

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
                Assert.Equal(90, data.GetProperty("encodeQuality").GetInt32());
                Assert.Equal("GraphvizLikeGdThreshold", data.GetProperty("opaqueOutputPolicy").GetString());
                Assert.Equal("#fffffeff", data.GetProperty("opaqueFallbackColor").GetString());
                Assert.Equal(64, data.GetProperty("opaqueAlphaThreshold").GetInt32());
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

    [Fact]
    public void ExportBundle_WritesAllFormatsAndSeparateDiagnostics()
    {
        var graph = BuildGraph();
        var tempDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-bundle");
        var svgDiagnosticsPath = Path.Combine(tempDir, "managed-svg.jsonl");
        var pngDiagnosticsPath = Path.Combine(tempDir, "managed-png.jsonl");
        var jpgDiagnosticsPath = Path.Combine(tempDir, "managed-jpg.jsonl");

        Directory.CreateDirectory(tempDir);

        try
        {
            var bundle = _bundleExporter.Export(
                graph,
                new SfdpOptions(),
                new SfdpExportBundleDiagnosticsOptions
                {
                    SvgDiagnostics = new SfdpDiagnosticsOptions { Path = svgDiagnosticsPath, IncludeIterations = false },
                    PngDiagnostics = new SfdpDiagnosticsOptions { Path = pngDiagnosticsPath, IncludeIterations = false },
                    JpgDiagnostics = new SfdpDiagnosticsOptions { Path = jpgDiagnosticsPath, IncludeIterations = false }
                });

            Assert.NotNull(bundle.Svg);
            Assert.NotNull(bundle.PngBytes);
            Assert.NotNull(bundle.JpgBytes);
            Assert.Contains("<svg", bundle.Svg, StringComparison.OrdinalIgnoreCase);
            Assert.True(bundle.PngBytes.Length > 8);
            Assert.Equal(0x89, bundle.PngBytes[0]);
            Assert.Equal((byte)'P', bundle.PngBytes[1]);
            Assert.Equal((byte)'N', bundle.PngBytes[2]);
            Assert.Equal((byte)'G', bundle.PngBytes[3]);
            Assert.True(bundle.JpgBytes.Length > 4);
            Assert.Equal(0xFF, bundle.JpgBytes[0]);
            Assert.Equal(0xD8, bundle.JpgBytes[1]);

            Assert.True(File.Exists(svgDiagnosticsPath));
            Assert.True(File.Exists(pngDiagnosticsPath));
            Assert.True(File.Exists(jpgDiagnosticsPath));
            Assert.Contains(File.ReadLines(svgDiagnosticsPath), static line => line.Contains("\"Phase\":\"svg\"", StringComparison.Ordinal));
            Assert.Contains(File.ReadLines(pngDiagnosticsPath), static line => line.Contains("\"format\":\"Png\"", StringComparison.Ordinal));
            Assert.Contains(File.ReadLines(jpgDiagnosticsPath), static line => line.Contains("\"format\":\"Jpg\"", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
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
