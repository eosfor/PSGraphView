using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpUgGraphBuilderTests
{
    [Fact]
    public void Build_TwoPoints_ReturnsSelfAndOtherNode()
    {
        var graph = SfdpUgGraphBuilder.Build(
            x: [0.0, 10.0],
            y: [0.0, 0.0]);

        Assert.Equal(2, graph.Length);
        Assert.Equal([0, 1], graph[0].Edges);
        Assert.Equal([1, 0], graph[1].Edges);
    }

    [Fact]
    public void Build_CollinearPoints_PreservesChain()
    {
        var graph = SfdpUgGraphBuilder.Build(
            x: [0.0, 5.0, 10.0],
            y: [0.0, 0.0, 0.0]);

        Assert.Equal([0, 1], graph[0].Edges);
        Assert.Equal([1, 0, 2], graph[1].Edges);
        Assert.Equal([2, 1], graph[2].Edges);
    }
}
