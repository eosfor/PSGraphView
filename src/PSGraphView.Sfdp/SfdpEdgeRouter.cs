using System.Globalization;
using PSGraph.Model;

namespace PSGraphView.Sfdp;

internal static class SfdpEdgeRouter
{
    internal readonly record struct RoutedPoint(double X, double Y);

    internal sealed record RoutedEdge(
        int SourceIndex,
        int TargetIndex,
        string PathData,
        IReadOnlyList<RoutedPoint> Points);

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
            var points = BuildRoutePoints(sourceIndex, targetIndex, hasReverse, x, y, options);
            routed.Add(new RoutedEdge(sourceIndex, targetIndex, BuildPathData(points), points));
        }

        return routed;
    }

    internal static SfdpBoundingBox ComputeBounds(IReadOnlyList<RoutedPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 4 || ((points.Count - 1) % 3) != 0)
        {
            throw new ArgumentException("Expected routed points to contain one or more cubic Bezier segments.", nameof(points));
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        for (var segmentStart = 0; segmentStart <= points.Count - 4; segmentStart += 3)
        {
            var xValues = GetCubicExtrema(
                points[segmentStart].X,
                points[segmentStart + 1].X,
                points[segmentStart + 2].X,
                points[segmentStart + 3].X);
            var yValues = GetCubicExtrema(
                points[segmentStart].Y,
                points[segmentStart + 1].Y,
                points[segmentStart + 2].Y,
                points[segmentStart + 3].Y);

            minX = Math.Min(minX, xValues.Min());
            minY = Math.Min(minY, yValues.Min());
            maxX = Math.Max(maxX, xValues.Max());
            maxY = Math.Max(maxY, yValues.Max());
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }

    internal static RoutedPoint[] BuildRoutePoints(
        int sourceIndex,
        int targetIndex,
        bool hasReverse,
        double[] x,
        double[] y,
        SfdpOptions options)
    {
        if (sourceIndex == targetIndex)
        {
            return BuildSelfLoopPoints(x[sourceIndex], y[sourceIndex], options.NodeRadius);
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
            return
            [
                new RoutedPoint(startX, startY),
                new RoutedPoint(control1X, control1Y),
                new RoutedPoint(control2X, control2Y),
                new RoutedPoint(endX, endY)
            ];
        }

        var nx = -uy;
        var ny = ux;
        var curvature = Math.Max(options.NodeRadius * 12.0, distance * 0.30);
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

        return
        [
            new RoutedPoint(startXCurve, startYCurve),
            new RoutedPoint(curveControl1X, curveControl1Y),
            new RoutedPoint(curveControl2X, curveControl2Y),
            new RoutedPoint(endXCurve, endYCurve)
        ];
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

    internal static string BuildPathData(IReadOnlyList<RoutedPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 4 || ((points.Count - 1) % 3) != 0)
        {
            throw new ArgumentException("Expected routed points to contain one or more cubic Bezier segments.", nameof(points));
        }

        var builder = new System.Text.StringBuilder();
        builder.Append(Invariant($"M {points[0].X:0.###} {points[0].Y:0.###}"));
        for (var segmentStart = 0; segmentStart <= points.Count - 4; segmentStart += 3)
        {
            builder.Append(Invariant($" C {points[segmentStart + 1].X:0.###} {points[segmentStart + 1].Y:0.###} {points[segmentStart + 2].X:0.###} {points[segmentStart + 2].Y:0.###} {points[segmentStart + 3].X:0.###} {points[segmentStart + 3].Y:0.###}"));
        }

        return builder.ToString();
    }

    private static RoutedPoint[] BuildSelfLoopPoints(double x, double y, double nodeRadius)
    {
        var startX = x + (nodeRadius * 0.83);
        var startY = y - (nodeRadius * 1.14);
        var firstControl1X = x + (nodeRadius * 9.75);
        var firstControl1Y = y - (nodeRadius * 12.5);
        var firstControl2X = x + (nodeRadius * 26.0);
        var firstControl2Y = y - (nodeRadius * 12.1);
        var middleX = x + (nodeRadius * 26.0);
        var middleY = y;
        var secondControl1X = x + (nodeRadius * 26.0);
        var secondControl1Y = y + (nodeRadius * 11.7);
        var secondControl2X = x + (nodeRadius * 10.8);
        var secondControl2Y = y + (nodeRadius * 12.4);
        var endX = x + (nodeRadius * 1.69);
        var endY = y + (nodeRadius * 2.15);

        return
        [
            new RoutedPoint(startX, startY),
            new RoutedPoint(firstControl1X, firstControl1Y),
            new RoutedPoint(firstControl2X, firstControl2Y),
            new RoutedPoint(middleX, middleY),
            new RoutedPoint(secondControl1X, secondControl1Y),
            new RoutedPoint(secondControl2X, secondControl2Y),
            new RoutedPoint(endX, endY)
        ];
    }

    private static string Invariant(FormattableString value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<double> GetCubicExtrema(double p0, double p1, double p2, double p3)
    {
        var values = new List<double>(4)
        {
            EvaluateBezier(p0, p1, p2, p3, 0.0),
            EvaluateBezier(p0, p1, p2, p3, 1.0)
        };

        foreach (var t in GetDerivativeRoots(p0, p1, p2, p3))
        {
            if (t > 0.0 && t < 1.0)
            {
                values.Add(EvaluateBezier(p0, p1, p2, p3, t));
            }
        }

        return values;
    }

    private static IEnumerable<double> GetDerivativeRoots(double p0, double p1, double p2, double p3)
    {
        var a = -p0 + (3.0 * p1) - (3.0 * p2) + p3;
        var b = (3.0 * p0) - (6.0 * p1) + (3.0 * p2);
        var c = (-3.0 * p0) + (3.0 * p1);

        var quadraticA = 3.0 * a;
        var quadraticB = 2.0 * b;
        var quadraticC = c;

        if (Math.Abs(quadraticA) <= double.Epsilon)
        {
            if (Math.Abs(quadraticB) <= double.Epsilon)
            {
                yield break;
            }

            yield return -quadraticC / quadraticB;
            yield break;
        }

        var discriminant = (quadraticB * quadraticB) - (4.0 * quadraticA * quadraticC);
        if (discriminant < 0.0)
        {
            yield break;
        }

        if (Math.Abs(discriminant) <= double.Epsilon)
        {
            yield return -quadraticB / (2.0 * quadraticA);
            yield break;
        }

        var sqrtDiscriminant = Math.Sqrt(discriminant);
        yield return (-quadraticB + sqrtDiscriminant) / (2.0 * quadraticA);
        yield return (-quadraticB - sqrtDiscriminant) / (2.0 * quadraticA);
    }

    private static double EvaluateBezier(double p0, double p1, double p2, double p3, double t)
    {
        var oneMinusT = 1.0 - t;
        return (oneMinusT * oneMinusT * oneMinusT * p0) +
               (3.0 * oneMinusT * oneMinusT * t * p1) +
               (3.0 * oneMinusT * t * t * p2) +
               (t * t * t * p3);
    }
}
