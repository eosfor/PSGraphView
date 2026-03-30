namespace PSGraphView.Sfdp;

internal sealed class SfdpTransferOperator
{
    public SfdpTransferOperator(
        int fineNodeCount,
        int coarseNodeCount,
        int[] fineToCoarse,
        int[][] coarseToFine,
        SfdpSparseMatrix p,
        SfdpSparseMatrix rawR,
        SfdpSparseMatrix r)
    {
        FineNodeCount = fineNodeCount;
        CoarseNodeCount = coarseNodeCount;
        FineToCoarse = fineToCoarse;
        CoarseToFine = coarseToFine;
        P = p;
        RawR = rawR;
        R = r;
    }

    public int FineNodeCount { get; }
    public int CoarseNodeCount { get; }
    public int[] FineToCoarse { get; }
    public int[][] CoarseToFine { get; }
    public SfdpSparseMatrix P { get; }
    public SfdpSparseMatrix RawR { get; }
    public SfdpSparseMatrix R { get; }

    public static SfdpTransferOperator Identity(int nodeCount)
    {
        var fineToCoarse = Enumerable.Range(0, nodeCount).ToArray();
        var coarseToFine = Enumerable.Range(0, nodeCount)
            .Select(static index => new[] { index })
            .ToArray();
        var identity = SfdpSparseMatrix.Identity(nodeCount);
        return new SfdpTransferOperator(nodeCount, nodeCount, fineToCoarse, coarseToFine, identity, identity, identity);
    }

    public static SfdpTransferOperator FromClusters(int fineNodeCount, IReadOnlyList<int[]> coarseToFine)
    {
        var fineToCoarse = new int[fineNodeCount];
        for (var coarseIndex = 0; coarseIndex < coarseToFine.Count; coarseIndex++)
        {
            foreach (var fineNode in coarseToFine[coarseIndex])
            {
                fineToCoarse[fineNode] = coarseIndex;
            }
        }

        var pEntries = new List<(int Row, int Column, double Value)>(fineNodeCount);
        for (var fineNode = 0; fineNode < fineNodeCount; fineNode++)
        {
            pEntries.Add((fineNode, fineToCoarse[fineNode], 1.0));
        }

        var p = SfdpSparseMatrix.FromCoordinateEntries(fineNodeCount, coarseToFine.Count, pEntries);
        var rawR = p.Transpose();
        var r = rawR.NormalizeRowsBySum();

        return new SfdpTransferOperator(
            fineNodeCount,
            coarseToFine.Count,
            fineToCoarse,
            coarseToFine.Select(static cluster => cluster.ToArray()).ToArray(),
            p,
            rawR,
            r);
    }

    public static SfdpTransferOperator Compose(SfdpTransferOperator left, SfdpTransferOperator right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.CoarseNodeCount != right.FineNodeCount)
        {
            throw new ArgumentException("Transfer operators cannot be composed because their dimensions do not match.");
        }

        var fineToCoarse = new int[left.FineNodeCount];
        for (var fineNode = 0; fineNode < left.FineNodeCount; fineNode++)
        {
            fineToCoarse[fineNode] = right.FineToCoarse[left.FineToCoarse[fineNode]];
        }

        var coarseToFine = new int[right.CoarseNodeCount][];
        for (var coarseNode = 0; coarseNode < right.CoarseNodeCount; coarseNode++)
        {
            coarseToFine[coarseNode] = right.CoarseToFine[coarseNode]
                .SelectMany(midNode => left.CoarseToFine[midNode])
                .ToArray();
        }

        return new SfdpTransferOperator(
            left.FineNodeCount,
            right.CoarseNodeCount,
            fineToCoarse,
            coarseToFine,
            SfdpSparseMatrix.Multiply(left.P, right.P),
            SfdpSparseMatrix.Multiply(right.RawR, left.RawR),
            SfdpSparseMatrix.Multiply(right.R, left.R));
    }

    public void ApplyProlongation(double[] coarseX, double[] coarseY, double[] fineX, double[] fineY)
    {
        ArgumentNullException.ThrowIfNull(coarseX);
        ArgumentNullException.ThrowIfNull(coarseY);
        ArgumentNullException.ThrowIfNull(fineX);
        ArgumentNullException.ThrowIfNull(fineY);

        if (coarseX.Length != coarseY.Length || coarseX.Length != CoarseNodeCount)
        {
            throw new ArgumentException("Coarse coordinates must match the coarse operator dimension.");
        }

        if (fineX.Length != fineY.Length || fineX.Length != FineNodeCount)
        {
            throw new ArgumentException("Fine coordinates must match the fine operator dimension.");
        }

        var x = P.MultiplyDense(coarseX);
        var y = P.MultiplyDense(coarseY);
        Array.Copy(x, fineX, FineNodeCount);
        Array.Copy(y, fineY, FineNodeCount);
    }
}
