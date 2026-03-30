using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismBoxBuilderTests
{
    [Fact]
    public void Build_CreatesBoxesWithExpectedCentersAndHalfSizes()
    {
        var x = new[] { 1.0, 3.0 };
        var y = new[] { 2.0, 4.0 };

        var boxes = SfdpPrismBoxBuilder.Build(x, y, nodeRadius: 5.0, padding: 2.0);

        Assert.Equal(2, boxes.Length);
        Assert.Equal(new SfdpPrismNodeBox(0, 1.0, 2.0, 6.0, 6.0), boxes[0]);
        Assert.Equal(new SfdpPrismNodeBox(1, 3.0, 4.0, 6.0, 6.0), boxes[1]);
    }

    [Fact]
    public void Build_WithGraphvizPoints_ConvertsRadiusAndPaddingToLayoutUnits()
    {
        var x = new[] { 1.0, 3.0 };
        var y = new[] { 2.0, 4.0 };

        var boxes = SfdpPrismBoxBuilder.Build(
            x,
            y,
            nodeRadius: 0.72,
            padding: 4.0,
            boxUnits: SfdpOverlapRemovalBoxUnits.GraphvizPoints);

        Assert.Equal(2, boxes.Length);
        Assert.Equal(0, boxes[0].Index);
        Assert.Equal(1.0, boxes[0].CenterX);
        Assert.Equal(2.0, boxes[0].CenterY);
        Assert.Equal(0.06555555555555556, boxes[0].HalfWidth, 12);
        Assert.Equal(0.06555555555555556, boxes[0].HalfHeight, 12);
        Assert.Equal(1, boxes[1].Index);
        Assert.Equal(3.0, boxes[1].CenterX);
        Assert.Equal(4.0, boxes[1].CenterY);
        Assert.Equal(0.06555555555555556, boxes[1].HalfWidth, 12);
        Assert.Equal(0.06555555555555556, boxes[1].HalfHeight, 12);
    }
}
