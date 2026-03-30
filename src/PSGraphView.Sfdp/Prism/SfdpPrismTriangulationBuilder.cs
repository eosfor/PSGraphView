namespace PSGraphView.Sfdp;

internal static class SfdpPrismTriangulationBuilder
{
    private const double RelativeEpsilon = 1e-12;

    public static SfdpPrismEdge[] BuildEdges(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length <= 1)
        {
            return [];
        }

        if (x.Length == 2)
        {
            return [new SfdpPrismEdge(0, 1)];
        }

        var edges = BuildDelaunayEdges(x, y);
        if (edges.Length > 0)
        {
            return edges;
        }

        return BuildCollinearFallbackEdges(x, y);
    }

    private static SfdpPrismEdge[] BuildDelaunayEdges(double[] x, double[] y)
    {
        if (IsCollinear(x, y))
        {
            return [];
        }

        var points = BuildUniquePoints(x, y);
        if (points.Count < 3)
        {
            return [];
        }

        var triangles = BuildTriangles(points);
        if (triangles.Count == 0)
        {
            return [];
        }

        var edges = new HashSet<SfdpPrismEdge>();
        foreach (var triangle in triangles)
        {
            AddEdge(edges, points[triangle.A].OriginalIndex, points[triangle.B].OriginalIndex);
            AddEdge(edges, points[triangle.B].OriginalIndex, points[triangle.C].OriginalIndex);
            AddEdge(edges, points[triangle.C].OriginalIndex, points[triangle.A].OriginalIndex);
        }

        return edges
            .OrderBy(static edge => edge.Left)
            .ThenBy(static edge => edge.Right)
            .ToArray();
    }

    private static List<TriPoint> BuildUniquePoints(double[] x, double[] y)
    {
        var points = new List<TriPoint>(x.Length);
        var firstIndexByCoordinate = new Dictionary<(double X, double Y), int>();

        for (var i = 0; i < x.Length; i++)
        {
            var key = (x[i], y[i]);
            if (firstIndexByCoordinate.ContainsKey(key))
            {
                continue;
            }

            firstIndexByCoordinate.Add(key, i);
            points.Add(new TriPoint(x[i], y[i], i));
        }

        return points;
    }

    private static List<Triangle> BuildTriangles(List<TriPoint> inputPoints)
    {
        var points = new List<TriPoint>(inputPoints.Count + 3);
        points.AddRange(inputPoints);

        var minX = inputPoints.Min(static point => point.X);
        var maxX = inputPoints.Max(static point => point.X);
        var minY = inputPoints.Min(static point => point.Y);
        var maxY = inputPoints.Max(static point => point.Y);
        var dx = maxX - minX;
        var dy = maxY - minY;
        var delta = Math.Max(Math.Max(dx, dy), 1.0);
        var midX = (minX + maxX) * 0.5;
        var midY = (minY + maxY) * 0.5;

        var superA = points.Count;
        points.Add(new TriPoint(midX - (20.0 * delta), midY - delta, -1));
        var superB = points.Count;
        points.Add(new TriPoint(midX, midY + (20.0 * delta), -1));
        var superC = points.Count;
        points.Add(new TriPoint(midX + (20.0 * delta), midY - delta, -1));

        var triangles = new List<Triangle>
        {
            CreateTriangle(superA, superB, superC, points)
        };

        for (var pointIndex = 0; pointIndex < inputPoints.Count; pointIndex++)
        {
            var boundaryCounts = new Dictionary<Edge, int>();
            var keptTriangles = new List<Triangle>(triangles.Count);

            foreach (var triangle in triangles)
            {
                if (CircumcircleContains(triangle, points[pointIndex]))
                {
                    CountBoundaryEdge(boundaryCounts, new Edge(triangle.A, triangle.B));
                    CountBoundaryEdge(boundaryCounts, new Edge(triangle.B, triangle.C));
                    CountBoundaryEdge(boundaryCounts, new Edge(triangle.C, triangle.A));
                }
                else
                {
                    keptTriangles.Add(triangle);
                }
            }

            triangles = keptTriangles;
            foreach (var pair in boundaryCounts)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                triangles.Add(CreateTriangle(pair.Key.A, pair.Key.B, pointIndex, points));
            }
        }

        return triangles
            .Where(triangle => triangle.A < inputPoints.Count && triangle.B < inputPoints.Count && triangle.C < inputPoints.Count)
            .ToList();
    }

    private static Triangle CreateTriangle(int a, int b, int c, IReadOnlyList<TriPoint> points)
    {
        if (Orientation(points[a], points[b], points[c]) < 0.0)
        {
            (b, c) = (c, b);
        }

        var ax = points[a].X;
        var ay = points[a].Y;
        var bx = points[b].X;
        var by = points[b].Y;
        var cx = points[c].X;
        var cy = points[c].Y;

        var determinant = 2.0 * ((ax * (by - cy)) + (bx * (cy - ay)) + (cx * (ay - by)));
        if (Math.Abs(determinant) <= RelativeEpsilon)
        {
            var centerX = (ax + bx + cx) / 3.0;
            var centerY = (ay + by + cy) / 3.0;
            var radiusSquared = Math.Max(
                SquaredDistance(centerX, centerY, ax, ay),
                Math.Max(
                    SquaredDistance(centerX, centerY, bx, by),
                    SquaredDistance(centerX, centerY, cx, cy)));
            return new Triangle(a, b, c, centerX, centerY, radiusSquared);
        }

        var ax2ay2 = (ax * ax) + (ay * ay);
        var bx2by2 = (bx * bx) + (by * by);
        var cx2cy2 = (cx * cx) + (cy * cy);
        var centerXExact = ((ax2ay2 * (by - cy)) + (bx2by2 * (cy - ay)) + (cx2cy2 * (ay - by))) / determinant;
        var centerYExact = ((ax2ay2 * (cx - bx)) + (bx2by2 * (ax - cx)) + (cx2cy2 * (bx - ax))) / determinant;
        var radiusSquaredExact = SquaredDistance(centerXExact, centerYExact, ax, ay);
        return new Triangle(a, b, c, centerXExact, centerYExact, radiusSquaredExact);
    }

    private static bool CircumcircleContains(Triangle triangle, TriPoint point)
    {
        var distanceSquared = SquaredDistance(triangle.CenterX, triangle.CenterY, point.X, point.Y);
        var tolerance = triangle.RadiusSquared * 1e-9 + 1e-12;
        return distanceSquared < triangle.RadiusSquared - tolerance;
    }

    private static void CountBoundaryEdge(Dictionary<Edge, int> boundaryCounts, Edge edge)
    {
        var normalized = edge.Normalize();
        boundaryCounts.TryGetValue(normalized, out var count);
        boundaryCounts[normalized] = count + 1;
    }

    private static void AddEdge(HashSet<SfdpPrismEdge> edges, int left, int right)
    {
        if (left == right || left < 0 || right < 0)
        {
            return;
        }

        edges.Add(new SfdpPrismEdge(left, right).Normalize());
    }

    private static bool IsCollinear(double[] x, double[] y)
    {
        var first = 0;
        var second = -1;
        for (var i = 1; i < x.Length; i++)
        {
            if (x[i] != x[first] || y[i] != y[first])
            {
                second = i;
                break;
            }
        }

        if (second < 0)
        {
            return true;
        }

        var ax = x[first];
        var ay = y[first];
        var bx = x[second];
        var by = y[second];
        var minX = x.Min();
        var maxX = x.Max();
        var minY = y.Min();
        var maxY = y.Max();
        var span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1.0);
        var tolerance = span * RelativeEpsilon;

        for (var i = 0; i < x.Length; i++)
        {
            var area2 = ((bx - ax) * (y[i] - ay)) - ((by - ay) * (x[i] - ax));
            if (Math.Abs(area2) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static SfdpPrismEdge[] BuildCollinearFallbackEdges(double[] x, double[] y)
    {
        var indices = Enumerable.Range(0, x.Length).ToArray();
        var orderByY = x.Length > 1 && x[0] == x[1];

        Array.Sort(indices, (left, right) =>
        {
            var primary = orderByY ? y[left].CompareTo(y[right]) : x[left].CompareTo(x[right]);
            return primary != 0 ? primary : left.CompareTo(right);
        });

        var edges = new SfdpPrismEdge[Math.Max(0, x.Length - 1)];
        for (var i = 1; i < indices.Length; i++)
        {
            edges[i - 1] = new SfdpPrismEdge(indices[i - 1], indices[i]).Normalize();
        }

        return edges;
    }

    private static double Orientation(TriPoint a, TriPoint b, TriPoint c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static double SquaredDistance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct TriPoint(double X, double Y, int OriginalIndex);

    private readonly record struct Edge(int A, int B)
    {
        public Edge Normalize() => A <= B ? this : new Edge(B, A);
    }

    private readonly record struct Triangle(int A, int B, int C, double CenterX, double CenterY, double RadiusSquared);
}
