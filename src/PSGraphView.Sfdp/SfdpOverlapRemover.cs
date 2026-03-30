namespace PSGraphView.Sfdp;

internal static class SfdpOverlapRemover
{
    private const double MinDistance = 0.0001;

    public static void RemoveOverlaps(
        double[] x,
        double[] y,
        double nodeRadius,
        double padding,
        int maxIterations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length <= 1 || maxIterations <= 0)
        {
            return;
        }

        var minimumSeparation = Math.Max(nodeRadius * 2.0 + padding, 0.0);
        if (minimumSeparation <= 0.0)
        {
            Recenter(x, y);
            return;
        }

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var moved = false;
            for (var i = 0; i < x.Length; i++)
            {
                for (var j = i + 1; j < x.Length; j++)
                {
                    var dx = x[j] - x[i];
                    var dy = y[j] - y[i];
                    var distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance >= minimumSeparation)
                    {
                        continue;
                    }

                    if (distance < MinDistance)
                    {
                        var angle = ((i + 1) * 0.7548776662466927) + ((j + 1) * 0.5698402909980532);
                        dx = Math.Cos(angle);
                        dy = Math.Sin(angle);
                        distance = 1.0;
                    }

                    var overlap = minimumSeparation - distance;
                    var pushX = dx / distance * overlap * 0.5;
                    var pushY = dy / distance * overlap * 0.5;

                    x[i] -= pushX;
                    y[i] -= pushY;
                    x[j] += pushX;
                    y[j] += pushY;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        Recenter(x, y);
    }

    private static void Recenter(double[] x, double[] y)
    {
        var centerX = 0.0;
        var centerY = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            centerX += x[i];
            centerY += y[i];
        }

        centerX /= x.Length;
        centerY /= y.Length;

        for (var i = 0; i < x.Length; i++)
        {
            x[i] -= centerX;
            y[i] -= centerY;
        }
    }

}
