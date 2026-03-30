using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpPrismModelBuilderTests
{
    [Fact]
    public void Build_IncludesOverlapEdgesWhenNeighborhoodOnlyIsFalse()
    {
        var prismGraph = new SfdpPrismGraph(
            nodeBoxes:
            [
                new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
                new SfdpPrismNodeBox(1, 1.0, 0.0, 2.0, 2.0)
            ],
            overlapEdges:
            [
                new SfdpPrismEdge(0, 1, IsOverlapConstraint: true)
            ],
            proximityEdges: []);

        var neighborhoodOnlyModel = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: true, minDistance: 0.0001, expansionWeightMultiplier: 100.0);
        var fullModel = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: false, minDistance: 0.0001, expansionWeightMultiplier: 100.0);

        Assert.Empty(neighborhoodOnlyModel.Lw.Values);
        Assert.NotEmpty(fullModel.Lw.Values);
        Assert.NotEmpty(fullModel.Lwd.Values);
    }

    [Fact]
    public void Build_ComputesOverlapFactorAboveOneForIntersectingBoxes()
    {
        var prismGraph = new SfdpPrismGraph(
            nodeBoxes:
            [
                new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
                new SfdpPrismNodeBox(1, 1.0, 0.0, 2.0, 2.0)
            ],
            overlapEdges:
            [
                new SfdpPrismEdge(0, 1, IsOverlapConstraint: true)
            ],
            proximityEdges: []);

        var model = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: false, minDistance: 0.0001, expansionWeightMultiplier: 100.0);

        Assert.True(model.MaxOverlapFactor > 1.0);
        Assert.True(double.IsFinite(model.MinOverlapFactor));
    }

    [Fact]
    public void Build_MergesProximityAndOverlapEdgesByEndpoints()
    {
        var prismGraph = new SfdpPrismGraph(
            nodeBoxes:
            [
                new SfdpPrismNodeBox(0, 0.0, 0.0, 2.0, 2.0),
                new SfdpPrismNodeBox(1, 1.0, 0.0, 2.0, 2.0)
            ],
            overlapEdges:
            [
                new SfdpPrismEdge(0, 1, IsOverlapConstraint: true)
            ],
            proximityEdges:
            [
                new SfdpPrismEdge(0, 1)
            ]);

        var model = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly: false, minDistance: 0.0001, expansionWeightMultiplier: 100.0);

        Assert.Equal(1, model.CombinedEdgeCount);
        Assert.Equal(0, model.ProximityOnlyEdgeCount);
        Assert.Equal(0, model.OverlapOnlyEdgeCount);
        Assert.Equal(1, model.SharedEdgeCount);
    }
}
