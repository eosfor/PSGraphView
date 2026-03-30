namespace PSGraphView.Sfdp;

internal static class SfdpPrismBoxBuilder
{
    private const double PointsPerInch = 72.0;

    public static SfdpPrismNodeBox[] Build(
        double[] x,
        double[] y,
        double nodeRadius,
        double padding,
        SfdpOverlapRemovalBoxUnits boxUnits = SfdpOverlapRemovalBoxUnits.OutputUnits)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var halfWidth = GetDefaultHalfSize(nodeRadius, padding, boxUnits);
        var halfHeight = halfWidth;
        return BuildWithHalfSize(x, y, halfWidth, halfHeight);
    }

    public static double GetDefaultHalfSize(
        double nodeRadius,
        double padding,
        SfdpOverlapRemovalBoxUnits boxUnits)
    {
        return boxUnits switch
        {
            SfdpOverlapRemovalBoxUnits.OutputUnits => Math.Max(nodeRadius + (padding * 0.5), 0.0),
            SfdpOverlapRemovalBoxUnits.GraphvizPoints => Math.Max((nodeRadius + padding) / PointsPerInch, 0.0),
            _ => throw new ArgumentOutOfRangeException(nameof(boxUnits), boxUnits, "Unsupported overlap removal box units.")
        };
    }

    public static SfdpPrismNodeBox[] BuildWithHalfSize(double[] x, double[] y, double halfWidth, double halfHeight)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var boxes = new SfdpPrismNodeBox[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            boxes[i] = new SfdpPrismNodeBox(i, x[i], y[i], halfWidth, halfHeight);
        }

        return boxes;
    }
}
