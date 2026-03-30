namespace PSGraphView.Sfdp;

internal sealed record SfdpRepulsiveExponentResolution(
    double? RequestedRepulsiveExponent,
    double EffectiveRepulsiveExponent,
    string Source,
    bool PowerLawGraph);

internal sealed record SfdpSingleLevelLayoutResult(
    double[] X,
    double[] Y,
    double NaturalLength,
    double RepulsiveExponent);

internal sealed class SfdpSingleLevelLayouter
{
    private const int GraphvizQuadtreeOptimizerMaxLevel = 20;
    private const double AttractiveForceConstant = 0.2;
    private const double CoolingFactor = 0.90;
    private const double MinDistance = 0.0001;

    public SfdpSingleLevelLayoutResult LayoutComponent(
        SfdpComponentGraph componentGraph,
        SfdpOptions options,
        Random? random,
        CancellationToken cancellationToken,
        double[]? initialX = null,
        double[]? initialY = null,
        int? maxIterationsOverride = null,
        bool? adaptiveCoolingOverride = null,
        double? naturalLengthOverride = null,
        SfdpRepulsiveExponentResolution? repulsiveExponentResolution = null,
        double? initialStepOverride = null,
        int? componentId = null,
        int? level = null,
        SfdpDiagnosticsWriter? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(componentGraph);
        ArgumentNullException.ThrowIfNull(options);

        diagnostics ??= SfdpDiagnosticsWriter.Disabled;
        var maxIterations = maxIterationsOverride ?? options.MaxIterations;
        var adaptiveCooling = adaptiveCoolingOverride ?? options.AdaptiveCooling;
        var step = initialStepOverride ?? options.InitialStep;
        var configuredQuadtreeMode = ResolveConfiguredQuadtreeMode(options);
        var effectiveQuadtreeMode = ResolveEffectiveQuadtreeMode(componentGraph, options, configuredQuadtreeMode);

        diagnostics.Write("singlelevel", "start", componentId, level,
        [
            ("nodeCount", componentGraph.NodeCount),
            ("edgeCount", componentGraph.EdgeCount),
            ("maxIterations", maxIterations),
            ("initialStep", step),
            ("tolerance", options.Tolerance),
            ("adaptiveCooling", adaptiveCooling),
            ("hasInitialPositions", initialX is not null && initialY is not null),
            ("useBarnesHut", options.UseBarnesHut),
            ("quadtreeMode", configuredQuadtreeMode),
            ("barnesHutThreshold", options.BarnesHutThreshold),
            ("barnesHutTheta", options.BarnesHutTheta),
            ("quadtreeHybridThreshold", options.QuadtreeHybridThreshold),
            ("quadtreeMaxDepth", options.QuadtreeMaxDepth)
        ]);

        if (componentGraph.NodeCount == 0)
        {
            diagnostics.Write("singlelevel", "finish", componentId, level, [("reason", "empty")]);
            return new SfdpSingleLevelLayoutResult([], [], 1.0, -1.0);
        }

        if (componentGraph.NodeCount == 1)
        {
            var resolution = repulsiveExponentResolution ?? ResolveRepulsiveExponent(componentGraph, options.RepulsiveExponent);
            var exponent = resolution.EffectiveRepulsiveExponent;
            var singletonX = new[] { 0.0 };
            var singletonY = new[] { 0.0 };
            diagnostics.WriteWithCoordinates("singlelevel", "finish", singletonX, singletonY, componentId, level,
            [
                ("reason", "singleton"),
                ("naturalLength", 1.0),
                ("repulsiveExponent", exponent),
                ("iterations", 0),
                ("finalStep", step),
                ("finalForceNorm", 0.0)
            ]);
            return new SfdpSingleLevelLayoutResult(singletonX, singletonY, 1.0, exponent);
        }

        var x = new double[componentGraph.NodeCount];
        var y = new double[componentGraph.NodeCount];

        if (initialX is not null && initialY is not null)
        {
            if (initialX.Length != componentGraph.NodeCount || initialY.Length != componentGraph.NodeCount)
            {
                throw new ArgumentException("Initial layout arrays must match component node count.");
            }

            Array.Copy(initialX, x, componentGraph.NodeCount);
            Array.Copy(initialY, y, componentGraph.NodeCount);
        }
        else
        {
            InitializePositions(x, y, random);
        }

        var resolutionForLevel = repulsiveExponentResolution ?? ResolveRepulsiveExponent(componentGraph, options.RepulsiveExponent);
        var powerLawGraph = resolutionForLevel.PowerLawGraph;
        var requestedRepulsiveExponent = resolutionForLevel.RequestedRepulsiveExponent;
        var pSource = resolutionForLevel.Source;
        var p = resolutionForLevel.EffectiveRepulsiveExponent;

        var requestedNaturalLength = naturalLengthOverride ?? options.NaturalLength;
        var kSource = naturalLengthOverride.HasValue
            ? "override"
            : options.NaturalLength.HasValue
                ? "options"
                : "auto";
        var k = requestedNaturalLength ?? EstimateNaturalLength(componentGraph, x, y);

        var attractiveScale = Math.Pow(AttractiveForceConstant, (2.0 - p) / 3.0) / k;
        var repulsiveScale = Math.Pow(k, 1.0 - p);
        var forceNorm = 0.0;
        var iterations = 0;

        diagnostics.Write("singlelevel", "control", componentId, level,
        [
            ("requestedNaturalLength", requestedNaturalLength),
            ("naturalLength", k),
            ("naturalLengthSource", kSource),
            ("requestedRepulsiveExponent", requestedRepulsiveExponent),
            ("repulsiveExponent", p),
            ("repulsiveExponentSource", pSource),
            ("powerLawGraph", powerLawGraph),
            ("attractiveScale", attractiveScale),
            ("repulsiveScale", repulsiveScale),
            ("quadtreeMode", configuredQuadtreeMode),
            ("effectiveQuadtreeMode", effectiveQuadtreeMode)
        ]);

        var forceX = effectiveQuadtreeMode == SfdpQuadtreeMode.Fast
            ? new double[componentGraph.NodeCount]
            : null;
        var forceY = effectiveQuadtreeMode == SfdpQuadtreeMode.Fast
            ? new double[componentGraph.NodeCount]
            : null;
        var supernodeBuffer = effectiveQuadtreeMode == SfdpQuadtreeMode.Normal
            ? new SfdpQuadTreeSupernodeBuffer()
            : null;
        var quadtreeLevelOptimizer = effectiveQuadtreeMode == SfdpQuadtreeMode.Normal
            ? new SfdpQuadtreeLevelOptimizer(Math.Min(options.QuadtreeMaxDepth, GraphvizQuadtreeOptimizerMaxLevel), GraphvizQuadtreeOptimizerMaxLevel)
            : null;

        for (var iteration = 0; iteration < maxIterations && step > options.Tolerance; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            iterations = iteration + 1;
            var previousForceNorm = forceNorm;
            var quadtreeLevel = effectiveQuadtreeMode == SfdpQuadtreeMode.Normal
                ? quadtreeLevelOptimizer!.CurrentLevel
                : options.QuadtreeMaxDepth;
            var quadTree = effectiveQuadtreeMode is SfdpQuadtreeMode.Normal or SfdpQuadtreeMode.Fast
                ? SfdpQuadTree.Build(x, y, quadtreeLevel)
                : null;
            double? nsuperAverage = null;
            double? countsAverage = null;
            double? quadtreeWork = null;

            if (effectiveQuadtreeMode == SfdpQuadtreeMode.Fast)
            {
                forceNorm = ExecuteFastIteration(componentGraph, x, y, attractiveScale, repulsiveScale, p, step, options.BarnesHutTheta, quadTree, forceX!, forceY!);
            }
            else if (effectiveQuadtreeMode == SfdpQuadtreeMode.Normal)
            {
                forceNorm = ExecuteNormalIteration(
                    componentGraph,
                    x,
                    y,
                    attractiveScale,
                    repulsiveScale,
                    p,
                    step,
                    options.BarnesHutTheta,
                    quadTree!,
                    supernodeBuffer!,
                    out var computedNsuperAverage,
                    out var computedCountsAverage);

                nsuperAverage = computedNsuperAverage;
                countsAverage = computedCountsAverage;
                quadtreeWork = (5.0 * computedNsuperAverage) + computedCountsAverage;
                quadtreeLevelOptimizer!.Train(quadtreeWork.Value);
            }
            else
            {
                forceNorm = ExecuteSequentialIteration(componentGraph, x, y, attractiveScale, repulsiveScale, p, step, options.BarnesHutTheta, quadTree);
            }

            if (diagnostics.IncludeIterations)
            {
                diagnostics.Write("singlelevel", "iter", componentId, level, iteration,
                [
                    ("step", step),
                    ("forceNorm", forceNorm),
                    ("previousForceNorm", previousForceNorm),
                    ("usedBarnesHut", quadTree is not null),
                    ("quadtreeMode", configuredQuadtreeMode),
                    ("effectiveQuadtreeMode", effectiveQuadtreeMode),
                    ("quadtreeLevel", quadTree is not null ? quadtreeLevel : null),
                    ("quadtreeMaxDepthUsed", quadTree?.MaxDepthUsed),
                    ("quadtreeWork", quadtreeWork),
                    ("nsuperAverage", nsuperAverage),
                    ("countsAverage", countsAverage)
                ]);
            }

            step = UpdateStep(adaptiveCooling, step, forceNorm, previousForceNorm);
        }

        CenterPositions(x, y);
        var terminationReason = step <= options.Tolerance
            ? "tolerance"
            : iterations >= maxIterations
                ? "max_iterations"
                : "completed";
        diagnostics.WriteWithCoordinates("singlelevel", "finish", x, y, componentId, level,
        [
            ("iterations", iterations),
            ("finalStep", step),
            ("finalForceNorm", forceNorm),
            ("terminationReason", terminationReason),
            ("naturalLength", k),
            ("repulsiveExponent", p),
            ("quadtreeMode", configuredQuadtreeMode),
            ("effectiveQuadtreeMode", effectiveQuadtreeMode)
        ]);
        return new SfdpSingleLevelLayoutResult(x, y, k, p);
    }

    internal static SfdpQuadtreeMode ResolveConfiguredQuadtreeMode(SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.UseBarnesHut ? options.QuadtreeMode : SfdpQuadtreeMode.None;
    }

    internal static SfdpQuadtreeMode ResolveEffectiveQuadtreeMode(
        SfdpComponentGraph componentGraph,
        SfdpOptions options,
        SfdpQuadtreeMode configuredMode)
    {
        ArgumentNullException.ThrowIfNull(componentGraph);
        ArgumentNullException.ThrowIfNull(options);

        if (configuredMode == SfdpQuadtreeMode.None || componentGraph.NodeCount < options.BarnesHutThreshold)
        {
            return SfdpQuadtreeMode.None;
        }

        if (configuredMode == SfdpQuadtreeMode.Hybrid)
        {
            return componentGraph.NodeCount > options.QuadtreeHybridThreshold
                ? SfdpQuadtreeMode.Fast
                : SfdpQuadtreeMode.Normal;
        }

        return configuredMode;
    }

    private static double ExecuteNormalIteration(
        SfdpComponentGraph componentGraph,
        double[] x,
        double[] y,
        double attractiveScale,
        double repulsiveScale,
        double repulsiveExponent,
        double step,
        double theta,
        SfdpQuadTree quadTree,
        SfdpQuadTreeSupernodeBuffer supernodeBuffer,
        out double nsuperAverage,
        out double countsAverage)
    {
        var forceNorm = 0.0;
        var nsuperSum = 0.0;
        var countsSum = 0.0;

        for (var i = 0; i < componentGraph.NodeCount; i++)
        {
            var fx = 0.0;
            var fy = 0.0;

            AccumulateAttractiveForce(componentGraph, x, y, attractiveScale, i, ref fx, ref fy);
            quadTree.GetSupernodes(i, theta, supernodeBuffer);
            countsSum += supernodeBuffer.TraversalCount;
            nsuperSum += supernodeBuffer.Count;

            for (var supernodeIndex = 0; supernodeIndex < supernodeBuffer.Count; supernodeIndex++)
            {
                var distance = Math.Max(supernodeBuffer.Distances[supernodeIndex], MinDistance);
                var repulsive = supernodeBuffer.Weights[supernodeIndex] * repulsiveScale / Math.Pow(distance, 1.0 - repulsiveExponent);
                fx += repulsive * (x[i] - supernodeBuffer.CenterX[supernodeIndex]);
                fy += repulsive * (y[i] - supernodeBuffer.CenterY[supernodeIndex]);
            }

            var magnitude = Math.Sqrt(fx * fx + fy * fy);
            forceNorm += magnitude;

            if (magnitude <= 0)
            {
                continue;
            }

            x[i] += step * (fx / magnitude);
            y[i] += step * (fy / magnitude);
        }

        nsuperAverage = nsuperSum / componentGraph.NodeCount;
        countsAverage = countsSum / componentGraph.NodeCount;
        return forceNorm;
    }

    private static double ExecuteSequentialIteration(
        SfdpComponentGraph componentGraph,
        double[] x,
        double[] y,
        double attractiveScale,
        double repulsiveScale,
        double repulsiveExponent,
        double step,
        double theta,
        SfdpQuadTree? quadTree)
    {
        var forceNorm = 0.0;

        for (var i = 0; i < componentGraph.NodeCount; i++)
        {
            var fx = 0.0;
            var fy = 0.0;

            AccumulateAttractiveForce(componentGraph, x, y, attractiveScale, i, ref fx, ref fy);

            if (quadTree is not null)
            {
                quadTree.AccumulateRepulsion(i, theta, repulsiveExponent, repulsiveScale, ref fx, ref fy);
            }
            else
            {
                AccumulateExactRepulsion(componentGraph.NodeCount, x, y, repulsiveExponent, repulsiveScale, i, ref fx, ref fy);
            }

            var magnitude = Math.Sqrt(fx * fx + fy * fy);
            forceNorm += magnitude;

            if (magnitude <= 0)
            {
                continue;
            }

            x[i] += step * (fx / magnitude);
            y[i] += step * (fy / magnitude);
        }

        return forceNorm;
    }

    private static double ExecuteFastIteration(
        SfdpComponentGraph componentGraph,
        double[] x,
        double[] y,
        double attractiveScale,
        double repulsiveScale,
        double repulsiveExponent,
        double step,
        double theta,
        SfdpQuadTree? quadTree,
        double[] forceX,
        double[] forceY)
    {
        Array.Clear(forceX, 0, forceX.Length);
        Array.Clear(forceY, 0, forceY.Length);

        for (var i = 0; i < componentGraph.NodeCount; i++)
        {
            if (quadTree is not null)
            {
                quadTree.AccumulateRepulsion(i, theta, repulsiveExponent, repulsiveScale, ref forceX[i], ref forceY[i]);
            }
            else
            {
                AccumulateExactRepulsion(componentGraph.NodeCount, x, y, repulsiveExponent, repulsiveScale, i, ref forceX[i], ref forceY[i]);
            }
        }

        for (var i = 0; i < componentGraph.NodeCount; i++)
        {
            AccumulateAttractiveForce(componentGraph, x, y, attractiveScale, i, ref forceX[i], ref forceY[i]);
        }

        var forceNorm = 0.0;
        for (var i = 0; i < componentGraph.NodeCount; i++)
        {
            var fx = forceX[i];
            var fy = forceY[i];
            var magnitude = Math.Sqrt(fx * fx + fy * fy);
            forceNorm += magnitude;
            if (magnitude <= 0)
            {
                continue;
            }

            x[i] += step * (fx / magnitude);
            y[i] += step * (fy / magnitude);
        }

        return forceNorm;
    }

    private static void AccumulateAttractiveForce(
        SfdpComponentGraph componentGraph,
        double[] x,
        double[] y,
        double attractiveScale,
        int node,
        ref double fx,
        ref double fy)
    {
        for (var edgeIndex = 0; edgeIndex < componentGraph.Neighbors[node].Length; edgeIndex++)
        {
            var neighbor = componentGraph.Neighbors[node][edgeIndex];
            var multiplicity = componentGraph.Weights[node][edgeIndex];
            var dx = x[node] - x[neighbor];
            var dy = y[node] - y[neighbor];
            var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), MinDistance);
            var attractive = attractiveScale * multiplicity * distance;
            fx -= attractive * dx;
            fy -= attractive * dy;
        }
    }

    private static void AccumulateExactRepulsion(
        int nodeCount,
        double[] x,
        double[] y,
        double repulsiveExponent,
        double repulsiveScale,
        int node,
        ref double fx,
        ref double fy)
    {
        for (var other = 0; other < nodeCount; other++)
        {
            if (other == node)
            {
                continue;
            }

            var dx = x[node] - x[other];
            var dy = y[node] - y[other];
            var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), MinDistance);
            var repulsive = repulsiveScale / Math.Pow(distance, 1.0 - repulsiveExponent);
            fx += repulsive * dx;
            fy += repulsive * dy;
        }
    }

    private sealed class SfdpQuadtreeLevelOptimizer
    {
        private const int OptInit = 0;
        private const int OptUp = 1;
        private const int OptDown = -1;
        private readonly double[] _work;
        private readonly int _maxLevel;
        private int _direction;

        public SfdpQuadtreeLevelOptimizer(int initialLevel, int maxLevel)
        {
            _maxLevel = Math.Max(0, maxLevel);
            CurrentLevel = Math.Clamp(initialLevel, 0, _maxLevel);
            _work = new double[_maxLevel + 1];
            _direction = OptInit;
        }

        public int CurrentLevel { get; private set; }

        public void Train(double work)
        {
            var level = CurrentLevel;
            _work[level] = work;

            if (_direction == OptInit)
            {
                if (level == _maxLevel)
                {
                    _direction = OptDown;
                    CurrentLevel = Math.Max(0, level - 1);
                }
                else
                {
                    _direction = OptUp;
                    CurrentLevel = Math.Min(_maxLevel, level + 1);
                }

                return;
            }

            if (_direction == OptUp)
            {
                if (level >= 1 && _work[level] < _work[level - 1] && level < _maxLevel)
                {
                    CurrentLevel = Math.Min(_maxLevel, level + 1);
                }
                else
                {
                    CurrentLevel = Math.Max(0, level - 1);
                    _direction = OptDown;
                }

                return;
            }

            if (level < _maxLevel && _work[level] < _work[level + 1] && level > 0)
            {
                CurrentLevel = Math.Max(0, level - 1);
            }
            else
            {
                CurrentLevel = Math.Min(_maxLevel, level + 1);
                _direction = OptUp;
            }
        }
    }

    private static void InitializePositions(double[] x, double[] y, Random? random)
    {
        var source = random ?? Random.Shared;
        for (var i = 0; i < x.Length; i++)
        {
            x[i] = source.NextDouble();
            y[i] = source.NextDouble();
        }
    }

    internal static double EstimateNaturalLength(SfdpComponentGraph graph, double[] x, double[] y)
    {
        return Math.Max(AverageEdgeLength(graph, x, y), MinDistance);
    }

    internal static double DetermineRepulsiveExponent(SfdpComponentGraph graph)
        => ResolveRepulsiveExponent(graph, requestedRepulsiveExponent: null).EffectiveRepulsiveExponent;

    internal static SfdpRepulsiveExponentResolution ResolveRepulsiveExponent(
        SfdpComponentGraph graph,
        double? requestedRepulsiveExponent,
        string autoSource = "auto")
    {
        var powerLawGraph = IsPowerLawGraph(graph);
        var source = requestedRepulsiveExponent.HasValue ? "options" : autoSource;
        var effectiveRepulsiveExponent = requestedRepulsiveExponent ?? (powerLawGraph ? -1.8 : -1.0);
        if (effectiveRepulsiveExponent >= 0)
        {
            effectiveRepulsiveExponent = -1.0;
            source = "clamped_nonnegative";
        }

        return new SfdpRepulsiveExponentResolution(
            requestedRepulsiveExponent,
            effectiveRepulsiveExponent,
            source,
            powerLawGraph);
    }

    private static double AverageEdgeLength(SfdpComponentGraph graph, double[] x, double[] y)
    {
        if (graph.EdgeCount == 0)
        {
            return 1.0;
        }

        var distanceSum = 0.0;
        for (var i = 0; i < graph.NodeCount; i++)
        {
            for (var edgeIndex = 0; edgeIndex < graph.Neighbors[i].Length; edgeIndex++)
            {
                var neighbor = graph.Neighbors[i][edgeIndex];
                if (neighbor <= i)
                {
                    continue;
                }

                var dx = x[i] - x[neighbor];
                var dy = y[i] - y[neighbor];
                distanceSum += Math.Sqrt(dx * dx + dy * dy);
            }
        }

        return distanceSum / graph.EdgeCount;
    }

    private static bool IsPowerLawGraph(SfdpComponentGraph graph)
    {
        if (graph.NodeCount == 0)
        {
            return false;
        }

        var maxDegree = 0;
        for (var i = 0; i < graph.NodeCount; i++)
        {
            maxDegree = Math.Max(maxDegree, graph.Neighbors[i].Length);
        }

        if (maxDegree == 0)
        {
            return false;
        }

        var histogram = new int[maxDegree + 1];
        var maxBucket = 0;
        for (var i = 0; i < graph.NodeCount; i++)
        {
            var degree = graph.Neighbors[i].Length;
            histogram[degree]++;
            maxBucket = Math.Max(maxBucket, histogram[degree]);
        }

        return histogram[1] > 0.8 * maxBucket && histogram[1] > 0.3 * graph.NodeCount;
    }

    private static double UpdateStep(bool adaptiveCooling, double step, double forceNorm, double previousForceNorm)
    {
        if (!adaptiveCooling)
        {
            return CoolingFactor * step;
        }

        if (forceNorm >= previousForceNorm)
        {
            return CoolingFactor * step;
        }

        if (forceNorm <= 0.95 * previousForceNorm)
        {
            return 0.99 * step / CoolingFactor;
        }

        return step;
    }

    private static void CenterPositions(double[] x, double[] y)
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
}
