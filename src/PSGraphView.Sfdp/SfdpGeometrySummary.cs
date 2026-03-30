namespace PSGraphView.Sfdp;

internal readonly record struct SfdpGeometrySummary(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double Width,
    double Height,
    double Diagonal,
    double? AverageEdgeLength)
{
    public static SfdpGeometrySummary Create(
        SfdpComponentGraph? graph,
        double[] x,
        double[] y)
        => CreateCore(
            x,
            y,
            graph is null || graph.EdgeCount <= 0
                ? null
                : SfdpSingleLevelLayouter.EstimateNaturalLength(graph, x, y));

    public static SfdpGeometrySummary Create(
        SfdpCsrGraph? graph,
        double[] x,
        double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (graph is null || graph.NodeCount == 0)
        {
            return CreateCore(x, y, null);
        }

        double? averageEdgeLength = null;
        long weightedEdgeCount = 0;
        var weightedEdgeLengthSum = 0.0;

        for (var node = 0; node < graph.NodeCount; node++)
        {
            var start = graph.Offsets[node];
            var end = graph.Offsets[node + 1];
            for (var edgeIndex = start; edgeIndex < end; edgeIndex++)
            {
                var neighbor = graph.Neighbors[edgeIndex];
                if (neighbor <= node)
                {
                    continue;
                }

                var multiplicity = graph.Multiplicities?[edgeIndex] ?? 1;
                var dx = x[node] - x[neighbor];
                var dy = y[node] - y[neighbor];
                weightedEdgeLengthSum += Math.Sqrt((dx * dx) + (dy * dy)) * multiplicity;
                weightedEdgeCount += multiplicity;
            }
        }

        if (weightedEdgeCount > 0)
        {
            averageEdgeLength = weightedEdgeLengthSum / weightedEdgeCount;
        }

        return CreateCore(x, y, averageEdgeLength);
    }

    private static SfdpGeometrySummary CreateCore(
        double[] x,
        double[] y,
        double? averageEdgeLength)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length == 0)
        {
            return new SfdpGeometrySummary(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, null);
        }

        var minX = x[0];
        var maxX = x[0];
        var minY = y[0];
        var maxY = y[0];

        for (var i = 1; i < x.Length; i++)
        {
            minX = Math.Min(minX, x[i]);
            maxX = Math.Max(maxX, x[i]);
            minY = Math.Min(minY, y[i]);
            maxY = Math.Max(maxY, y[i]);
        }

        var width = maxX - minX;
        var height = maxY - minY;
        var diagonal = Math.Sqrt((width * width) + (height * height));
        return new SfdpGeometrySummary(
            minX,
            minY,
            maxX,
            maxY,
            width,
            height,
            diagonal,
            averageEdgeLength);
    }
}
