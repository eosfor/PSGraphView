using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpCallTriTests
{
    [Fact]
    public void Build_TwoPoints_ReturnsDiagonalAndSymmetricEdge()
    {
        var matrix = SfdpCallTri.Build(
            x: [0.0, 10.0],
            y: [0.0, 0.0]);

        Assert.Equal(2, matrix.RowCount);
        Assert.Equal(2, matrix.ColumnCount);
        Assert.Equal(4, matrix.Values.Length);

        AssertRow(matrix, 0, [(0, 1.0), (1, 1.0)]);
        AssertRow(matrix, 1, [(0, 1.0), (1, 1.0)]);
    }

    private static void AssertRow(SfdpSparseMatrix matrix, int row, (int Column, double Value)[] expected)
    {
        var actual = new List<(int Column, double Value)>();
        for (var offset = matrix.Offsets[row]; offset < matrix.Offsets[row + 1]; offset++)
        {
            actual.Add((matrix.Columns[offset], matrix.Values[offset]));
        }

        Assert.Equal(expected, actual);
    }
}
