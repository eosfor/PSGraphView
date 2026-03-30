namespace PSGraphView.Sfdp;

internal sealed class SfdpPrismStressSystem
{
    public SfdpPrismStressSystem(
        SfdpSparseMatrix lw,
        SfdpSparseMatrix lwd,
        int combinedEdgeCount,
        int proximityOnlyEdgeCount,
        int overlapOnlyEdgeCount,
        int sharedEdgeCount,
        int expandEdgeCount,
        int shrinkEdgeCount,
        double maxOverlapFactor,
        double minOverlapFactor)
    {
        ArgumentNullException.ThrowIfNull(lw);
        ArgumentNullException.ThrowIfNull(lwd);

        if (lw.RowCount != lw.ColumnCount || lwd.RowCount != lwd.ColumnCount || lw.RowCount != lwd.RowCount)
        {
            throw new ArgumentException("Prism stress matrices must be square and have matching dimensions.");
        }

        Lw = lw;
        Lwd = lwd;
        CombinedEdgeCount = combinedEdgeCount;
        ProximityOnlyEdgeCount = proximityOnlyEdgeCount;
        OverlapOnlyEdgeCount = overlapOnlyEdgeCount;
        SharedEdgeCount = sharedEdgeCount;
        ExpandEdgeCount = expandEdgeCount;
        ShrinkEdgeCount = shrinkEdgeCount;
        MaxOverlapFactor = maxOverlapFactor;
        MinOverlapFactor = minOverlapFactor;
    }

    public SfdpSparseMatrix Lw { get; }

    public SfdpSparseMatrix Lwd { get; }

    public int CombinedEdgeCount { get; }

    public int ProximityOnlyEdgeCount { get; }

    public int OverlapOnlyEdgeCount { get; }

    public int SharedEdgeCount { get; }

    public int ExpandEdgeCount { get; }

    public int ShrinkEdgeCount { get; }

    public double MaxOverlapFactor { get; }

    public double MinOverlapFactor { get; }
}
