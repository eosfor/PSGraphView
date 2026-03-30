namespace PSGraphView.Sfdp;

internal sealed record SfdpPrismRelaxationResult(
    bool Changed,
    double? Residual,
    int ZeroDistancePerturbations);

internal static class SfdpPrismRelaxationSolver
{
    public static SfdpPrismRelaxationResult Relax(
        double[] x,
        double[] y,
        SfdpPrismStressSystem stressSystem,
        double minDistance,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(stressSystem);

        if (stressSystem.Lw.RowCount != x.Length || x.Length != y.Length)
        {
            throw new ArgumentException("Stress system and coordinates must have the same length.");
        }

        if (stressSystem.Lw.Values.Length == 0)
        {
            return new SfdpPrismRelaxationResult(false, null, 0);
        }

        var lwdd = BuildIterationMatrix(stressSystem.Lw, stressSystem.Lwd, x, y, minDistance, random, out var zeroDistancePerturbations);
        if (lwdd.Values.Length == 0)
        {
            return new SfdpPrismRelaxationResult(false, null, zeroDistancePerturbations);
        }

        var rhsX = lwdd.MultiplyDense(x);
        var rhsY = lwdd.MultiplyDense(y);
        var maxIterations = Math.Max(1, (int)Math.Floor(Math.Sqrt(x.Length)));
        const double tolerance = 0.01;

        var residualX = SfdpPrismConjugateGradientSolver.Solve(stressSystem.Lw, x, rhsX, tolerance, maxIterations);
        var residualY = SfdpPrismConjugateGradientSolver.Solve(stressSystem.Lw, y, rhsY, tolerance, maxIterations);

        return new SfdpPrismRelaxationResult(true, Math.Max(residualX, residualY), zeroDistancePerturbations);
    }

    private static SfdpSparseMatrix BuildIterationMatrix(
        SfdpSparseMatrix lw,
        SfdpSparseMatrix lwd,
        double[] x,
        double[] y,
        double minDistance,
        Random? random,
        out int zeroDistancePerturbations)
    {
        zeroDistancePerturbations = 0;
        var entries = new List<(int Row, int Column, double Value)>(lwd.Values.Length);
        for (var row = 0; row < lwd.RowCount; row++)
        {
            var diagonal = 0.0;
            for (var offset = lwd.Offsets[row]; offset < lwd.Offsets[row + 1]; offset++)
            {
                var column = lwd.Columns[offset];
                if (column == row)
                {
                    continue;
                }

                var dx = x[row] - x[column];
                var dy = y[row] - y[column];
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= double.Epsilon)
                {
                    PerturbCoincidentPoint(x, y, column, lwd.Values[offset], lw.Values[offset], random);
                    zeroDistancePerturbations++;
                    dx = x[row] - x[column];
                    dy = y[row] - y[column];
                    distanceSquared = (dx * dx) + (dy * dy);
                }

                var distance = Math.Max(Math.Sqrt(distanceSquared), minDistance);
                var value = lwd.Values[offset] / distance;
                entries.Add((row, column, value));
                diagonal -= value;
            }

            if (Math.Abs(diagonal) > 1e-18)
            {
                entries.Add((row, row, diagonal));
            }
        }

        return SfdpSparseMatrix.FromCoordinateEntries(lwd.RowCount, lwd.ColumnCount, entries);
    }

    private static void PerturbCoincidentPoint(
        double[] x,
        double[] y,
        int index,
        double weightedDistance,
        double weight,
        Random? random)
    {
        var idealDistance = Math.Abs(weight) > 1e-18 ? Math.Abs(weightedDistance / weight) : 1.0;
        var source = random ?? Random.Shared;
        x[index] += 0.0001 * (source.NextDouble() + 0.0001) * idealDistance;
        y[index] += 0.0001 * (source.NextDouble() + 0.0001) * idealDistance;
    }
}
