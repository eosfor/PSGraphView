namespace PSGraphView.Sfdp;

internal static class SfdpNeighborhoodGraphBuilder
{
    private const int TargetNeighbors = 8;

    public static IReadOnlyList<(int Left, int Right)> Build(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length <= 1)
        {
            return Array.Empty<(int Left, int Right)>();
        }

        if (x.Length == 2)
        {
            return [(0, 1)];
        }

        var minX = x.Min();
        var maxX = x.Max();
        var minY = y.Min();
        var maxY = y.Max();
        var spanX = Math.Max(maxX - minX, 1.0);
        var spanY = Math.Max(maxY - minY, 1.0);
        var cellSize = Math.Max(Math.Sqrt((spanX * spanY) / x.Length), 1.0);

        var grid = new Dictionary<CellKey, List<int>>();
        for (var i = 0; i < x.Length; i++)
        {
            var cell = new CellKey(
                (int)Math.Floor((x[i] - minX) / cellSize),
                (int)Math.Floor((y[i] - minY) / cellSize));

            if (!grid.TryGetValue(cell, out var bucket))
            {
                bucket = [];
                grid.Add(cell, bucket);
            }

            bucket.Add(i);
        }

        var edges = new HashSet<(int Left, int Right)>();
        for (var i = 0; i < x.Length; i++)
        {
            var cell = new CellKey(
                (int)Math.Floor((x[i] - minX) / cellSize),
                (int)Math.Floor((y[i] - minY) / cellSize));

            var candidates = CollectCandidates(cell, i, grid, x.Length);
            foreach (var neighbor in candidates
                         .Select(candidate => new Candidate(candidate, SquaredDistance(i, candidate, x, y)))
                         .OrderBy(static candidate => candidate.Distance)
                         .Take(TargetNeighbors)
                         .Select(static candidate => candidate.Index))
            {
                edges.Add(i < neighbor ? (i, neighbor) : (neighbor, i));
            }
        }

        return edges.ToArray();
    }

    private static HashSet<int> CollectCandidates(
        CellKey origin,
        int nodeIndex,
        Dictionary<CellKey, List<int>> grid,
        int nodeCount)
    {
        var candidates = new HashSet<int>();
        var maxRing = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(nodeCount)));

        for (var ring = 0; ring <= maxRing && candidates.Count < TargetNeighbors; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue;
                    }

                    if (!grid.TryGetValue(new CellKey(origin.X + dx, origin.Y + dy), out var bucket))
                    {
                        continue;
                    }

                    foreach (var candidate in bucket)
                    {
                        if (candidate != nodeIndex)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }
        }

        return candidates;
    }

    private static double SquaredDistance(int left, int right, double[] x, double[] y)
    {
        var dx = x[left] - x[right];
        var dy = y[left] - y[right];
        return (dx * dx) + (dy * dy);
    }

    private readonly record struct CellKey(int X, int Y);

    private readonly record struct Candidate(int Index, double Distance);
}
