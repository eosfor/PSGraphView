using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismTriangulationBuilderTests
{
    [Fact]
    public void BuildEdges_Square_UsesGraphvizDiagonal()
    {
        var edges = SfdpPrismTriangulationBuilder.BuildEdges(
            x: [0.0, 10.0, 10.0, 0.0],
            y: [0.0, 0.0, 10.0, 10.0]);

        SfdpPrismEdge[] expected =
        [
            new(0, 1),
            new(0, 2),
            new(0, 3),
            new(1, 2),
            new(2, 3)
        ];

        Assert.Equal(
            expected.OrderBy(static edge => edge.Left).ThenBy(static edge => edge.Right),
            edges.OrderBy(static edge => edge.Left).ThenBy(static edge => edge.Right));
    }

    [Fact]
    public void BuildEdges_RoofShape_MatchesGraphvizEdgeSet()
    {
        var edges = SfdpPrismTriangulationBuilder.BuildEdges(
            x: [0.0, 10.0, 20.0, 5.0, 15.0],
            y: [0.0, 0.0, 0.0, 10.0, 10.0]);

        SfdpPrismEdge[] expected =
        [
            new(0, 1),
            new(0, 3),
            new(1, 2),
            new(1, 3),
            new(1, 4),
            new(2, 4),
            new(3, 4)
        ];

        Assert.Equal(
            expected.OrderBy(static edge => edge.Left).ThenBy(static edge => edge.Right),
            edges.OrderBy(static edge => edge.Left).ThenBy(static edge => edge.Right));
    }
}
