namespace PSGraphView.Sfdp;

public sealed class SfdpCsrGraph
{
    public SfdpCsrGraph(
        int nodeCount,
        int[] offsets,
        int[] neighbors,
        int[]? multiplicities = null)
    {
        NodeCount = nodeCount;
        Offsets = offsets;
        Neighbors = neighbors;
        Multiplicities = multiplicities;
    }

    public int NodeCount { get; }
    public int[] Offsets { get; }
    public int[] Neighbors { get; }
    public int[]? Multiplicities { get; }

    public ReadOnlySpan<int> GetNeighbors(int node)
    {
        return Neighbors.AsSpan(Offsets[node], Offsets[node + 1] - Offsets[node]);
    }
}
