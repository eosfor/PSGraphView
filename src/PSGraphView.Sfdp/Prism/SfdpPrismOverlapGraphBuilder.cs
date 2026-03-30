namespace PSGraphView.Sfdp;

internal static class SfdpPrismOverlapGraphBuilder
{
    public static SfdpPrismEdge[] Build(IReadOnlyList<SfdpPrismNodeBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        return BuildInternal(boxes, checkOverlapOnly: false).Edges;
    }

    public static bool HasAnyOverlap(IReadOnlyList<SfdpPrismNodeBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        return BuildInternal(boxes, checkOverlapOnly: true).HasOverlap;
    }

    private static BuildResult BuildInternal(IReadOnlyList<SfdpPrismNodeBox> boxes, bool checkOverlapOnly)
    {
        if (boxes.Count <= 1)
        {
            return new BuildResult([], false);
        }

        var xScanPoints = new ScanPoint[boxes.Count * 2];
        var yScanPoints = new ScanPoint[boxes.Count * 2];

        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            xScanPoints[2 * i] = new ScanPoint(i, box.CenterX - box.HalfWidth);
            xScanPoints[(2 * i) + 1] = new ScanPoint(i + boxes.Count, box.CenterX + box.HalfWidth);
            yScanPoints[i] = new ScanPoint(i, box.CenterY - box.HalfHeight);
            yScanPoints[i + boxes.Count] = new ScanPoint(i + boxes.Count, box.CenterY + box.HalfHeight);
        }

        Array.Sort(xScanPoints, ScanPointComparer.Instance);

        // Match Graphviz get_overlap_graph(): sweep in X, keep a sorted
        // structure of Y endpoints, and when the X interval closes remove only
        // the upper Y endpoint. This preserves the same dense overlap graph.
        var yTree = new List<ScanPoint>(boxes.Count * 2);
        var overlaps = checkOverlapOnly ? null : new HashSet<SfdpPrismEdge>();

        foreach (var point in xScanPoints)
        {
            var node = point.Node % boxes.Count;
            if (point.Node < boxes.Count)
            {
                InsertSorted(yTree, yScanPoints[node]);
                InsertSorted(yTree, yScanPoints[node + boxes.Count]);
                continue;
            }

            var closePoint = yScanPoints[node + boxes.Count];
            var closeIndex = IndexOf(yTree, closePoint);
            if (closeIndex < 0)
            {
                continue;
            }

            var start = yScanPoints[node].Position;
            var end = closePoint.Position;
            for (var i = closeIndex - 1; i >= 0; i--)
            {
                var neighbor = yTree[i].Node % boxes.Count;
                if (neighbor == node)
                {
                    continue;
                }

                var neighborStart = yScanPoints[neighbor].Position;
                var neighborEnd = yScanPoints[neighbor + boxes.Count].Position;
                var centersDistance = Math.Abs((0.5 * (start + end)) - (0.5 * (neighborStart + neighborEnd)));
                var halfSpansSum = (0.5 * (end - start)) + (0.5 * (neighborEnd - neighborStart));
                if (centersDistance >= halfSpansSum)
                {
                    continue;
                }

                if (checkOverlapOnly)
                {
                    return new BuildResult([], true);
                }

                overlaps!.Add(new SfdpPrismEdge(neighbor, node, IsOverlapConstraint: true).Normalize());
            }

            yTree.RemoveAt(closeIndex);
        }

        return new BuildResult(overlaps?.ToArray() ?? [], overlaps is { Count: > 0 });
    }

    private static void InsertSorted(List<ScanPoint> points, ScanPoint point)
    {
        var index = points.BinarySearch(point, ScanPointComparer.Instance);
        if (index < 0)
        {
            index = ~index;
        }

        points.Insert(index, point);
    }

    private static int IndexOf(List<ScanPoint> points, ScanPoint point)
        => points.BinarySearch(point, ScanPointComparer.Instance);

    private readonly record struct ScanPoint(int Node, double Position);

    private sealed class ScanPointComparer : IComparer<ScanPoint>
    {
        public static ScanPointComparer Instance { get; } = new();

        public int Compare(ScanPoint left, ScanPoint right)
        {
            var positionCompare = left.Position.CompareTo(right.Position);
            return positionCompare != 0 ? positionCompare : left.Node.CompareTo(right.Node);
        }
    }

    private readonly record struct BuildResult(SfdpPrismEdge[] Edges, bool HasOverlap);
}
