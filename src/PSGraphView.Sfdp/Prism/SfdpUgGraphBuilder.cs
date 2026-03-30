namespace PSGraphView.Sfdp;

internal static class SfdpUgGraphBuilder
{
    public static SfdpUgNode[] Build(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var nodeCount = x.Length;
        if (nodeCount == 0)
        {
            return [];
        }

        if (nodeCount == 1)
        {
            return [new SfdpUgNode(0, [0])];
        }

        if (nodeCount == 2)
        {
            return
            [
                new SfdpUgNode(0, [0, 1]),
                new SfdpUgNode(1, [1, 0])
            ];
        }

        var adjacency = BuildDelaunayAdjacency(x, y);

        for (var i = 0; i < nodeCount; i++)
        {
            for (var j = 1; j < adjacency[i].Count; )
            {
                var neighborJ = adjacency[i][j];
                var distanceIJ = SquaredDistance(x[i], y[i], x[neighborJ], y[neighborJ]);
                var removed = false;

                for (var k = 1; k < adjacency[i].Count && !removed; k++)
                {
                    var neighborK = adjacency[i][k];
                    var distanceIK = SquaredDistance(x[i], y[i], x[neighborK], y[neighborK]);
                    if (distanceIK < distanceIJ)
                    {
                        var distanceJK = SquaredDistance(x[neighborJ], y[neighborJ], x[neighborK], y[neighborK]);
                        if (distanceJK < distanceIJ)
                        {
                            adjacency[i][j] = adjacency[i][adjacency[i].Count - 1];
                            adjacency[i].RemoveAt(adjacency[i].Count - 1);
                            RemoveEdge(adjacency, neighborJ, i);
                            removed = true;
                        }
                    }
                }

                if (!removed)
                {
                    j++;
                }
            }
        }

        return adjacency
            .Select((edges, index) => new SfdpUgNode(index, edges))
            .ToArray();
    }

    private static List<int>[] BuildDelaunayAdjacency(double[] x, double[] y)
    {
        var adjacency = new List<int>[x.Length];
        for (var i = 0; i < adjacency.Length; i++)
        {
            adjacency[i] = [i];
        }

        foreach (var edge in SfdpPrismTriangulationBuilder.BuildEdges(x, y))
        {
            if (!adjacency[edge.Left].Contains(edge.Right))
            {
                adjacency[edge.Left].Add(edge.Right);
            }

            if (!adjacency[edge.Right].Contains(edge.Left))
            {
                adjacency[edge.Right].Add(edge.Left);
            }
        }

        return adjacency;
    }

    private static void RemoveEdge(IReadOnlyList<List<int>> adjacency, int source, int destination)
    {
        for (var i = 1; i < adjacency[source].Count; i++)
        {
            if (adjacency[source][i] == destination)
            {
                adjacency[source].RemoveAt(i);
                break;
            }
        }
    }

    private static double SquaredDistance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return (dx * dx) + (dy * dy);
    }
}

internal sealed record SfdpUgNode(int Index, IReadOnlyList<int> Edges);
