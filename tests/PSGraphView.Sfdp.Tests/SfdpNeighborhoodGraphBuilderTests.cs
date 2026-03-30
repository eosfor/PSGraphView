using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpNeighborhoodGraphBuilderTests
{
    [Fact]
    public void Build_TwoPoints_ReturnsSingleEdge()
    {
        var edges = SfdpNeighborhoodGraphBuilder.Build(
            x: [0.0, 10.0],
            y: [0.0, 0.0]);

        Assert.Single(edges);
        Assert.Equal((0, 1), edges[0]);
    }
}
