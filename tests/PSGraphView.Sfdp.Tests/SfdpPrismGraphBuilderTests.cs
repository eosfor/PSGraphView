using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismGraphBuilderTests
{
    [Fact]
    public void Build_CreatesBoxesOverlapAndProximityEdges()
    {
        var graph = SfdpPrismGraphBuilder.Build(
            x: [0.0, 1.0, 10.0],
            y: [0.0, 1.0, 0.0],
            nodeRadius: 1.0,
            padding: 0.0);

        Assert.Equal(3, graph.NodeBoxes.Count);
        Assert.Equal(3, graph.OverlapEdges.Count);
        Assert.NotEmpty(graph.ProximityEdges);
        Assert.Contains(graph.OverlapEdges, static edge => edge.Left == 0 && edge.Right == 1 && edge.IsOverlapConstraint);
        Assert.Contains(graph.OverlapEdges, static edge => edge.Left == 0 && edge.Right == 2 && edge.IsOverlapConstraint);
        Assert.Contains(graph.OverlapEdges, static edge => edge.Left == 1 && edge.Right == 2 && edge.IsOverlapConstraint);
    }
}
