namespace PSGraphView.Sfdp;

internal static class SfdpStressMajorizationSmoother
{
    private const double MinDistance = 0.0001;
    private const double LambdaFactor = 0.05;
    private const double OuterTolerance = 0.001;
    private const double InnerTolerance = 0.01;

    public static void Smooth(
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        SfdpSmoothingMode mode,
        int iterations,
        int? componentId,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length || x.Length != graph.NodeCount)
        {
            throw new ArgumentException("Stress smoothing coordinates must match graph node count.");
        }

        if (mode is not (SfdpSmoothingMode.GraphDistance or SfdpSmoothingMode.AverageDistance or SfdpSmoothingMode.PowerDistance))
        {
            throw new NotSupportedException($"Sfdp smoothing mode '{mode}' is not supported by stress majorization.");
        }

        diagnostics.Write("stress", "start", componentId,
        [
            ("nodeCount", graph.NodeCount),
            ("mode", mode),
            ("iterations", iterations)
        ]);

        if (graph.NodeCount <= 1 || iterations <= 0)
        {
            diagnostics.Write("stress", "finish", componentId, data: [("reason", "skipped")]);
            return;
        }

        var model = BuildModel(graph, x, y, mode);
        if (model.Rows.All(static row => row.Neighbors.Length == 0))
        {
            diagnostics.Write("stress", "finish", componentId, data: [("reason", "no_model_edges")]);
            return;
        }

        var initialX = x.ToArray();
        var initialY = y.ToArray();
        var nextX = new double[x.Length];
        var nextY = new double[y.Length];
        var rhsX = new double[x.Length];
        var rhsY = new double[y.Length];

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BuildRightHandSide(model, x, y, initialX, initialY, rhsX, rhsY);
            Solve(model, x, rhsX, nextX);
            Solve(model, y, rhsY, nextY);

            var diff = RelativeDifference(x, y, nextX, nextY);
            Array.Copy(nextX, x, x.Length);
            Array.Copy(nextY, y, y.Length);
            if (diagnostics.IncludeIterations)
            {
                diagnostics.Write("stress", "iter", componentId, null, iteration,
                [
                    ("diff", diff),
                    ("outerTolerance", OuterTolerance)
                ]);
            }

            if (diff <= OuterTolerance)
            {
                break;
            }
        }

        Recenter(x, y);
        diagnostics.WriteWithCoordinates("stress", "finish", x, y, componentId,
            data:
            [
                ("mode", mode),
                ("outerTolerance", OuterTolerance)
            ]);
    }

    private static StressModel BuildModel(
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        SfdpSmoothingMode mode)
    {
        var averageNeighborDistance = ComputeAverageNeighborDistance(graph, x, y);
        var rows = new StressRow[graph.NodeCount];
        var markers = Enumerable.Repeat(-1, graph.NodeCount).ToArray();
        var stop = 0.0;
        var sbot = 0.0;

        for (var node = 0; node < graph.NodeCount; node++)
        {
            markers[node] = node;
            var neighbors = new List<StressNeighbor>();
            var diagonalWeight = 0.0;

            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[node].Length; edgeIndex++)
            {
                var neighbor = graph.Neighbors[node][edgeIndex];
                if (markers[neighbor] == node)
                {
                    continue;
                }

                markers[neighbor] = node;
                AddNeighbor(
                    node,
                    neighbor,
                    1,
                    neighbor,
                    x,
                    y,
                    mode,
                    averageNeighborDistance,
                    neighbors,
                    ref diagonalWeight,
                    ref stop,
                    ref sbot);
            }

            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[node].Length; edgeIndex++)
            {
                var intermediate = graph.Neighbors[node][edgeIndex];
                for (var nextEdgeIndex = 0; nextEdgeIndex < graph.Neighbors[intermediate].Length; nextEdgeIndex++)
                {
                    var neighbor = graph.Neighbors[intermediate][nextEdgeIndex];
                    if (markers[neighbor] == node)
                    {
                        continue;
                    }

                    markers[neighbor] = node;
                    AddNeighbor(
                        node,
                        neighbor,
                        2,
                        intermediate,
                        x,
                        y,
                        mode,
                        averageNeighborDistance,
                        neighbors,
                        ref diagonalWeight,
                        ref stop,
                        ref sbot);
                }
            }

            var lambda = diagonalWeight > 0.0 ? LambdaFactor * diagonalWeight : 0.0;
            rows[node] = new StressRow(neighbors.ToArray(), diagonalWeight + lambda, lambda);
        }

        var scaling = sbot > 0.0 ? stop / sbot : 1.0;
        if (scaling <= 0.0 || double.IsNaN(scaling) || double.IsInfinity(scaling))
        {
            scaling = 1.0;
        }

        if (Math.Abs(scaling - 1.0) > double.Epsilon)
        {
            for (var node = 0; node < rows.Length; node++)
            {
                var scaledNeighbors = new StressNeighbor[rows[node].Neighbors.Length];
                for (var i = 0; i < scaledNeighbors.Length; i++)
                {
                    var neighbor = rows[node].Neighbors[i];
                    scaledNeighbors[i] = neighbor with { IdealDistance = neighbor.IdealDistance * scaling };
                }

                rows[node] = rows[node] with { Neighbors = scaledNeighbors };
            }
        }

        return new StressModel(rows);
    }

    private static void AddNeighbor(
        int nodeIndex,
        int neighborIndex,
        int graphDistance,
        int intermediateIndex,
        double[] x,
        double[] y,
        SfdpSmoothingMode mode,
        double[] averageNeighborDistance,
        List<StressNeighbor> neighbors,
        ref double diagonalWeight,
        ref double stop,
        ref double sbot)
    {
        if (neighborIndex == nodeIndex)
        {
            return;
        }

        var idealDistance = ResolveIdealDistance(
            mode,
            graphDistance,
            nodeIndex,
            neighborIndex,
            intermediateIndex,
            x,
            y,
            averageNeighborDistance);
        idealDistance = Math.Max(idealDistance, MinDistance);

        var weight = 1.0 / (idealDistance * idealDistance);
        neighbors.Add(new StressNeighbor(neighborIndex, idealDistance, weight));
        diagonalWeight += weight;

        var actualDistance = Distance(x, y, nodeIndex, neighborIndex);
        stop += weight * idealDistance * actualDistance;
        sbot += weight * idealDistance * idealDistance;
    }

    private static void BuildRightHandSide(
        StressModel model,
        double[] x,
        double[] y,
        double[] initialX,
        double[] initialY,
        double[] rhsX,
        double[] rhsY)
    {
        for (var node = 0; node < model.Rows.Length; node++)
        {
            var row = model.Rows[node];
            var diagonalDistanceWeight = 0.0;
            var valueX = row.Lambda * initialX[node];
            var valueY = row.Lambda * initialY[node];

            foreach (var neighbor in row.Neighbors)
            {
                var distance = Distance(x, y, node, neighbor.Index);
                var scaledWeight = neighbor.Weight * neighbor.IdealDistance / distance;
                diagonalDistanceWeight += scaledWeight;
                valueX -= scaledWeight * x[neighbor.Index];
                valueY -= scaledWeight * y[neighbor.Index];
            }

            valueX += diagonalDistanceWeight * x[node];
            valueY += diagonalDistanceWeight * y[node];
            rhsX[node] = valueX;
            rhsY[node] = valueY;
        }
    }

    private static void Solve(StressModel model, double[] previous, double[] rhs, double[] result)
    {
        Array.Copy(previous, result, previous.Length);

        var maxIterations = Math.Max(4, (int)Math.Floor(Math.Sqrt(model.Rows.Length)));
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var maxDelta = 0.0;

            for (var node = 0; node < model.Rows.Length; node++)
            {
                var row = model.Rows[node];
                if (row.Diagonal <= MinDistance)
                {
                    continue;
                }

                var value = rhs[node];
                foreach (var neighbor in row.Neighbors)
                {
                    value += neighbor.Weight * result[neighbor.Index];
                }

                var next = value / row.Diagonal;
                maxDelta = Math.Max(maxDelta, Math.Abs(next - result[node]));
                result[node] = next;
            }

            if (maxDelta <= InnerTolerance)
            {
                break;
            }
        }
    }

    private static double RelativeDifference(double[] currentX, double[] currentY, double[] nextX, double[] nextY)
    {
        var numerator = 0.0;
        var denominator = 0.0;
        for (var i = 0; i < currentX.Length; i++)
        {
            var dx = nextX[i] - currentX[i];
            var dy = nextY[i] - currentY[i];
            numerator += Math.Sqrt((dx * dx) + (dy * dy));
            denominator += (currentX[i] * currentX[i]) + (currentY[i] * currentY[i]);
        }

        return numerator / Math.Sqrt(Math.Max(denominator, MinDistance));
    }

    private static double[] ComputeAverageNeighborDistance(SfdpComponentGraph graph, double[] x, double[] y)
    {
        var averageDistances = new double[graph.NodeCount];

        for (var i = 0; i < graph.NodeCount; i++)
        {
            if (graph.Neighbors[i].Length == 0)
            {
                averageDistances[i] = 1.0;
                continue;
            }

            var total = 0.0;
            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[i].Length; edgeIndex++)
            {
                total += Distance(x, y, i, graph.Neighbors[i][edgeIndex]);
            }

            averageDistances[i] = total / graph.Neighbors[i].Length;
        }

        return averageDistances;
    }

    private static double ResolveIdealDistance(
        SfdpSmoothingMode mode,
        int graphDistance,
        int nodeIndex,
        int neighborIndex,
        int intermediateIndex,
        double[] x,
        double[] y,
        double[] averageNeighborDistance)
    {
        return mode switch
        {
            SfdpSmoothingMode.GraphDistance => graphDistance,
            SfdpSmoothingMode.AverageDistance => graphDistance == 1
                ? (averageNeighborDistance[nodeIndex] + averageNeighborDistance[neighborIndex]) * 0.5
                : (averageNeighborDistance[nodeIndex] + (2.0 * averageNeighborDistance[intermediateIndex]) + averageNeighborDistance[neighborIndex]) * 0.5,
            SfdpSmoothingMode.PowerDistance => Math.Pow(Math.Max(DistanceCropped(x, y, nodeIndex, neighborIndex), MinDistance), 0.4),
            _ => throw new NotSupportedException($"Sfdp smoothing mode '{mode}' is not supported.")
        };
    }

    private static double Distance(double[] x, double[] y, int left, int right)
    {
        var dx = x[left] - x[right];
        var dy = y[left] - y[right];
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double DistanceCropped(double[] x, double[] y, int left, int right)
        => Math.Max(Distance(x, y, left, right), MinDistance);

    private static void Recenter(double[] x, double[] y)
    {
        var centerX = 0.0;
        var centerY = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            centerX += x[i];
            centerY += y[i];
        }

        centerX /= x.Length;
        centerY /= y.Length;

        for (var i = 0; i < x.Length; i++)
        {
            x[i] -= centerX;
            y[i] -= centerY;
        }
    }

    private sealed record StressModel(StressRow[] Rows);

    private sealed record StressRow(StressNeighbor[] Neighbors, double Diagonal, double Lambda);

    private sealed record StressNeighbor(int Index, double IdealDistance, double Weight);
}
