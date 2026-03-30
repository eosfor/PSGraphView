namespace PSGraphView.Sfdp;

internal static class SfdpPrismConjugateGradientSolver
{
    public static double Solve(
        SfdpSparseMatrix matrix,
        double[] initial,
        double[] rhs,
        double tolerance,
        int maxIterations)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(rhs);

        if (matrix.RowCount != matrix.ColumnCount || matrix.RowCount != initial.Length || rhs.Length != initial.Length)
        {
            throw new ArgumentException("Matrix and vectors must have matching square dimensions.");
        }

        var n = initial.Length;
        if (n == 0)
        {
            return 0.0;
        }

        var x = initial;
        var r = Subtract(rhs, matrix.MultiplyDense(x));
        var z = ApplyDiagonalPreconditioner(matrix, r);
        var p = (double[])z.Clone();
        var res0 = Norm(r) / n;
        var res = res0;
        var rhoOld = 1.0;

        var iter = 0;
        while (iter++ < maxIterations && res > tolerance * Math.Max(res0, 1e-12))
        {
            var rho = Dot(r, z);
            if (Math.Abs(rho) <= 1e-18)
            {
                break;
            }

            if (iter > 1)
            {
                var beta = rho / rhoOld;
                for (var i = 0; i < n; i++)
                {
                    p[i] = z[i] + (beta * p[i]);
                }
            }

            var q = matrix.MultiplyDense(p);
            var pq = Dot(p, q);
            if (Math.Abs(pq) <= 1e-18)
            {
                break;
            }

            var alpha = rho / pq;
            for (var i = 0; i < n; i++)
            {
                x[i] += alpha * p[i];
                r[i] -= alpha * q[i];
            }

            res = Norm(r) / n;
            z = ApplyDiagonalPreconditioner(matrix, r);
            rhoOld = rho;
        }

        return res;
    }

    private static double[] ApplyDiagonalPreconditioner(SfdpSparseMatrix matrix, double[] vector)
    {
        var result = new double[vector.Length];
        for (var row = 0; row < matrix.RowCount; row++)
        {
            var inverseDiagonal = 1.0;
            for (var offset = matrix.Offsets[row]; offset < matrix.Offsets[row + 1]; offset++)
            {
                if (matrix.Columns[offset] == row && Math.Abs(matrix.Values[offset]) > 1e-18)
                {
                    inverseDiagonal = 1.0 / matrix.Values[offset];
                    break;
                }
            }

            result[row] = vector[row] * inverseDiagonal;
        }

        return result;
    }

    private static double[] Subtract(double[] left, double[] right)
    {
        var result = new double[left.Length];
        for (var i = 0; i < left.Length; i++)
        {
            result[i] = left[i] - right[i];
        }

        return result;
    }

    private static double Dot(double[] left, double[] right)
    {
        var sum = 0.0;
        for (var i = 0; i < left.Length; i++)
        {
            sum += left[i] * right[i];
        }

        return sum;
    }

    private static double Norm(double[] vector)
    {
        return Math.Sqrt(Dot(vector, vector));
    }
}
