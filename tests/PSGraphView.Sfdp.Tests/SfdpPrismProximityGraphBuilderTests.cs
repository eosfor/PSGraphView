using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismProximityGraphBuilderTests
{
    [Fact]
    public void Build_TwoPoints_ReturnsSingleNormalizedEdge()
    {
        var edges = SfdpPrismProximityGraphBuilder.Build(
            x: [10.0, 0.0],
            y: [0.0, 0.0]);

        Assert.Single(edges);
        Assert.Equal(new SfdpPrismEdge(0, 1), edges[0]);
    }

    [Fact]
    public void Build_Triangle_ReturnsThreeHullEdges()
    {
        var edges = SfdpPrismProximityGraphBuilder.Build(
            x: [0.0, 10.0, 5.0],
            y: [0.0, 0.0, 10.0]);

        Assert.Equal(3, edges.Length);
        Assert.Contains(new SfdpPrismEdge(0, 1), edges);
        Assert.Contains(new SfdpPrismEdge(0, 2), edges);
        Assert.Contains(new SfdpPrismEdge(1, 2), edges);
    }

    [Fact]
    public void Build_Square_ReturnsHullEdgesAndOneDiagonal()
    {
        var edges = SfdpPrismProximityGraphBuilder.Build(
            x: [0.0, 10.0, 10.0, 0.0],
            y: [0.0, 0.0, 10.0, 10.0]);

        Assert.Equal(5, edges.Length);
        Assert.Contains(new SfdpPrismEdge(0, 1), edges);
        Assert.Contains(new SfdpPrismEdge(1, 2), edges);
        Assert.Contains(new SfdpPrismEdge(2, 3), edges);
        Assert.Contains(new SfdpPrismEdge(0, 3), edges);
        Assert.Contains(new SfdpPrismEdge(0, 2), edges);
        Assert.DoesNotContain(new SfdpPrismEdge(1, 3), edges);
    }

    [Fact]
    public void Build_CollinearPoints_ReturnsChain()
    {
        var edges = SfdpPrismProximityGraphBuilder.Build(
            x: [5.0, 0.0, 10.0, 15.0],
            y: [0.0, 0.0, 0.0, 0.0]);

        SfdpPrismEdge[] expected =
        [
            new(0, 2),
            new(1, 0),
            new(2, 3)
        ];

        Assert.Equal(
            expected
                .Select(static edge => edge.Normalize())
                .OrderBy(static edge => edge.Left)
                .ThenBy(static edge => edge.Right),
            edges.OrderBy(static edge => edge.Left).ThenBy(static edge => edge.Right));
    }
}
