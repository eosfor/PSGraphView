using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPostProcessorTests
{
    [Fact]
    public void ApplyPrincipalComponentRotation_AlignsDominantAxisHorizontally()
    {
        var x = new[] { -3.0, -1.0, 1.0, 3.0 };
        var y = new[] { -3.0, -1.0, 1.0, 3.0 };

        SfdpPostProcessor.ApplyPrincipalComponentRotation(x, y);

        var width = x.Max() - x.Min();
        var height = y.Max() - y.Min();

        Assert.True(width > height, $"Expected width > height after rotation, but got width={width}, height={height}.");
    }

    [Fact]
    public void Rotate_RotatesPointsAroundCenter()
    {
        var x = new[] { -1.0, 1.0 };
        var y = new[] { 0.0, 0.0 };

        SfdpPostProcessor.Rotate(x, y, 90.0);

        Assert.True(Math.Abs(x[0]) < 0.0001);
        Assert.True(Math.Abs(x[1]) < 0.0001);
        Assert.True(y[0] > 0.9 || y[1] > 0.9);
        Assert.True(y[0] < -0.9 || y[1] < -0.9);
    }
}
