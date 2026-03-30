namespace PSGraphView.Sfdp;

internal static class SfdpPrismModelBuilder
{
    private const double MachineAccuracy = 1.0e-16;
    private const double ExpandMax = 1.5;
    private const double ExpandMin = 1.0;

    [Flags]
    private enum EdgeSources
    {
        None = 0,
        Proximity = 1,
        Overlap = 2
    }

    public static SfdpPrismStressSystem Build(
        SfdpPrismGraph prismGraph,
        bool neighborhoodOnly,
        double minDistance,
        double expansionWeightMultiplier)
    {
        ArgumentNullException.ThrowIfNull(prismGraph);

        var edgeMap = new Dictionary<EdgeEndpoints, EdgeSources>(prismGraph.ProximityEdges.Count + (neighborhoodOnly ? 0 : prismGraph.OverlapEdges.Count));

        foreach (var edge in prismGraph.ProximityEdges)
        {
            edgeMap.TryAdd(EdgeEndpoints.FromEdge(edge), EdgeSources.Proximity);
        }

        if (!neighborhoodOnly)
        {
            foreach (var edge in prismGraph.OverlapEdges)
            {
                var normalized = EdgeEndpoints.FromEdge(edge);
                edgeMap.TryGetValue(normalized, out var existingSources);
                edgeMap[normalized] = existingSources | EdgeSources.Overlap;
            }
        }

        var lwEntries = new List<(int Row, int Column, double Value)>();
        var lwdEntries = new List<(int Row, int Column, double Value)>();
        var diagW = new double[prismGraph.NodeBoxes.Count];
        var diagD = new double[prismGraph.NodeBoxes.Count];

        var proximityOnlyEdgeCount = 0;
        var overlapOnlyEdgeCount = 0;
        var sharedEdgeCount = 0;
        var expandEdgeCount = 0;
        var shrinkEdgeCount = 0;
        var maxOverlapFactor = 0.0;
        var minOverlapFactor = double.PositiveInfinity;

        foreach (var pair in edgeMap)
        {
            var edge = pair.Key;
            switch (pair.Value)
            {
                case EdgeSources.Proximity:
                    proximityOnlyEdgeCount++;
                    break;
                case EdgeSources.Overlap:
                    overlapOnlyEdgeCount++;
                    break;
                case EdgeSources.Proximity | EdgeSources.Overlap:
                    sharedEdgeCount++;
                    break;
            }

            var idealDistanceResult = GetIdealDistance(
                prismGraph.NodeBoxes[edge.Left],
                prismGraph.NodeBoxes[edge.Right],
                minDistance);

            maxOverlapFactor = Math.Max(maxOverlapFactor, idealDistanceResult.OverlapFactor);
            minOverlapFactor = Math.Min(minOverlapFactor, idealDistanceResult.OverlapFactor);

            if (idealDistanceResult.SignedIdealDistance > 0.0)
            {
                expandEdgeCount++;
            }
            else if (idealDistanceResult.SignedIdealDistance < 0.0)
            {
                shrinkEdgeCount++;
            }

            var idealDistance = Math.Max(Math.Abs(idealDistanceResult.SignedIdealDistance), minDistance);
            var weight = 1.0 / (idealDistance * idealDistance);
            if (idealDistanceResult.SignedIdealDistance > 0.0)
            {
                weight *= expansionWeightMultiplier;
            }

            lwEntries.Add((edge.Left, edge.Right, -weight));
            lwEntries.Add((edge.Right, edge.Left, -weight));

            var weightedDistance = weight * idealDistance;
            lwdEntries.Add((edge.Left, edge.Right, -weightedDistance));
            lwdEntries.Add((edge.Right, edge.Left, -weightedDistance));

            diagW[edge.Left] += weight;
            diagW[edge.Right] += weight;
            diagD[edge.Left] += weightedDistance;
            diagD[edge.Right] += weightedDistance;
        }

        for (var i = 0; i < prismGraph.NodeBoxes.Count; i++)
        {
            if (diagW[i] > 0.0)
            {
                lwEntries.Add((i, i, diagW[i]));
                lwdEntries.Add((i, i, diagD[i]));
            }
        }

        return new SfdpPrismStressSystem(
            SfdpSparseMatrix.FromCoordinateEntries(prismGraph.NodeBoxes.Count, prismGraph.NodeBoxes.Count, lwEntries),
            SfdpSparseMatrix.FromCoordinateEntries(prismGraph.NodeBoxes.Count, prismGraph.NodeBoxes.Count, lwdEntries),
            edgeMap.Count,
            proximityOnlyEdgeCount,
            overlapOnlyEdgeCount,
            sharedEdgeCount,
            expandEdgeCount,
            shrinkEdgeCount,
            maxOverlapFactor,
            minOverlapFactor);
    }

    private static IdealDistanceResult GetIdealDistance(
        SfdpPrismNodeBox left,
        SfdpPrismNodeBox right,
        double minDistance)
    {
        var dxSigned = left.CenterX - right.CenterX;
        var dySigned = left.CenterY - right.CenterY;
        var distance = Math.Max(Math.Sqrt((dxSigned * dxSigned) + (dySigned * dySigned)), minDistance);
        var dx = Math.Abs(dxSigned);
        var dy = Math.Abs(dySigned);
        var totalWidth = left.HalfWidth + right.HalfWidth;
        var totalHeight = left.HalfHeight + right.HalfHeight;

        if (dx < MachineAccuracy * totalWidth && dy < MachineAccuracy * totalHeight)
        {
            return new IdealDistanceResult(
                Math.Sqrt((totalWidth * totalWidth) + (totalHeight * totalHeight)),
                2.0);
        }

        double overlapFactor;
        if (dx < MachineAccuracy * totalWidth)
        {
            overlapFactor = totalHeight / Math.Max(dy, minDistance);
        }
        else if (dy < MachineAccuracy * totalHeight)
        {
            overlapFactor = totalWidth / Math.Max(dx, minDistance);
        }
        else
        {
            overlapFactor = Math.Min(totalWidth / dx, totalHeight / dy);
        }

        if (overlapFactor > 1.0)
        {
            overlapFactor = Math.Max(overlapFactor, 1.001);
        }

        var clampedFactor = Math.Min(ExpandMax, overlapFactor);
        clampedFactor = Math.Max(ExpandMin, clampedFactor);

        if (clampedFactor > 1.0)
        {
            return new IdealDistanceResult(distance * clampedFactor, overlapFactor);
        }

        return new IdealDistanceResult(-(distance * clampedFactor), overlapFactor);
    }

    private readonly record struct IdealDistanceResult(
        double SignedIdealDistance,
        double OverlapFactor);

    private readonly record struct EdgeEndpoints(int Left, int Right)
    {
        public static EdgeEndpoints FromEdge(SfdpPrismEdge edge)
        {
            var normalized = edge.Normalize();
            return new EdgeEndpoints(normalized.Left, normalized.Right);
        }
    }
}
