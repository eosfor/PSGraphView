using PSGraph.Model;
using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpGraphBuilderTests
{
    [Fact]
    public void BuildIndexed_PreservesNodeOrderAndLabels()
    {
        var graph = CreateGraph();

        var indexed = SfdpGraphBuilder.BuildIndexed(graph);

        Assert.Equal(3, indexed.NodeCount);
        Assert.Equal("A", indexed.Nodes[0].Label);
        Assert.Equal("B", indexed.Nodes[1].Label);
        Assert.Equal("C", indexed.Nodes[2].Label);
        Assert.Equal(2, indexed.EdgeCount);
        Assert.Equal(0, indexed.NodeIndexById["A"]);
        Assert.Equal(1, indexed.NodeIndexById["B"]);
    }

    [Fact]
    public void BuildUndirectedCsr_SymmetrizesDirectedEdges()
    {
        var indexed = SfdpGraphBuilder.BuildIndexed(CreateGraph());

        var csr = SfdpGraphBuilder.BuildUndirectedCsr(indexed);

        Assert.Equal([1], csr.GetNeighbors(0).ToArray());
        Assert.Equal([0, 2], csr.GetNeighbors(1).ToArray());
        Assert.Equal([1], csr.GetNeighbors(2).ToArray());
    }

    [Fact]
    public void BuildUndirectedCsr_RemovesSelfLoops()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "A", null, 1),
                new GraphViewEdge("A", "B", null, 1)
            ]);

        var indexed = SfdpGraphBuilder.BuildIndexed(graph);
        var csr = SfdpGraphBuilder.BuildUndirectedCsr(indexed);

        Assert.Equal([1], csr.GetNeighbors(0).ToArray());
        Assert.Equal([0], csr.GetNeighbors(1).ToArray());
    }

    [Fact]
    public void BuildUndirectedCsr_UsesEdgeWeightsAsMultiplicities()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 3)
            ]);

        var indexed = SfdpGraphBuilder.BuildIndexed(graph);
        var csr = SfdpGraphBuilder.BuildUndirectedCsr(indexed);

        Assert.Equal([1], csr.GetNeighbors(0).ToArray());
        Assert.Equal([3], csr.Multiplicities!.Take(1).ToArray());
    }

    private static GraphView CreateGraph()
    {
        return new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>()),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("B", "C", null, 1)
            ]);
    }
}
