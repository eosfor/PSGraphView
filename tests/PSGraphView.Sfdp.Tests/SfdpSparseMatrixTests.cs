using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpSparseMatrixTests
{
    [Fact]
    public void Multiply3_WithTransferOperator_ProducesExpectedCoarseAdjacency()
    {
        var graph = new SfdpComponentGraph(
            [0, 1, 2, 3],
            4,
            [
                [1],
                [0, 2],
                [1, 3],
                [2]
            ],
            [
                [1.0],
                [1.0, 1.0],
                [1.0, 1.0],
                [1.0]
            ]);

        var adjacency = SfdpSparseMatrix.FromComponentGraph(graph);
        var transfer = SfdpTransferOperator.FromClusters(4, [[0, 1], [2, 3]]);

        var coarseAdjacency = SfdpSparseMatrix.Multiply3(transfer.RawR, adjacency, transfer.P).RemoveDiagonal();
        var coarseGraph = coarseAdjacency.ToComponentGraph();

        Assert.Equal(2, coarseGraph.NodeCount);
        Assert.Equal([1], coarseGraph.Neighbors[0]);
        Assert.Equal([0], coarseGraph.Neighbors[1]);
        Assert.Equal(1.0, coarseGraph.Weights[0][0]);
        Assert.Equal(1.0, coarseGraph.Weights[1][0]);
    }

    [Fact]
    public void NormalizeRowsBySum_ProducesWeightedR()
    {
        var transfer = SfdpTransferOperator.FromClusters(4, [[0, 1, 2], [3]]);

        var row0Values = GetRowValues(transfer.R, 0);
        var row1Values = GetRowValues(transfer.R, 1);

        Assert.Equal([1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0], row0Values);
        Assert.Equal([1.0], row1Values);
    }

    private static double[] GetRowValues(SfdpSparseMatrix matrix, int row)
    {
        var start = matrix.Offsets[row];
        var length = matrix.Offsets[row + 1] - start;
        var values = new double[length];
        Array.Copy(matrix.Values, start, values, 0, length);
        return values;
    }
}
