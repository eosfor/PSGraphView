using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismOverlapGraphBuilderTests
{
    [Fact]
    public void Build_ReturnsOverlapEdgeForCoincidentBoxes()
    {
        SfdpPrismNodeBox[] boxes =
        [
            new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
            new SfdpPrismNodeBox(1, 1.0, 1.0, 2.0, 2.0)
        ];

        var overlaps = SfdpPrismOverlapGraphBuilder.Build(boxes);

        Assert.Single(overlaps);
        Assert.Equal(new SfdpPrismEdge(0, 1, IsOverlapConstraint: true), overlaps[0]);
    }

    [Fact]
    public void Build_DoesNotReturnEdgeForSeparatedBoxes()
    {
        SfdpPrismNodeBox[] boxes =
        [
            new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
            new SfdpPrismNodeBox(1, 10.0, 10.0, 2.0, 2.0)
        ];

        var overlaps = SfdpPrismOverlapGraphBuilder.Build(boxes);

        Assert.Empty(overlaps);
    }

    [Fact]
    public void Build_ReturnsEdgeForBoxesSeparatedInXButOverlappingInY()
    {
        SfdpPrismNodeBox[] boxes =
        [
            new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
            new SfdpPrismNodeBox(1, 10.0, 1.0, 2.0, 2.0)
        ];

        var overlaps = SfdpPrismOverlapGraphBuilder.Build(boxes);

        Assert.Single(overlaps);
        Assert.Equal(new SfdpPrismEdge(0, 1, IsOverlapConstraint: true), overlaps[0]);
    }

    [Fact]
    public void HasAnyOverlap_ReturnsTrueForIntersectingBoxes()
    {
        SfdpPrismNodeBox[] boxes =
        [
            new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
            new SfdpPrismNodeBox(1, 1.0, 0.0, 2.0, 2.0)
        ];

        Assert.True(SfdpPrismOverlapGraphBuilder.HasAnyOverlap(boxes));
    }

    [Fact]
    public void HasAnyOverlap_ReturnsTrueForBoxesSeparatedInXButOverlappingInY()
    {
        SfdpPrismNodeBox[] boxes =
        [
            new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
            new SfdpPrismNodeBox(1, 10.0, 1.0, 2.0, 2.0)
        ];

        Assert.True(SfdpPrismOverlapGraphBuilder.HasAnyOverlap(boxes));
    }
}
