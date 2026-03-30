using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpTransferOperatorTests
{
    [Fact]
    public void Compose_CombinesFineToCoarseMappings()
    {
        var first = SfdpTransferOperator.FromClusters(4, [[0, 1], [2, 3]]);
        var second = SfdpTransferOperator.FromClusters(2, [[0, 1]]);

        var composed = SfdpTransferOperator.Compose(first, second);

        Assert.Equal(4, composed.FineNodeCount);
        Assert.Equal(1, composed.CoarseNodeCount);
        Assert.Equal([0, 0, 0, 0], composed.FineToCoarse);
        Assert.Equal([0, 1, 2, 3], composed.CoarseToFine[0]);
    }

    [Fact]
    public void ApplyProlongation_CopiesCoarseCoordinatesToAssignedFineNodes()
    {
        var transfer = SfdpTransferOperator.FromClusters(4, [[0, 1], [2, 3]]);
        var coarseX = new[] { 10.0, -5.0 };
        var coarseY = new[] { 2.0, 7.0 };
        var fineX = new double[4];
        var fineY = new double[4];

        transfer.ApplyProlongation(coarseX, coarseY, fineX, fineY);

        Assert.Equal([10.0, 10.0, -5.0, -5.0], fineX);
        Assert.Equal([2.0, 2.0, 7.0, 7.0], fineY);
    }
}
