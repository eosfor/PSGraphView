namespace PSGraphView.Sfdp;

public sealed record SfdpConnectedComponentsResult(
    int ComponentCount,
    int[] ComponentByNode,
    IReadOnlyList<int[]> Components);

public static class SfdpConnectedComponents
{
    public static SfdpConnectedComponentsResult Find(SfdpCsrGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var componentByNode = Enumerable.Repeat(-1, graph.NodeCount).ToArray();
        var components = new List<int[]>();
        var queue = new Queue<int>();
        var currentComponent = 0;

        for (var node = 0; node < graph.NodeCount; node++)
        {
            if (componentByNode[node] >= 0)
            {
                continue;
            }

            var nodes = new List<int>();
            componentByNode[node] = currentComponent;
            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                nodes.Add(current);

                foreach (var neighbor in graph.GetNeighbors(current))
                {
                    if (componentByNode[neighbor] >= 0)
                    {
                        continue;
                    }

                    componentByNode[neighbor] = currentComponent;
                    queue.Enqueue(neighbor);
                }
            }

            components.Add(nodes.ToArray());
            currentComponent++;
        }

        return new SfdpConnectedComponentsResult(currentComponent, componentByNode, components);
    }
}
