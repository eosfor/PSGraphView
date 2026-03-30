using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpOverlapRemoverTests
{
    [Fact]
    public void RemoveOverlaps_SeparatesCoincidentNodes()
    {
        var x = new[] { 0.0, 0.0, 0.0 };
        var y = new[] { 0.0, 0.0, 0.0 };

        SfdpOverlapRemover.RemoveOverlaps(
            x,
            y,
            nodeRadius: 5.0,
            padding: 1.0,
            maxIterations: 50,
            cancellationToken: CancellationToken.None);

        for (var i = 0; i < x.Length; i++)
        {
            for (var j = i + 1; j < x.Length; j++)
            {
                var dx = x[j] - x[i];
                var dy = y[j] - y[i];
                var distance = Math.Sqrt(dx * dx + dy * dy);
                Assert.True(distance >= 11.0 - 0.001);
            }
        }
    }
}
