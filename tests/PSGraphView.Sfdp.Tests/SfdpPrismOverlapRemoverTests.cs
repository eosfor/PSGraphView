using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismOverlapRemoverTests
{
    [Fact]
    public void RemoveOverlaps_SeparatesCoincidentNodes()
    {
        var x = new[] { 0.0, 0.0, 0.0 };
        var y = new[] { 0.0, 0.0, 0.0 };

        SfdpPrismOverlapRemover.RemoveOverlaps(
            x,
            y,
            nodeRadius: 5.0,
            padding: 1.0,
            boxUnits: SfdpOverlapRemovalBoxUnits.OutputUnits,
            overlapHalfWidth: null,
            overlapHalfHeight: null,
            maxIterations: 32,
            random: null,
            cancellationToken: CancellationToken.None);

        for (var i = 0; i < x.Length; i++)
        {
            for (var j = i + 1; j < x.Length; j++)
            {
                var dx = x[j] - x[i];
                var dy = y[j] - y[i];
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                Assert.True(distance >= 11.0 - 0.001);
            }
        }
    }

    [Fact]
    public void RemoveOverlaps_WithDiagnostics_WritesTerminationDetails()
    {
        var x = new[] { 0.0, 0.0, 0.0 };
        var y = new[] { 0.0, 0.0, 0.0 };
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-overlap.jsonl");

        try
        {
            using (var diagnosticsWriter = SfdpDiagnosticsWriter.Create(new SfdpDiagnosticsOptions
            {
                Path = diagnosticsPath,
                IncludeIterations = true
            }))
            {
                SfdpPrismOverlapRemover.RemoveOverlaps(
                    x,
                    y,
                    nodeRadius: 5.0,
                    padding: 1.0,
                    boxUnits: SfdpOverlapRemovalBoxUnits.OutputUnits,
                    overlapHalfWidth: 2.5,
                    overlapHalfHeight: 3.5,
                    maxIterations: 32,
                    random: null,
                    cancellationToken: CancellationToken.None,
                    diagnostics: diagnosticsWriter);
            }

            var diagnostics = File.ReadAllText(diagnosticsPath);
            Assert.Contains("terminationReason", diagnostics, StringComparison.Ordinal);
            Assert.Contains("finalMaxOverlap", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"Name\":\"model\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"Name\":\"geometry\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"stage\":\"pre_scale\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"stage\":\"post_scale\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("combinedEdgeCount", diagnostics, StringComparison.Ordinal);
            Assert.Contains("expandEdgeCount", diagnostics, StringComparison.Ordinal);
            Assert.Contains("shrinkEdgeCount", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"boxSource\":\"override\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"boxHalfWidth\":2.5", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"boxHalfHeight\":3.5", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"Name\":\"finish\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("finishWidth", diagnostics, StringComparison.Ordinal);
            Assert.Contains("finishAverageEdgeLength", diagnostics, StringComparison.Ordinal);
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
    public void RemoveOverlaps_WithShrinkDiagnostics_WritesScalingDetails()
    {
        var x = new[] { 0.0, 20.0 };
        var y = new[] { 0.0, 0.0 };
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-overlap-shrink.jsonl");

        try
        {
            using (var diagnosticsWriter = SfdpDiagnosticsWriter.Create(new SfdpDiagnosticsOptions
            {
                Path = diagnosticsPath,
                IncludeIterations = true
            }))
            {
                SfdpPrismOverlapRemover.RemoveOverlaps(
                    x,
                    y,
                    nodeRadius: 5.0,
                    padding: 1.0,
                    boxUnits: SfdpOverlapRemovalBoxUnits.OutputUnits,
                    overlapHalfWidth: null,
                    overlapHalfHeight: null,
                    maxIterations: 32,
                    random: null,
                    cancellationToken: CancellationToken.None,
                    diagnostics: diagnosticsWriter);
            }

            var diagnostics = File.ReadAllText(diagnosticsPath);
            Assert.Contains("\"Name\":\"shrink\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("enteredShrinkStage", diagnostics, StringComparison.Ordinal);
            Assert.Contains("scaleBest", diagnostics, StringComparison.Ordinal);
            Assert.Contains("returnedEarlyAtScaleStart", diagnostics, StringComparison.Ordinal);
            Assert.Contains("hasOverlapAtScaleStart", diagnostics, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }
}
