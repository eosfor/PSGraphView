namespace PSGraphView.Sfdp;

internal sealed class SfdpMultilevelLayouter
{
    private const int MinCoarseGraphSize = 4;
    private const int MaxClusterSize = 4;
    private const double MinCoarsenFactor = 0.75;
    private readonly SfdpSingleLevelLayouter _singleLevelLayouter = new();

    public SfdpComponentLayout LayoutComponent(
        int componentId,
        int[] globalNodeIndices,
        SfdpCsrGraph graph,
        SfdpOptions options,
        Random? random,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken)
    {
        var componentGraph = SfdpComponentGraph.FromCsr(globalNodeIndices, graph);
        diagnostics.Write("component", "start", componentId: componentId,
        [
            ("nodeCount", componentGraph.NodeCount),
            ("edgeCount", componentGraph.EdgeCount)
        ]);

        var repulsiveExponentResolution = SfdpSingleLevelLayouter.ResolveRepulsiveExponent(
            componentGraph,
            options.RepulsiveExponent,
            autoSource: "component_auto");

        diagnostics.Write("component", "control", componentId: componentId,
        [
            ("requestedRepulsiveExponent", repulsiveExponentResolution.RequestedRepulsiveExponent),
            ("repulsiveExponent", repulsiveExponentResolution.EffectiveRepulsiveExponent),
            ("repulsiveExponentSource", repulsiveExponentResolution.Source),
            ("powerLawGraph", repulsiveExponentResolution.PowerLawGraph)
        ]);

        var layout = LayoutRecursive(componentGraph, options, random, diagnostics, cancellationToken, componentId, 0, repulsiveExponentResolution);
        var x = layout.X;
        var y = layout.Y;
        SfdpPostProcessor.Apply(componentGraph, x, y, options, _singleLevelLayouter, random, componentId, diagnostics, cancellationToken, repulsiveExponentResolution);
        WriteComponentGeometry("layout_component_return", componentGraph, x, y, componentId, diagnostics);

        diagnostics.WriteWithCoordinates("component", "finish", x, y, componentId: componentId,
            data:
            [
                ("nodeCount", componentGraph.NodeCount),
                ("edgeCount", componentGraph.EdgeCount)
            ]);

        return new SfdpComponentLayout(componentId, globalNodeIndices, x, y);
    }

    private static void WriteComponentGeometry(
        string stage,
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        int componentId,
        SfdpDiagnosticsWriter diagnostics)
    {
        var geometry = SfdpGeometrySummary.Create(graph, x, y);
        diagnostics.Write("component", "geometry", componentId,
        [
            ("stage", stage),
            ("minX", geometry.MinX),
            ("minY", geometry.MinY),
            ("maxX", geometry.MaxX),
            ("maxY", geometry.MaxY),
            ("width", geometry.Width),
            ("height", geometry.Height),
            ("diagonal", geometry.Diagonal),
            ("averageEdgeLength", geometry.AverageEdgeLength)
        ]);
    }

    private SfdpSingleLevelLayoutResult LayoutRecursive(
        SfdpComponentGraph graph,
        SfdpOptions options,
        Random? random,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken,
        int componentId,
        int depth,
        SfdpRepulsiveExponentResolution repulsiveExponentResolution)
    {
        diagnostics.Write("multilevel", "enter", componentId: componentId, level: depth,
        [
            ("nodeCount", graph.NodeCount),
            ("edgeCount", graph.EdgeCount),
            ("enableMultilevel", options.EnableMultilevel),
            ("multilevelThreshold", options.MultilevelThreshold),
            ("maxMultilevelDepth", options.MaxMultilevelDepth)
        ]);

        if (!options.EnableMultilevel)
        {
            diagnostics.Write("multilevel", "base_case", componentId: componentId, level: depth, [("reason", "disabled")]);
            return _singleLevelLayouter.LayoutComponent(graph, options, random, cancellationToken, repulsiveExponentResolution: repulsiveExponentResolution, componentId: componentId, level: depth, diagnostics: diagnostics);
        }

        if (graph.NodeCount < options.MultilevelThreshold)
        {
            diagnostics.Write("multilevel", "base_case", componentId: componentId, level: depth,
            [
                ("reason", "threshold"),
                ("threshold", options.MultilevelThreshold)
            ]);
            return _singleLevelLayouter.LayoutComponent(graph, options, random, cancellationToken, repulsiveExponentResolution: repulsiveExponentResolution, componentId: componentId, level: depth, diagnostics: diagnostics);
        }

        if (depth >= options.MaxMultilevelDepth)
        {
            diagnostics.Write("multilevel", "base_case", componentId: componentId, level: depth,
            [
                ("reason", "max_depth"),
                ("maxMultilevelDepth", options.MaxMultilevelDepth)
            ]);
            return _singleLevelLayouter.LayoutComponent(graph, options, random, cancellationToken, repulsiveExponentResolution: repulsiveExponentResolution, componentId: componentId, level: depth, diagnostics: diagnostics);
        }

        var coarsening = Coarsen(graph, random);
        if (coarsening.CoarseGraph.NodeCount >= graph.NodeCount)
        {
            diagnostics.Write("multilevel", "base_case", componentId: componentId, level: depth,
            [
                ("reason", "coarsening_not_smaller"),
                ("coarseNodeCount", coarsening.CoarseGraph.NodeCount)
            ]);
            return _singleLevelLayouter.LayoutComponent(graph, options, random, cancellationToken, repulsiveExponentResolution: repulsiveExponentResolution, componentId: componentId, level: depth, diagnostics: diagnostics);
        }

        diagnostics.Write("multilevel", "coarsen", componentId: componentId, level: depth,
        [
            ("fineNodeCount", graph.NodeCount),
            ("coarseNodeCount", coarsening.CoarseGraph.NodeCount),
            ("coarsenFactor", (double)coarsening.CoarseGraph.NodeCount / graph.NodeCount)
        ]);

        var coarseLayout = LayoutRecursive(coarsening.CoarseGraph, options, random, diagnostics, cancellationToken, componentId, depth + 1, repulsiveExponentResolution);
        var previousNaturalLength = coarseLayout.NaturalLength;
        var prolongation = Prolongate(graph, coarsening, coarseLayout.X, coarseLayout.Y, previousNaturalLength, random);
        var expectedGraphvizNaturalLength = previousNaturalLength * 0.75;
        var refinementNaturalLength = expectedGraphvizNaturalLength;

        diagnostics.Write("multilevel", "prolongate", componentId: componentId, level: depth,
        [
            ("fineNodeCount", graph.NodeCount),
            ("coarseNodeCount", coarsening.CoarseGraph.NodeCount),
            ("jitter", prolongation.Jitter),
            ("previousNaturalLength", previousNaturalLength),
            ("expectedGraphvizNaturalLength", expectedGraphvizNaturalLength),
            ("actualManagedNaturalLength", refinementNaturalLength)
        ]);

        diagnostics.Write("multilevel", "refine", componentId: componentId, level: depth,
        [
            ("maxIterations", Math.Min(options.MaxIterations, options.RefinementIterations)),
            ("adaptiveCooling", false),
            ("initialStep", options.InitialStep),
            ("previousNaturalLength", previousNaturalLength),
            ("expectedGraphvizNaturalLength", expectedGraphvizNaturalLength),
            ("actualManagedNaturalLength", refinementNaturalLength)
        ]);

        return _singleLevelLayouter.LayoutComponent(
            graph,
            options,
            random,
            cancellationToken,
            prolongation.X,
            prolongation.Y,
            Math.Min(options.MaxIterations, options.RefinementIterations),
            false,
            refinementNaturalLength,
            repulsiveExponentResolution,
            componentId: componentId,
            level: depth,
            diagnostics: diagnostics);
    }

    private static SfdpCoarseningResult Coarsen(SfdpComponentGraph graph, Random? random)
    {
        var currentGraph = graph;
        var transfer = SfdpTransferOperator.Identity(graph.NodeCount);

        while (currentGraph.NodeCount >= MinCoarseGraphSize)
        {
            var step = CoarsenSingle(currentGraph, random);
            if (step.CoarseGraph.NodeCount >= currentGraph.NodeCount || step.CoarseGraph.NodeCount < MinCoarseGraphSize)
            {
                break;
            }

            transfer = SfdpTransferOperator.Compose(transfer, step.Transfer);
            currentGraph = step.CoarseGraph;

            if (currentGraph.NodeCount <= Math.Floor(MinCoarsenFactor * graph.NodeCount))
            {
                break;
            }
        }

        return new SfdpCoarseningResult(currentGraph, transfer);
    }

    private static SfdpCoarseningResult CoarsenSingle(SfdpComponentGraph graph, Random? random)
    {
        var matched = new bool[graph.NodeCount];
        var clusters = new List<int[]>();

        foreach (var cluster in BuildSuperVariableClusters(graph))
        {
            if (cluster.Length <= 1)
            {
                continue;
            }

            foreach (var node in cluster)
            {
                matched[node] = true;
            }

            clusters.Add(cluster);
        }

        var order = Enumerable.Range(0, graph.NodeCount).ToArray();
        Shuffle(order, random);

        foreach (var node in order)
        {
            if (matched[node])
            {
                continue;
            }

            var bestNeighbor = -1;
            var bestMultiplicity = double.NegativeInfinity;
            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[node].Length; edgeIndex++)
            {
                var neighbor = graph.Neighbors[node][edgeIndex];
                if (neighbor == node || matched[neighbor])
                {
                    continue;
                }

                var multiplicity = graph.Weights[node][edgeIndex];
                if (multiplicity > bestMultiplicity)
                {
                    bestMultiplicity = multiplicity;
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor >= 0)
            {
                matched[node] = true;
                matched[bestNeighbor] = true;
                clusters.Add([node, bestNeighbor]);
            }
        }

        for (var node = 0; node < graph.NodeCount; node++)
        {
            if (!matched[node])
            {
                clusters.Add([node]);
            }
        }

        return BuildCoarseningResult(graph, clusters);
    }

    private static SfdpProlongationResult Prolongate(
        SfdpComponentGraph fineGraph,
        SfdpCoarseningResult coarsening,
        double[] coarseX,
        double[] coarseY,
        double previousNaturalLength,
        Random? random)
    {
        var x = new double[fineGraph.NodeCount];
        var y = new double[fineGraph.NodeCount];

        coarsening.Transfer.ApplyProlongation(coarseX, coarseY, x, y);

        var jitter = Math.Max(previousNaturalLength * 0.001, 0.0);
        foreach (var cluster in coarsening.Transfer.CoarseToFine)
        {
            if (cluster.Length <= 1)
            {
                continue;
            }

            for (var i = 1; i < cluster.Length; i++)
            {
                var fineNode = cluster[i];
                x[fineNode] += jitter * ((random ?? Random.Shared).NextDouble() - 0.5);
                y[fineNode] += jitter * ((random ?? Random.Shared).NextDouble() - 0.5);
            }
        }

        return new SfdpProlongationResult(x, y, jitter);
    }

    private static IReadOnlyList<int[]> BuildSuperVariableClusters(SfdpComponentGraph graph)
    {
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var node = 0; node < graph.NodeCount; node++)
        {
            var signature = BuildNodeSignature(graph, node);
            if (!groups.TryGetValue(signature, out var nodes))
            {
                nodes = [];
                groups.Add(signature, nodes);
            }

            nodes.Add(node);
        }

        var clusters = new List<int[]>();
        foreach (var group in groups.Values)
        {
            if (group.Count <= 1)
            {
                continue;
            }

            for (var offset = 0; offset < group.Count; offset += MaxClusterSize)
            {
                var count = Math.Min(MaxClusterSize, group.Count - offset);
                var cluster = new int[count];
                for (var i = 0; i < count; i++)
                {
                    cluster[i] = group[offset + i];
                }

                clusters.Add(cluster);
            }
        }

        return clusters;
    }

    private static string BuildNodeSignature(SfdpComponentGraph graph, int node)
    {
        var parts = new string[graph.Neighbors[node].Length];
        for (var i = 0; i < graph.Neighbors[node].Length; i++)
        {
            parts[i] = $"{graph.Neighbors[node][i]}:{graph.Weights[node][i]:G17}";
        }

        return string.Join("|", parts);
    }

    private static SfdpCoarseningResult BuildCoarseningResult(SfdpComponentGraph graph, IReadOnlyList<int[]> clusters)
    {
        var transfer = SfdpTransferOperator.FromClusters(graph.NodeCount, clusters);
        var fineAdjacency = SfdpSparseMatrix.FromComponentGraph(graph);
        var coarseAdjacency = SfdpSparseMatrix
            .Multiply3(transfer.RawR, fineAdjacency, transfer.P)
            .RemoveDiagonal();
        var coarseGraph = coarseAdjacency.ToComponentGraph();

        return new SfdpCoarseningResult(coarseGraph, transfer);
    }

    private static void Shuffle(int[] values, Random? random)
    {
        if (random is null)
        {
            return;
        }

        for (var i = values.Length - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }

    private sealed record SfdpCoarseningResult(
        SfdpComponentGraph CoarseGraph,
        SfdpTransferOperator Transfer);

    private sealed record SfdpProlongationResult(
        double[] X,
        double[] Y,
        double Jitter);
}
