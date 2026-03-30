namespace PSGraphView.Sfdp;

internal static class SfdpPrismGraphBuilder
{
    public static SfdpPrismGraph Build(
        double[] x,
        double[] y,
        double nodeRadius,
        double padding,
        SfdpOverlapRemovalBoxUnits boxUnits = SfdpOverlapRemovalBoxUnits.OutputUnits)
    {
        var nodeBoxes = SfdpPrismBoxBuilder.Build(x, y, nodeRadius, padding, boxUnits);
        var overlapEdges = SfdpPrismOverlapGraphBuilder.Build(nodeBoxes);
        var proximityEdges = SfdpPrismProximityGraphBuilder.Build(x, y);
        return new SfdpPrismGraph(nodeBoxes, overlapEdges, proximityEdges);
    }

    public static SfdpPrismGraph BuildWithHalfSize(double[] x, double[] y, double halfWidth, double halfHeight)
    {
        var nodeBoxes = SfdpPrismBoxBuilder.BuildWithHalfSize(x, y, halfWidth, halfHeight);
        var overlapEdges = SfdpPrismOverlapGraphBuilder.Build(nodeBoxes);
        var proximityEdges = SfdpPrismProximityGraphBuilder.Build(x, y);
        return new SfdpPrismGraph(nodeBoxes, overlapEdges, proximityEdges);
    }
}
