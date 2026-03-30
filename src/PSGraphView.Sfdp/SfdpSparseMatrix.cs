using System.Globalization;

namespace PSGraphView.Sfdp;

internal sealed class SfdpSparseMatrix
{
    private const double ZeroTolerance = 1e-12;

    public SfdpSparseMatrix(int rowCount, int columnCount, int[] offsets, int[] columns, double[] values)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
        Offsets = offsets;
        Columns = columns;
        Values = values;
    }

    public int RowCount { get; }
    public int ColumnCount { get; }
    public int[] Offsets { get; }
    public int[] Columns { get; }
    public double[] Values { get; }

    public static SfdpSparseMatrix Identity(int size)
    {
        var offsets = new int[size + 1];
        var columns = new int[size];
        var values = new double[size];
        for (var i = 0; i < size; i++)
        {
            offsets[i] = i;
            columns[i] = i;
            values[i] = 1.0;
        }

        offsets[size] = size;
        return new SfdpSparseMatrix(size, size, offsets, columns, values);
    }

    public static SfdpSparseMatrix FromCoordinateEntries(
        int rowCount,
        int columnCount,
        IEnumerable<(int Row, int Column, double Value)> entries)
    {
        var rows = new Dictionary<int, double>[rowCount];
        for (var i = 0; i < rowCount; i++)
        {
            rows[i] = [];
        }

        foreach (var (row, column, value) in entries)
        {
            if (Math.Abs(value) <= ZeroTolerance)
            {
                continue;
            }

            if (row < 0 || row >= rowCount || column < 0 || column >= columnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), $"Sparse entry ({row}, {column}) is outside matrix bounds {rowCount}x{columnCount}.");
            }

            rows[row].TryGetValue(column, out var current);
            rows[row][column] = current + value;
        }

        var offsets = new int[rowCount + 1];
        var columns = new List<int>();
        var values = new List<double>();

        for (var row = 0; row < rowCount; row++)
        {
            offsets[row] = columns.Count;
            foreach (var entry in rows[row].OrderBy(static pair => pair.Key))
            {
                if (Math.Abs(entry.Value) <= ZeroTolerance)
                {
                    continue;
                }

                columns.Add(entry.Key);
                values.Add(entry.Value);
            }
        }

        offsets[rowCount] = columns.Count;
        return new SfdpSparseMatrix(rowCount, columnCount, offsets, columns.ToArray(), values.ToArray());
    }

    public static SfdpSparseMatrix FromComponentGraph(SfdpComponentGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var entries = new List<(int Row, int Column, double Value)>();
        for (var row = 0; row < graph.NodeCount; row++)
        {
            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[row].Length; edgeIndex++)
            {
                entries.Add((row, graph.Neighbors[row][edgeIndex], graph.Weights[row][edgeIndex]));
            }
        }

        return FromCoordinateEntries(graph.NodeCount, graph.NodeCount, entries);
    }

    public SfdpSparseMatrix Transpose()
    {
        var entries = new List<(int Row, int Column, double Value)>(Values.Length);
        for (var row = 0; row < RowCount; row++)
        {
            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                entries.Add((Columns[offset], row, Values[offset]));
            }
        }

        return FromCoordinateEntries(ColumnCount, RowCount, entries);
    }

    public SfdpSparseMatrix NormalizeRowsBySum()
    {
        var entries = new List<(int Row, int Column, double Value)>(Values.Length);
        for (var row = 0; row < RowCount; row++)
        {
            var rowSum = 0.0;
            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                rowSum += Values[offset];
            }

            if (Math.Abs(rowSum) <= ZeroTolerance)
            {
                continue;
            }

            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                entries.Add((row, Columns[offset], Values[offset] / rowSum));
            }
        }

        return FromCoordinateEntries(RowCount, ColumnCount, entries);
    }

    public SfdpSparseMatrix RemoveDiagonal()
    {
        var entries = new List<(int Row, int Column, double Value)>(Values.Length);
        for (var row = 0; row < RowCount; row++)
        {
            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                if (Columns[offset] == row)
                {
                    continue;
                }

                entries.Add((row, Columns[offset], Values[offset]));
            }
        }

        return FromCoordinateEntries(RowCount, ColumnCount, entries);
    }

    public double[] MultiplyDense(double[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Length != ColumnCount)
        {
            throw new ArgumentException("Dense vector length must match sparse matrix column count.");
        }

        var result = new double[RowCount];
        for (var row = 0; row < RowCount; row++)
        {
            var sum = 0.0;
            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                sum += Values[offset] * vector[Columns[offset]];
            }

            result[row] = sum;
        }

        return result;
    }

    public static SfdpSparseMatrix Multiply(SfdpSparseMatrix left, SfdpSparseMatrix right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.ColumnCount != right.RowCount)
        {
            throw new ArgumentException($"Sparse matrices cannot be multiplied because dimensions do not match: {left.RowCount}x{left.ColumnCount} times {right.RowCount}x{right.ColumnCount}.");
        }

        var entries = new List<(int Row, int Column, double Value)>();
        for (var row = 0; row < left.RowCount; row++)
        {
            var accumulator = new Dictionary<int, double>();
            for (var leftOffset = left.Offsets[row]; leftOffset < left.Offsets[row + 1]; leftOffset++)
            {
                var pivot = left.Columns[leftOffset];
                var leftValue = left.Values[leftOffset];
                for (var rightOffset = right.Offsets[pivot]; rightOffset < right.Offsets[pivot + 1]; rightOffset++)
                {
                    var column = right.Columns[rightOffset];
                    accumulator.TryGetValue(column, out var current);
                    accumulator[column] = current + (leftValue * right.Values[rightOffset]);
                }
            }

            foreach (var entry in accumulator)
            {
                if (Math.Abs(entry.Value) <= ZeroTolerance)
                {
                    continue;
                }

                entries.Add((row, entry.Key, entry.Value));
            }
        }

        return FromCoordinateEntries(left.RowCount, right.ColumnCount, entries);
    }

    public static SfdpSparseMatrix Multiply3(SfdpSparseMatrix left, SfdpSparseMatrix middle, SfdpSparseMatrix right)
    {
        return Multiply(Multiply(left, middle), right);
    }

    public SfdpComponentGraph ToComponentGraph()
    {
        if (RowCount != ColumnCount)
        {
            throw new InvalidOperationException("Only square sparse matrices can be converted to a component graph.");
        }

        var neighbors = new int[RowCount][];
        var weights = new double[RowCount][];
        var edgeCount = 0;

        for (var row = 0; row < RowCount; row++)
        {
            var rowNeighbors = new List<int>();
            var rowWeights = new List<double>();
            for (var offset = Offsets[row]; offset < Offsets[row + 1]; offset++)
            {
                var column = Columns[offset];
                if (column == row || Math.Abs(Values[offset]) <= ZeroTolerance)
                {
                    continue;
                }

                rowNeighbors.Add(column);
                rowWeights.Add(Values[offset]);
                if (row < column)
                {
                    edgeCount++;
                }
            }

            neighbors[row] = rowNeighbors.ToArray();
            weights[row] = rowWeights.ToArray();
        }

        return new SfdpComponentGraph(
            Enumerable.Range(0, RowCount).ToArray(),
            edgeCount,
            neighbors,
            weights);
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{RowCount}x{ColumnCount} nnz={Values.Length}");
    }
}
