namespace PSGraphView.Sfdp;

internal sealed record SfdpComponentGraph(
    int[] NodeIndices,
    int EdgeCount,
    int[][] Neighbors,
    double[][] Weights)
{
    public int NodeCount => NodeIndices.Length;

    public static SfdpComponentGraph FromCsr(int[] globalNodeIndices, SfdpCsrGraph graph)
    {
        ArgumentNullException.ThrowIfNull(globalNodeIndices);
        ArgumentNullException.ThrowIfNull(graph);

        var localIndexByGlobal = new Dictionary<int, int>(globalNodeIndices.Length);
        for (var i = 0; i < globalNodeIndices.Length; i++)
        {
            localIndexByGlobal[globalNodeIndices[i]] = i;
        }

        var neighborLists = new List<int>[globalNodeIndices.Length];
        var weightLists = new List<double>[globalNodeIndices.Length];
        for (var i = 0; i < globalNodeIndices.Length; i++)
        {
            neighborLists[i] = [];
            weightLists[i] = [];
        }

        var edgeCount = 0;
        for (var localIndex = 0; localIndex < globalNodeIndices.Length; localIndex++)
        {
            var globalIndex = globalNodeIndices[localIndex];
            var start = graph.Offsets[globalIndex];
            var end = graph.Offsets[globalIndex + 1];
            for (var offset = start; offset < end; offset++)
            {
                if (!localIndexByGlobal.TryGetValue(graph.Neighbors[offset], out var localNeighbor))
                {
                    continue;
                }

                neighborLists[localIndex].Add(localNeighbor);
                weightLists[localIndex].Add(graph.Multiplicities?[offset] ?? 1);
                if (localIndex < localNeighbor)
                {
                    edgeCount++;
                }
            }
        }

        var neighbors = new int[globalNodeIndices.Length][];
        var weights = new double[globalNodeIndices.Length][];
        for (var i = 0; i < globalNodeIndices.Length; i++)
        {
            neighbors[i] = neighborLists[i].ToArray();
            weights[i] = weightLists[i].ToArray();
        }

        return new SfdpComponentGraph(globalNodeIndices, edgeCount, neighbors, weights);
    }
}
