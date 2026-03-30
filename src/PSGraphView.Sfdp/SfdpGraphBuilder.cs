using PSGraph.Model;

namespace PSGraphView.Sfdp;

public static class SfdpGraphBuilder
{
    public static SfdpIndexedGraph BuildIndexed(GraphView graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodes = new List<SfdpIndexedNode>(graph.Nodes.Count);
        var nodeIndexById = new Dictionary<string, int>(graph.Nodes.Count, StringComparer.Ordinal);

        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            if (!nodeIndexById.TryAdd(node.Id, i))
            {
                throw new InvalidOperationException($"Duplicate graph node id '{node.Id}'.");
            }

            nodes.Add(new SfdpIndexedNode(
                i,
                node.Id,
                node.Label,
                node.TypeName,
                new Dictionary<string, object?>(node.Metadata, StringComparer.Ordinal)));
        }

        var edges = new List<SfdpIndexedEdge>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            if (!nodeIndexById.TryGetValue(edge.SourceId, out var source))
            {
                throw new InvalidOperationException($"Edge source '{edge.SourceId}' does not exist in graph nodes.");
            }

            if (!nodeIndexById.TryGetValue(edge.TargetId, out var target))
            {
                throw new InvalidOperationException($"Edge target '{edge.TargetId}' does not exist in graph nodes.");
            }

            edges.Add(new SfdpIndexedEdge(source, target, edge.Weight, edge.Label));
        }

        return new SfdpIndexedGraph(nodes, edges, nodeIndexById);
    }

    public static SfdpCsrGraph BuildUndirectedCsr(
        SfdpIndexedGraph graph,
        bool mergeParallelEdges = true,
        bool removeSelfLoops = true)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var undirectedEdges = new Dictionary<(int A, int B), int>();
        foreach (var edge in graph.DirectedEdges)
        {
            if (removeSelfLoops && edge.Source == edge.Target)
            {
                continue;
            }

            var a = Math.Min(edge.Source, edge.Target);
            var b = Math.Max(edge.Source, edge.Target);
            var key = (a, b);
            var weight = Math.Max(1, edge.Weight);

            if (mergeParallelEdges)
            {
                undirectedEdges.TryGetValue(key, out var multiplicity);
                undirectedEdges[key] = multiplicity + weight;
            }
            else
            {
                if (!undirectedEdges.ContainsKey(key))
                {
                    undirectedEdges[key] = weight;
                }
            }
        }

        var adjacency = new List<(int Neighbor, int Multiplicity)>[graph.NodeCount];
        for (var i = 0; i < adjacency.Length; i++)
        {
            adjacency[i] = [];
        }

        foreach (var edge in undirectedEdges.OrderBy(pair => pair.Key.A).ThenBy(pair => pair.Key.B))
        {
            adjacency[edge.Key.A].Add((edge.Key.B, edge.Value));
            adjacency[edge.Key.B].Add((edge.Key.A, edge.Value));
        }

        var offsets = new int[graph.NodeCount + 1];
        var totalNeighbors = 0;
        for (var node = 0; node < graph.NodeCount; node++)
        {
            adjacency[node].Sort(static (left, right) => left.Neighbor.CompareTo(right.Neighbor));
            offsets[node] = totalNeighbors;
            totalNeighbors += adjacency[node].Count;
        }

        offsets[graph.NodeCount] = totalNeighbors;

        var neighbors = new int[totalNeighbors];
        var multiplicities = new int[totalNeighbors];
        var cursor = 0;

        for (var node = 0; node < graph.NodeCount; node++)
        {
            foreach (var entry in adjacency[node])
            {
                neighbors[cursor] = entry.Neighbor;
                multiplicities[cursor] = entry.Multiplicity;
                cursor++;
            }
        }

        return new SfdpCsrGraph(graph.NodeCount, offsets, neighbors, multiplicities);
    }
}
