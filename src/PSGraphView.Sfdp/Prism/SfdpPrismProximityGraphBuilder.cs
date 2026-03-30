namespace PSGraphView.Sfdp;

internal static class SfdpPrismProximityGraphBuilder
{
    public static SfdpPrismEdge[] Build(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var adjacency = SfdpCallTri.Build(x, y);
        var edges = new List<SfdpPrismEdge>();
        for (var row = 0; row < adjacency.RowCount; row++)
        {
            for (var offset = adjacency.Offsets[row]; offset < adjacency.Offsets[row + 1]; offset++)
            {
                var column = adjacency.Columns[offset];
                if (column <= row)
                {
                    continue;
                }

                edges.Add(new SfdpPrismEdge(row, column));
            }
        }

        return edges.ToArray();
    }
}
