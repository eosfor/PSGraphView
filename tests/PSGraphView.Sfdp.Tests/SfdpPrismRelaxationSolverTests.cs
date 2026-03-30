using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismRelaxationSolverTests
{
    [Fact]
    public void Relax_MovesNodesApartForRepulsiveConstraint()
    {
        var x = new[] { 0.0, 0.001 };
        var y = new[] { 0.0, 0.0 };
        var prismGraph = new SfdpPrismGraph(
            nodeBoxes:
            [
                new SfdpPrismNodeBox(0, x[0], y[0], 2.0, 2.0),
                new SfdpPrismNodeBox(1, x[1], y[1], 2.0, 2.0)
            ],
            overlapEdges:
            [
                new SfdpPrismEdge(0, 1, IsOverlapConstraint: true)
            ],
            proximityEdges: []);
        var stressSystem = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: false, minDistance: 0.0001, expansionWeightMultiplier: 100.0);

        var result = SfdpPrismRelaxationSolver.Relax(x, y, stressSystem, minDistance: 0.0001);

        Assert.True(result.Changed);
        Assert.NotNull(result.Residual);
        Assert.Equal(0, result.ZeroDistancePerturbations);
        Assert.True(x[0] < 0.0 || x[1] > 0.001);
    }

    [Fact]
    public void Relax_WithCoincidentNodes_PerturbsZeroDistancePair()
    {
        var x = new[] { 0.0, 0.0 };
        var y = new[] { 0.0, 0.0 };
        var prismGraph = new SfdpPrismGraph(
            nodeBoxes:
            [
                new SfdpPrismNodeBox(0, x[0], y[0], 2.0, 2.0),
                new SfdpPrismNodeBox(1, x[1], y[1], 2.0, 2.0)
            ],
            overlapEdges:
            [
                new SfdpPrismEdge(0, 1, IsOverlapConstraint: true)
            ],
            proximityEdges: []);
        var stressSystem = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: false, minDistance: 0.0001, expansionWeightMultiplier: 100.0);

        var result = SfdpPrismRelaxationSolver.Relax(x, y, stressSystem, minDistance: 0.0001, random: new Random(42));

        Assert.True(result.Changed);
        Assert.NotNull(result.Residual);
        Assert.True(result.ZeroDistancePerturbations > 0);
        Assert.NotEqual(0.0, x[1]);
        Assert.NotEqual(0.0, y[1]);
    }
}
