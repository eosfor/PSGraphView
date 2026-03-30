namespace PSGraphView.Sfdp;

internal sealed class SfdpPrismGraph
{
    public SfdpPrismGraph(
        IReadOnlyList<SfdpPrismNodeBox> nodeBoxes,
        IReadOnlyList<SfdpPrismEdge> overlapEdges,
        IReadOnlyList<SfdpPrismEdge> proximityEdges)
    {
        ArgumentNullException.ThrowIfNull(nodeBoxes);
        ArgumentNullException.ThrowIfNull(overlapEdges);
        ArgumentNullException.ThrowIfNull(proximityEdges);

        NodeBoxes = nodeBoxes;
        OverlapEdges = overlapEdges;
        ProximityEdges = proximityEdges;
    }

    public IReadOnlyList<SfdpPrismNodeBox> NodeBoxes { get; }

    public IReadOnlyList<SfdpPrismEdge> OverlapEdges { get; }

    public IReadOnlyList<SfdpPrismEdge> ProximityEdges { get; }
}
