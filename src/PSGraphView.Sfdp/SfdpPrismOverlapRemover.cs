namespace PSGraphView.Sfdp;

internal static class SfdpPrismOverlapRemover
{
    private const double MinDistance = 0.0001;
    private const double DesiredScaleTolerance = 0.0001;
    private const int ShrinkScalingIterations = 15;
    private const double ExpansionWeightMultiplier = 100.0;
    private const double InitialScaling = -4.0;
    private const double ConvergenceEpsilon = 0.005;
    private const double LargeResidual = 100000.0;
    private const double MachineAccuracy = 1.0e-16;

    public static void RemoveOverlaps(
        double[] x,
        double[] y,
        double nodeRadius,
        double padding,
        SfdpOverlapRemovalBoxUnits boxUnits,
        double? overlapHalfWidth,
        double? overlapHalfHeight,
        int maxIterations,
        Random? random,
        CancellationToken cancellationToken,
        int? componentId = null,
        SfdpDiagnosticsWriter? diagnostics = null)
        => RemoveOverlaps(null, x, y, nodeRadius, padding, boxUnits, overlapHalfWidth, overlapHalfHeight, maxIterations, random, cancellationToken, componentId, diagnostics);

    public static void RemoveOverlaps(
        SfdpComponentGraph? graph,
        double[] x,
        double[] y,
        double nodeRadius,
        double padding,
        SfdpOverlapRemovalBoxUnits boxUnits,
        double? overlapHalfWidth,
        double? overlapHalfHeight,
        int maxIterations,
        Random? random,
        CancellationToken cancellationToken,
        int? componentId = null,
        SfdpDiagnosticsWriter? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        diagnostics ??= SfdpDiagnosticsWriter.Disabled;

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var effectiveHalfWidth = overlapHalfWidth ?? SfdpPrismBoxBuilder.GetDefaultHalfSize(nodeRadius, padding, boxUnits);
        var effectiveHalfHeight = overlapHalfHeight ?? overlapHalfWidth ?? effectiveHalfWidth;
        var boxSource = overlapHalfWidth.HasValue || overlapHalfHeight.HasValue ? "override" : "node_radius_padding";

        diagnostics.Write("overlap", "start", componentId,
        [
            ("nodeCount", x.Length),
            ("edgeCount", graph?.EdgeCount),
            ("nodeRadius", nodeRadius),
            ("padding", padding),
            ("boxSource", boxSource),
            ("boxUnits", boxUnits),
            ("boxHalfWidth", effectiveHalfWidth),
            ("boxHalfHeight", effectiveHalfHeight),
            ("maxIterations", maxIterations),
            ("initialScaling", InitialScaling)
        ]);

        if (x.Length <= 1 || maxIterations <= 0)
        {
            diagnostics.WriteWithCoordinates("overlap", "finish", x, y, componentId,
                data: [("reason", "skipped")]);
            return;
        }

        var boxes = overlapHalfWidth.HasValue || overlapHalfHeight.HasValue
            ? SfdpPrismBoxBuilder.BuildWithHalfSize(x, y, effectiveHalfWidth, effectiveHalfHeight)
            : SfdpPrismBoxBuilder.Build(x, y, nodeRadius, padding, boxUnits);
        WriteGeometry("pre_scale", graph, boxes, x, y, componentId, diagnostics);
        var halfWidth = boxes[0].HalfWidth;
        var halfHeight = boxes[0].HalfHeight;
        if (halfWidth <= 0.0 || halfHeight <= 0.0)
        {
            Recenter(x, y);
            diagnostics.WriteWithCoordinates("overlap", "finish", x, y, componentId,
                data: [("reason", "degenerate_boxes")]);
            return;
        }

        JitterExactDuplicates(x, y, Math.Max(halfWidth, halfHeight) * 0.01);
        var scaling = ScaleToEdgeLength(graph, boxes, x, y, InitialScaling);
        diagnostics.Write("overlap", "scale_to_edge_length", componentId,
        [
            ("applied", scaling.Applied),
            ("reason", scaling.Reason),
            ("averageLabelSize", scaling.AverageLabelSize),
            ("targetEdgeLength", scaling.TargetEdgeLength),
            ("averageEdgeLength", scaling.AverageEdgeLength),
            ("scaleFactor", scaling.ScaleFactor)
        ]);
        RefreshBoxCenters(boxes, x, y);
        WriteGeometry("post_scale", graph, boxes, x, y, componentId, diagnostics);

        var neighborhoodOnly = true;
        var shrink = false;
        var residual = LargeResidual;
        var iterations = 0;
        var lastRelaxationResidual = (double?)null;
        var lastMaxOverlap = double.NaN;
        var lastMinOverlap = double.NaN;
        var terminationReason = "completed";

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations = iteration + 1;

            var prismGraph = new SfdpPrismGraph(
                boxes,
                neighborhoodOnly ? [] : SfdpPrismOverlapGraphBuilder.Build(boxes),
                SfdpPrismProximityGraphBuilder.Build(x, y));

            var stressSystem = SfdpPrismModelBuilder.Build(prismGraph, neighborhoodOnly, MinDistance, ExpansionWeightMultiplier);
            var maxOverlap = stressSystem.MaxOverlapFactor;
            lastMaxOverlap = maxOverlap;
            lastMinOverlap = stressSystem.MinOverlapFactor;

            diagnostics.Write("overlap", "model", componentId, null, iteration,
            [
                ("neighborhoodOnly", neighborhoodOnly),
                ("shrink", shrink),
                ("proximityEdgeCount", prismGraph.ProximityEdges.Count),
                ("overlapEdgeCount", prismGraph.OverlapEdges.Count),
                ("combinedEdgeCount", stressSystem.CombinedEdgeCount),
                ("proximityOnlyEdgeCount", stressSystem.ProximityOnlyEdgeCount),
                ("overlapOnlyEdgeCount", stressSystem.OverlapOnlyEdgeCount),
                ("sharedEdgeCount", stressSystem.SharedEdgeCount),
                ("expandEdgeCount", stressSystem.ExpandEdgeCount),
                ("shrinkEdgeCount", stressSystem.ShrinkEdgeCount),
                ("maxOverlapFactor", stressSystem.MaxOverlapFactor),
                ("minOverlapFactor", stressSystem.MinOverlapFactor)
            ]);

            if (!neighborhoodOnly && shrink && maxOverlap < 1.0)
            {
                var scaleStart = Math.Min(1.0, maxOverlap * 1.0001);
                var shrinkScaling = OverlapScaling(
                    boxes,
                    x,
                    y,
                    scaleStart,
                    1.0,
                    DesiredScaleTolerance,
                    ShrinkScalingIterations,
                    componentId,
                    iteration,
                    diagnostics);
                diagnostics.Write("overlap", "shrink", componentId, null, iteration,
                [
                    ("enteredShrinkStage", true),
                    ("postSwitchMaxOverlap", maxOverlap),
                    ("postSwitchMinOverlap", stressSystem.MinOverlapFactor),
                    ("scaleStart", shrinkScaling.ScaleStart),
                    ("scaleStop", shrinkScaling.ScaleStop),
                    ("finalScaleStart", shrinkScaling.FinalScaleStart),
                    ("finalScaleStop", shrinkScaling.FinalScaleStop),
                    ("scaleBest", shrinkScaling.ScaleBest),
                    ("epsilon", DesiredScaleTolerance),
                    ("maxIterations", ShrinkScalingIterations),
                    ("scalingIterations", shrinkScaling.Iterations),
                    ("hasOverlapAtScaleStart", shrinkScaling.HasOverlapAtScaleStart),
                    ("hasOverlapAtScaleStop", shrinkScaling.HasOverlapAtScaleStop),
                    ("returnedEarlyAtScaleStart", shrinkScaling.ReturnedEarlyAtScaleStart)
                ]);
                maxOverlap = 1.0;
                lastMaxOverlap = maxOverlap;
            }

            if (CheckConvergence(maxOverlap, residual, hasPenaltyTerms: false, ConvergenceEpsilon))
            {
                diagnostics.Write("overlap", "iter", componentId, null, iteration,
                [
                    ("neighborhoodOnly", neighborhoodOnly),
                    ("shrink", shrink),
                    ("maxOverlap", maxOverlap),
                    ("minOverlap", stressSystem.MinOverlapFactor),
                    ("residual", lastRelaxationResidual),
                    ("overlapEdgeCount", prismGraph.OverlapEdges.Count),
                    ("proximityEdgeCount", prismGraph.ProximityEdges.Count),
                    ("converged", true),
                    ("relaxed", null)
                ]);

                if (!neighborhoodOnly)
                {
                    terminationReason = "converged";
                    break;
                }

                residual = LargeResidual;
                lastRelaxationResidual = null;
                neighborhoodOnly = false;
                shrink = true;
                diagnostics.Write("overlap", "mode_switch", componentId, null, iteration,
                [
                    ("neighborhoodOnly", neighborhoodOnly),
                    ("shrink", shrink)
                ]);
                WriteGeometry("mode_switch", graph, boxes, x, y, componentId, diagnostics, iteration);
                continue;
            }

            var relaxation = RelaxOverlapGraph(stressSystem, x, y, random);
            lastRelaxationResidual = relaxation.Residual;
            residual = relaxation.Residual ?? LargeResidual;
            diagnostics.Write("overlap", "iter", componentId, null, iteration,
            [
                ("neighborhoodOnly", neighborhoodOnly),
                ("shrink", shrink),
                ("maxOverlap", maxOverlap),
                ("minOverlap", stressSystem.MinOverlapFactor),
                ("residual", relaxation.Residual),
                ("overlapEdgeCount", prismGraph.OverlapEdges.Count),
                ("proximityEdgeCount", prismGraph.ProximityEdges.Count),
                ("zeroDistancePerturbations", relaxation.ZeroDistancePerturbations),
                ("converged", false),
                ("relaxed", relaxation.Changed)
            ]);

            if (!relaxation.Changed)
            {
                terminationReason = "relaxation_no_change";
                break;
            }

            RefreshBoxCenters(boxes, x, y);
        }

        if (terminationReason == "completed" && iterations >= maxIterations)
        {
            terminationReason = "max_iterations";
        }

        var finishGeometry = SfdpGeometrySummary.Create(graph, x, y);
        var finishAverageLabelSize = 0.0;
        for (var i = 0; i < boxes.Length; i++)
        {
            finishAverageLabelSize += boxes[i].HalfWidth + boxes[i].HalfHeight;
        }

        if (boxes.Length > 0)
        {
            finishAverageLabelSize /= boxes.Length;
        }

        WriteGeometry("finish", graph, boxes, x, y, componentId, diagnostics, iterations > 0 ? iterations - 1 : null);
        Recenter(x, y);
        diagnostics.WriteWithCoordinates("overlap", "finish", x, y, componentId,
            data:
            [
                ("iterations", iterations),
                ("neighborhoodOnly", neighborhoodOnly),
                ("shrink", shrink),
                ("terminationReason", terminationReason),
                ("residual", lastRelaxationResidual),
                ("finalMaxOverlap", lastMaxOverlap),
                ("finalMinOverlap", lastMinOverlap),
                ("finishMinX", finishGeometry.MinX),
                ("finishMinY", finishGeometry.MinY),
                ("finishMaxX", finishGeometry.MaxX),
                ("finishMaxY", finishGeometry.MaxY),
                ("finishWidth", finishGeometry.Width),
                ("finishHeight", finishGeometry.Height),
                ("finishDiagonal", finishGeometry.Diagonal),
                ("finishAverageEdgeLength", finishGeometry.AverageEdgeLength),
                ("finishAverageLabelSize", finishAverageLabelSize)
            ]);
    }

    private static SfdpEdgeLengthScalingResult ScaleToEdgeLength(
        SfdpComponentGraph? graph,
        IReadOnlyList<SfdpPrismNodeBox> boxes,
        double[] x,
        double[] y,
        double initialScaling)
    {
        if (graph is null || graph.EdgeCount <= 0 || initialScaling == 0.0)
        {
            return new SfdpEdgeLengthScalingResult(false, null, null, null, null, "graph_or_scaling_missing");
        }

        double targetEdgeLength;
        double? averageLabelSize = null;
        if (initialScaling < 0.0)
        {
            averageLabelSize = 0.0;
            for (var i = 0; i < boxes.Count; i++)
            {
                averageLabelSize += boxes[i].HalfWidth + boxes[i].HalfHeight;
            }

            averageLabelSize /= boxes.Count;
            targetEdgeLength = -initialScaling * averageLabelSize.Value;
        }
        else
        {
            targetEdgeLength = initialScaling;
        }

        if (targetEdgeLength <= 0.0)
        {
            return new SfdpEdgeLengthScalingResult(false, averageLabelSize, targetEdgeLength, null, null, "non_positive_target");
        }

        var averageEdgeLength = Math.Max(SfdpSingleLevelLayouter.EstimateNaturalLength(graph, x, y), MachineAccuracy);
        var scaleFactor = targetEdgeLength / averageEdgeLength;
        Scale(x, y, scaleFactor);
        return new SfdpEdgeLengthScalingResult(true, averageLabelSize, targetEdgeLength, averageEdgeLength, scaleFactor, "applied");
    }

    private static SfdpPrismRelaxationResult RelaxOverlapGraph(
        SfdpPrismStressSystem stressSystem,
        double[] x,
        double[] y,
        Random? random)
        => SfdpPrismRelaxationSolver.Relax(x, y, stressSystem, MinDistance, random);

    private static bool CheckConvergence(
        double maxOverlap,
        double residual,
        bool hasPenaltyTerms,
        double epsilon)
    {
        if (!hasPenaltyTerms)
        {
            return maxOverlap <= 1.0;
        }

        return residual < epsilon;
    }

    private static SfdpOverlapScalingResult OverlapScaling(
        SfdpPrismNodeBox[] boxes,
        double[] x,
        double[] y,
        double scaleStart,
        double scaleStop,
        double epsilon,
        int maxIterations,
        int? componentId,
        int overlapIteration,
        SfdpDiagnosticsWriter diagnostics)
    {
        var initialScaleStart = scaleStart;
        var initialScaleStop = scaleStop;
        double scaleBest;
        bool? hasOverlapAtScaleStart = null;
        bool? hasOverlapAtScaleStop = null;
        var iterations = 0;

        if (scaleStart <= 0.0)
        {
            scaleStart = 0.0;
        }
        else
        {
            Scale(x, y, scaleStart);
            RefreshBoxCenters(boxes, x, y);
            hasOverlapAtScaleStart = HasAnyOverlap(boxes);
            if (hasOverlapAtScaleStart == false)
            {
                return new SfdpOverlapScalingResult(
                    initialScaleStart,
                    initialScaleStop,
                    scaleStart,
                    scaleStop,
                    scaleStart,
                    iterations,
                    hasOverlapAtScaleStart,
                    hasOverlapAtScaleStop,
                    ReturnedEarlyAtScaleStart: true);
            }

            Scale(x, y, 1.0 / scaleStart);
            RefreshBoxCenters(boxes, x, y);
        }

        if (scaleStop < 0.0)
        {
            scaleStop = scaleStart == 0.0 ? epsilon : scaleStart;
            Scale(x, y, scaleStop);
            RefreshBoxCenters(boxes, x, y);

            do
            {
                scaleStop *= 2.0;
                Scale(x, y, 2.0);
                RefreshBoxCenters(boxes, x, y);
                hasOverlapAtScaleStop = HasAnyOverlap(boxes);
            }
            while (hasOverlapAtScaleStop == true);

            Scale(x, y, 1.0 / scaleStop);
            RefreshBoxCenters(boxes, x, y);
        }

        scaleBest = scaleStop;
        for (var scalingIteration = 0; scalingIteration < maxIterations && (scaleStop - scaleStart) > epsilon; scalingIteration++)
        {
            iterations = scalingIteration + 1;
            if (diagnostics.IncludeIterations)
            {
                diagnostics.Write("overlap", "scaling_iter", componentId, null, overlapIteration,
                [
                    ("scalingIteration", scalingIteration + 1),
                    ("scaleStart", scaleStart),
                    ("scaleStop", scaleStop),
                    ("scaleBest", scaleBest),
                    ("epsilon", epsilon)
                ]);
            }

            var scale = (scaleStart + scaleStop) * 0.5;
            Scale(x, y, scale);
            RefreshBoxCenters(boxes, x, y);
            var overlap = HasAnyOverlap(boxes);
            Scale(x, y, 1.0 / scale);
            RefreshBoxCenters(boxes, x, y);

            if (overlap)
            {
                scaleStart = scale;
            }
            else
            {
                scaleBest = scaleStop = scale;
            }
        }

        Scale(x, y, scaleBest);
        RefreshBoxCenters(boxes, x, y);
        return new SfdpOverlapScalingResult(
            initialScaleStart,
            initialScaleStop,
            scaleStart,
            scaleStop,
            scaleBest,
            iterations,
            hasOverlapAtScaleStart,
            hasOverlapAtScaleStop,
            ReturnedEarlyAtScaleStart: false);
    }

    private static bool HasAnyOverlap(IReadOnlyList<SfdpPrismNodeBox> boxes)
    {
        return SfdpPrismOverlapGraphBuilder.HasAnyOverlap(boxes);
    }

    private static void WriteGeometry(
        string stage,
        SfdpComponentGraph? graph,
        IReadOnlyList<SfdpPrismNodeBox> boxes,
        double[] x,
        double[] y,
        int? componentId,
        SfdpDiagnosticsWriter diagnostics,
        int? iteration = null)
    {
        var geometry = SfdpGeometrySummary.Create(graph, x, y);
        var averageLabelSize = 0.0;
        for (var i = 0; i < boxes.Count; i++)
        {
            averageLabelSize += boxes[i].HalfWidth + boxes[i].HalfHeight;
        }

        if (boxes.Count > 0)
        {
            averageLabelSize /= boxes.Count;
        }

        diagnostics.Write("overlap", "geometry", componentId, null, iteration,
        [
            ("stage", stage),
            ("minX", geometry.MinX),
            ("minY", geometry.MinY),
            ("maxX", geometry.MaxX),
            ("maxY", geometry.MaxY),
            ("width", geometry.Width),
            ("height", geometry.Height),
            ("diagonal", geometry.Diagonal),
            ("averageEdgeLength", geometry.AverageEdgeLength),
            ("averageLabelSize", averageLabelSize)
        ]);
    }

    private static void JitterExactDuplicates(double[] x, double[] y, double radius)
    {
        if (radius <= 0.0)
        {
            return;
        }

        var groups = new Dictionary<(double X, double Y), List<int>>();
        for (var i = 0; i < x.Length; i++)
        {
            var key = (x[i], y[i]);
            if (!groups.TryGetValue(key, out var nodeIndices))
            {
                nodeIndices = [];
                groups.Add(key, nodeIndices);
            }

            nodeIndices.Add(i);
        }

        foreach (var nodeIndices in groups.Values)
        {
            if (nodeIndices.Count <= 1)
            {
                continue;
            }

            for (var i = 0; i < nodeIndices.Count; i++)
            {
                var angle = 2.0 * Math.PI * i / nodeIndices.Count;
                var index = nodeIndices[i];
                x[index] += radius * Math.Cos(angle);
                y[index] += radius * Math.Sin(angle);
            }
        }
    }

    private static void Scale(double[] x, double[] y, double scale)
    {
        for (var i = 0; i < x.Length; i++)
        {
            x[i] *= scale;
            y[i] *= scale;
        }
    }

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
    private static void RefreshBoxCenters(SfdpPrismNodeBox[] boxes, double[] x, double[] y)
    {
        for (var i = 0; i < boxes.Length; i++)
        {
            boxes[i] = boxes[i] with { CenterX = x[i], CenterY = y[i] };
        }
    }

    private sealed record SfdpEdgeLengthScalingResult(
        bool Applied,
        double? AverageLabelSize,
        double? TargetEdgeLength,
        double? AverageEdgeLength,
        double? ScaleFactor,
        string Reason);

    private sealed record SfdpOverlapScalingResult(
        double ScaleStart,
        double ScaleStop,
        double FinalScaleStart,
        double FinalScaleStop,
        double ScaleBest,
        int Iterations,
        bool? HasOverlapAtScaleStart,
        bool? HasOverlapAtScaleStop,
        bool ReturnedEarlyAtScaleStart);
}
