namespace PSGraphView.Sfdp;

internal static class SfdpCallTri
{
    public static SfdpSparseMatrix Build(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var nodeCount = x.Length;
        var entries = new List<(int Row, int Column, double Value)>();

        if (nodeCount > 2)
        {
            foreach (var edge in SfdpPrismTriangulationBuilder.BuildEdges(x, y))
            {
                entries.Add((edge.Left, edge.Right, 1.0));
            }
        }
        else if (nodeCount == 2)
        {
            entries.Add((0, 1, 1.0));
        }

        for (var i = 0; i < nodeCount; i++)
        {
            entries.Add((i, i, 1.0));
        }

        return Symmetrize(SfdpSparseMatrix.FromCoordinateEntries(nodeCount, nodeCount, entries));
    }

    public static SfdpSparseMatrix Build2(int nodeCount, int dimension, double[] coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        if (dimension < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be at least 2.");
        }

        if (coordinates.Length != nodeCount * dimension)
        {
            throw new ArgumentException("Coordinate array length must match nodeCount * dimension.");
        }

        var x = new double[nodeCount];
        var y = new double[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            x[i] = coordinates[i * dimension];
            y[i] = coordinates[(i * dimension) + 1];
        }

        var graph = SfdpUgGraphBuilder.Build(x, y);
        var entries = new List<(int Row, int Column, double Value)>();
        for (var i = 0; i < graph.Length; i++)
        {
            for (var j = 1; j < graph[i].Edges.Count; j++)
            {
                entries.Add((i, graph[i].Edges[j], 1.0));
            }
        }

        for (var i = 0; i < nodeCount; i++)
        {
            entries.Add((i, i, 1.0));
        }

        return Symmetrize(SfdpSparseMatrix.FromCoordinateEntries(nodeCount, nodeCount, entries));
    }

    private static SfdpSparseMatrix Symmetrize(SfdpSparseMatrix matrix)
    {
        var entries = new List<(int Row, int Column, double Value)>(matrix.Values.Length * 2);
        for (var row = 0; row < matrix.RowCount; row++)
        {
            for (var offset = matrix.Offsets[row]; offset < matrix.Offsets[row + 1]; offset++)
            {
                var column = matrix.Columns[offset];
                var value = matrix.Values[offset];
                entries.Add((row, column, value));
                if (row != column)
                {
                    entries.Add((column, row, value));
                }
            }
        }

        return SfdpSparseMatrix.FromCoordinateEntries(matrix.RowCount, matrix.ColumnCount, entries);
    }
}
