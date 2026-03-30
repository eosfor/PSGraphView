using System.Globalization;
using PSGraph.Model;

namespace PSGraphView.Sfdp;

internal static class SfdpEdgeRouter
{
    internal sealed record RoutedEdge(
        int SourceIndex,
        int TargetIndex,
        string PathData);

    public static IReadOnlyList<RoutedEdge> RouteEdges(
        GraphView graph,
        double[] x,
        double[] y,
        SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(options);

        var nodeIndexById = graph.Nodes
            .Select((node, index) => (node.Id, index))
            .ToDictionary(static item => item.Id, static item => item.index, StringComparer.Ordinal);

        var edgeKeys = new HashSet<(int Source, int Target)>();
        foreach (var edge in graph.Edges)
        {
            edgeKeys.Add((nodeIndexById[edge.SourceId], nodeIndexById[edge.TargetId]));
        }

        var routed = new List<RoutedEdge>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            var sourceIndex = nodeIndexById[edge.SourceId];
            var targetIndex = nodeIndexById[edge.TargetId];
            var hasReverse = edgeKeys.Contains((targetIndex, sourceIndex));
            routed.Add(new RoutedEdge(sourceIndex, targetIndex, RouteEdge(sourceIndex, targetIndex, hasReverse, x, y, options)));
        }

        return routed;
    }

    private static string RouteEdge(
        int sourceIndex,
        int targetIndex,
        bool hasReverse,
        double[] x,
        double[] y,
        SfdpOptions options)
    {
        if (sourceIndex == targetIndex)
        {
            return BuildSelfLoop(x[sourceIndex], y[sourceIndex], options.NodeRadius);
        }

        var sx = x[sourceIndex];
        var sy = y[sourceIndex];
        var tx = x[targetIndex];
        var ty = y[targetIndex];

        var dx = tx - sx;
        var dy = ty - sy;
        var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.0001);
        var ux = dx / distance;
        var uy = dy / distance;

        var startClearance = options.NodeRadius + 1.0;
        var endClearance = options.NodeRadius + (options.ShowArrows ? 6.0 : 1.0);
        (startClearance, endClearance) = ClampClearances(distance, startClearance, endClearance);

        if (!hasReverse)
        {
            var startX = sx + ux * startClearance;
            var startY = sy + uy * startClearance;
            var endX = tx - ux * endClearance;
            var endY = ty - uy * endClearance;
            var control1X = startX + ((endX - startX) / 3.0);
            var control1Y = startY + ((endY - startY) / 3.0);
            var control2X = startX + (2.0 * (endX - startX) / 3.0);
            var control2Y = startY + (2.0 * (endY - startY) / 3.0);
            return Invariant($"M {startX:0.###} {startY:0.###} C {control1X:0.###} {control1Y:0.###} {control2X:0.###} {control2Y:0.###} {endX:0.###} {endY:0.###}");
        }

        var nx = -uy;
        var ny = ux;
        var curvature = Math.Max(options.NodeRadius * 4.0, distance * 0.12);
        var direction = sourceIndex < targetIndex ? 1.0 : -1.0;
        var controlX = (sx + tx) * 0.5 + nx * curvature * direction;
        var controlY = (sy + ty) * 0.5 + ny * curvature * direction;

        var startTangentX = controlX - sx;
        var startTangentY = controlY - sy;
        var startTangentLength = Math.Max(Math.Sqrt(startTangentX * startTangentX + startTangentY * startTangentY), 0.0001);
        startTangentX /= startTangentLength;
        startTangentY /= startTangentLength;

        var endTangentX = tx - controlX;
        var endTangentY = ty - controlY;
        var endTangentLength = Math.Max(Math.Sqrt(endTangentX * endTangentX + endTangentY * endTangentY), 0.0001);
        endTangentX /= endTangentLength;
        endTangentY /= endTangentLength;

        var startXCurve = sx + startTangentX * startClearance;
        var startYCurve = sy + startTangentY * startClearance;
        var endXCurve = tx - endTangentX * endClearance;
        var endYCurve = ty - endTangentY * endClearance;
        var curveControl1X = startXCurve + (controlX - startXCurve) * (2.0 / 3.0);
        var curveControl1Y = startYCurve + (controlY - startYCurve) * (2.0 / 3.0);
        var curveControl2X = endXCurve + (controlX - endXCurve) * (2.0 / 3.0);
        var curveControl2Y = endYCurve + (controlY - endYCurve) * (2.0 / 3.0);

        return Invariant($"M {startXCurve:0.###} {startYCurve:0.###} C {curveControl1X:0.###} {curveControl1Y:0.###} {curveControl2X:0.###} {curveControl2Y:0.###} {endXCurve:0.###} {endYCurve:0.###}");
    }

    private static (double Start, double End) ClampClearances(double distance, double startClearance, double endClearance)
    {
        var totalClearance = startClearance + endClearance;
        var maxTotalClearance = Math.Max(distance * 0.95, 0.001);
        if (totalClearance <= maxTotalClearance || totalClearance <= 0.0)
        {
            return (startClearance, endClearance);
        }

        var scale = maxTotalClearance / totalClearance;
        return (startClearance * scale, endClearance * scale);
    }

    private static string BuildSelfLoop(double x, double y, double nodeRadius)
    {
        var loopRadius = nodeRadius * 2.8;
        var startX = x + nodeRadius * 0.6;
        var startY = y - nodeRadius * 0.8;
        var endX = x - nodeRadius * 0.2;
        var endY = y - nodeRadius * 1.1;
        var control1X = x + loopRadius;
        var control1Y = y - loopRadius * 1.8;
        var control2X = x - loopRadius;
        var control2Y = y - loopRadius * 1.8;
        return Invariant($"M {startX:0.###} {startY:0.###} C {control1X:0.###} {control1Y:0.###} {control2X:0.###} {control2Y:0.###} {endX:0.###} {endY:0.###}");
    }

    private static string Invariant(FormattableString value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
