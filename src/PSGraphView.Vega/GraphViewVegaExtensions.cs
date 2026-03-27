using Newtonsoft.Json;
using PSGraph.Model;
using PSGraph.Model.VegaDataModels;

namespace PSGraphView.Vega;

public static class GraphViewVegaExtensions
{
    public static (IReadOnlyList<NodeRecord> Nodes, IReadOnlyList<LinkRecord> Links) ToForceDirectedRecords(this GraphView graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodeIndexes = graph.Nodes
            .Select((node, index) => new { node.Id, Index = index })
            .ToDictionary(entry => entry.Id, entry => entry.Index, StringComparer.Ordinal);

        var groups = graph.Nodes
            .ToDictionary(node => node.Id, node => ResolveGroup(node.Metadata), StringComparer.Ordinal);

        var nodes = graph.Nodes
            .Select(node => new NodeRecord(
                node.Label,
                ResolveGroup(node.Metadata),
                node.TypeName ?? string.Empty,
                nodeIndexes[node.Id]))
            .ToList();

        var links = graph.Edges
            .Where(edge => nodeIndexes.ContainsKey(edge.SourceId) && nodeIndexes.ContainsKey(edge.TargetId))
            .Select(edge =>
            {
                var sourceIndex = nodeIndexes[edge.SourceId];
                var targetIndex = nodeIndexes[edge.TargetId];
                var value = groups.TryGetValue(edge.TargetId, out var group) ? group : 1;
                return new LinkRecord(sourceIndex, targetIndex, value, sourceIndex, targetIndex);
            })
            .ToList();

        return (nodes, links);
    }

    public static IReadOnlyList<TreeLayoutRecord> ToTreeLayoutRecords(
        this GraphView graph,
        bool useVirtualRoot = false,
        string virtualRootId = "__virtual_root__",
        string virtualRootLabel = "VirtualRoot Node")
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodes = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var indegree = graph.Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);

        foreach (var edge in graph.Edges)
        {
            if (nodes.ContainsKey(edge.SourceId) && indegree.ContainsKey(edge.TargetId))
            {
                indegree[edge.TargetId]++;
            }
        }

        var roots = graph.Nodes
            .Where(node => indegree.TryGetValue(node.Id, out var degree) && degree == 0)
            .ToList();

        if (roots.Count == 0)
        {
            throw new InvalidOperationException("The graph cannot be exported as a tree. No root nodes were found.");
        }

        var effectiveNodes = graph.Nodes.ToList();
        var parentById = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var edge in graph.Edges)
        {
            if (nodes.ContainsKey(edge.SourceId) && nodes.ContainsKey(edge.TargetId))
            {
                parentById[edge.TargetId] = edge.SourceId;
            }
        }

        if (roots.Count > 1)
        {
            if (!useVirtualRoot)
            {
                throw new InvalidOperationException("The graph cannot be exported as a tree. It has multiple roots.");
            }

            var virtualRoot = new GraphViewNode(
                virtualRootId,
                virtualRootLabel,
                null,
                new Dictionary<string, object?>());

            effectiveNodes.Add(virtualRoot);
            foreach (var root in roots)
            {
                parentById[root.Id] = virtualRootId;
            }
        }

        var idsByNodeId = effectiveNodes
            .Select((node, index) => new { node.Id, NumericId = index + 1 })
            .ToDictionary(entry => entry.Id, entry => entry.NumericId, StringComparer.Ordinal);

        return effectiveNodes
            .Select(node =>
            {
                if (parentById.TryGetValue(node.Id, out var parentId) && parentId is not null)
                {
                    return new TreeLayoutRecord(idsByNodeId[node.Id], node.Label, idsByNodeId[parentId]);
                }

                return new TreeLayoutRecord(idsByNodeId[node.Id], node.Label, null);
            })
            .ToList();
    }

    private static int ResolveGroup(IReadOnlyDictionary<string, object?> metadata)
    {
        if (metadata.TryGetValue("group", out var value) && value is not null && int.TryParse(value.ToString(), out var group))
        {
            return group;
        }

        return 1;
    }
}

public sealed record TreeLayoutRecord(
    [property: JsonProperty("id")] int Id,
    [property: JsonProperty("name")] string Name,
    [property: JsonProperty("parent", NullValueHandling = NullValueHandling.Ignore)] int? Parent);