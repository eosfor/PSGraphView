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
        if (points.Count != 4)
        {
            throw new ArgumentException("Expected exactly four routed points.", nameof(points));
        }

        var xValues = GetCubicExtrema(points[0].X, points[1].X, points[2].X, points[3].X);
        var yValues = GetCubicExtrema(points[0].Y, points[1].Y, points[2].Y, points[3].Y);

        return new SfdpBoundingBox(
            xValues.Min(),
            yValues.Min(),
            xValues.Max(),
            yValues.Max());
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
        if (points.Count != 4)
        {
            throw new ArgumentException("Expected exactly four routed points.", nameof(points));
        }

        return Invariant($"M {points[0].X:0.###} {points[0].Y:0.###} C {points[1].X:0.###} {points[1].Y:0.###} {points[2].X:0.###} {points[2].Y:0.###} {points[3].X:0.###} {points[3].Y:0.###}");
    }

    private static RoutedPoint[] BuildSelfLoopPoints(double x, double y, double nodeRadius)
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
        return
        [
            new RoutedPoint(startX, startY),
            new RoutedPoint(control1X, control1Y),
            new RoutedPoint(control2X, control2Y),
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
