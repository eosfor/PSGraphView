namespace PSGraphView.Sfdp;

public sealed record SfdpIndexedNode(
    int Index,
    string Id,
    string Label,
    string? TypeName,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record SfdpIndexedEdge(
    int Source,
    int Target,
    int Weight,
    string? Label);

public sealed class SfdpIndexedGraph
{
    public SfdpIndexedGraph(
        IReadOnlyList<SfdpIndexedNode> nodes,
        IReadOnlyList<SfdpIndexedEdge> directedEdges,
        IReadOnlyDictionary<string, int> nodeIndexById)
    {
        Nodes = nodes;
        DirectedEdges = directedEdges;
        NodeIndexById = nodeIndexById;
    }

    public IReadOnlyList<SfdpIndexedNode> Nodes { get; }
    public IReadOnlyList<SfdpIndexedEdge> DirectedEdges { get; }
    public IReadOnlyDictionary<string, int> NodeIndexById { get; }
    public int NodeCount => Nodes.Count;
    public int EdgeCount => DirectedEdges.Count;
}
