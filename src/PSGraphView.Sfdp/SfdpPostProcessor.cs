namespace PSGraphView.Sfdp;

internal static class SfdpPostProcessor
{
    public static void Apply(
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        SfdpOptions options,
        SfdpSingleLevelLayouter singleLevelLayouter,
        Random? random,
        int componentId,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken,
        SfdpRepulsiveExponentResolution? repulsiveExponentResolution = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(singleLevelLayouter);

        if (x.Length != y.Length || x.Length != graph.NodeCount)
        {
            throw new ArgumentException("Post-process coordinates must match graph node count.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        diagnostics.Write("postprocess", "start", componentId: componentId,
        [
            ("nodeCount", graph.NodeCount),
            ("smoothing", options.Smoothing),
            ("smoothingIterations", options.SmoothingIterations),
            ("applyPrincipalComponentRotation", options.ApplyPrincipalComponentRotation),
            ("rotationDegrees", options.RotationDegrees),
            ("enableOverlapRemoval", options.EnableOverlapRemoval),
            ("overlapRemovalIterations", options.OverlapRemovalIterations),
            ("overlapRemovalPadding", options.OverlapRemovalPadding),
            ("overlapRemovalBoxUnits", options.OverlapRemovalBoxUnits),
            ("overlapRemovalHalfWidth", options.OverlapRemovalHalfWidth),
            ("overlapRemovalHalfHeight", options.OverlapRemovalHalfHeight)
        ]);

        ApplySmoothing(graph, x, y, options, singleLevelLayouter, random, componentId, diagnostics, cancellationToken, repulsiveExponentResolution);
        WriteGeometry("before_principal_component_rotation", graph, x, y, componentId, diagnostics);

        if (options.ApplyPrincipalComponentRotation)
        {
            ApplyPrincipalComponentRotation(x, y);
            diagnostics.WriteWithCoordinates("postprocess", "pcp_rotate", x, y, componentId);
            WriteGeometry("after_principal_component_rotation", graph, x, y, componentId, diagnostics);
        }

        if (Math.Abs(options.RotationDegrees) > double.Epsilon)
        {
            Rotate(x, y, options.RotationDegrees);
            diagnostics.WriteWithCoordinates("postprocess", "rotate", x, y, componentId, data: [("rotationDegrees", options.RotationDegrees)]);
            WriteGeometry("after_rotation", graph, x, y, componentId, diagnostics);
        }

        if (options.EnableOverlapRemoval)
        {
            WriteGeometry("before_overlap_removal", graph, x, y, componentId, diagnostics);
            SfdpPrismOverlapRemover.RemoveOverlaps(
                graph,
                x,
                y,
                options.NodeRadius,
                options.OverlapRemovalPadding,
                options.OverlapRemovalBoxUnits,
                options.OverlapRemovalHalfWidth,
                options.OverlapRemovalHalfHeight,
                options.OverlapRemovalIterations,
                random,
                cancellationToken,
                componentId,
                diagnostics);
            WriteGeometry("after_overlap_removal", graph, x, y, componentId, diagnostics);
        }

        diagnostics.WriteWithCoordinates("postprocess", "finish", x, y, componentId: componentId,
            data: [("nodeCount", graph.NodeCount)]);
    }

    private static void WriteGeometry(
        string stage,
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        int componentId,
        SfdpDiagnosticsWriter diagnostics)
    {
        var geometry = SfdpGeometrySummary.Create(graph, x, y);
        diagnostics.Write("postprocess", "geometry", componentId,
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

    internal static void ApplyPrincipalComponentRotation(double[] x, double[] y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length <= 1)
        {
            return;
        }

        var centerX = 0.0;
        var centerY = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            centerX += x[i];
            centerY += y[i];
        }

        centerX /= x.Length;
        centerY /= x.Length;

        var y00 = 0.0;
        var y01 = 0.0;
        var y11 = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            x[i] -= centerX;
            y[i] -= centerY;
            y00 += x[i] * x[i];
            y01 += x[i] * y[i];
            y11 += y[i] * y[i];
        }

        double axisX;
        double axisY;
        if (Math.Abs(y01) <= double.Epsilon)
        {
            axisX = 0.0;
            axisY = 1.0;
        }
        else
        {
            axisX = -(-y00 + y11 - Math.Sqrt((y00 * y00) + (4.0 * y01 * y01) - (2.0 * y00 * y11) + (y11 * y11))) / (2.0 * y01);
            axisY = 1.0;
        }

        var axisLength = Math.Sqrt(1.0 + (axisX * axisX));
        axisX /= axisLength;
        axisY /= axisLength;

        for (var i = 0; i < x.Length; i++)
        {
            var rotatedX = (x[i] * axisX) + (y[i] * axisY);
            var rotatedY = (-x[i] * axisY) + (y[i] * axisX);
            x[i] = rotatedX;
            y[i] = rotatedY;
        }
    }

    internal static void Rotate(double[] x, double[] y, double angleDegrees)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Length <= 1)
        {
            return;
        }

        var centerX = 0.0;
        var centerY = 0.0;
        for (var i = 0; i < x.Length; i++)
        {
            centerX += x[i];
            centerY += y[i];
        }

        centerX /= x.Length;
        centerY /= y.Length;

        var radians = -angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        for (var i = 0; i < x.Length; i++)
        {
            var dx = x[i] - centerX;
            var dy = y[i] - centerY;
            var rotatedX = dx * cos + dy * sin;
            var rotatedY = -dx * sin + dy * cos;
            x[i] = rotatedX + centerX;
            y[i] = rotatedY + centerY;
        }
    }

    private static void ApplySmoothing(
        SfdpComponentGraph graph,
        double[] x,
        double[] y,
        SfdpOptions options,
        SfdpSingleLevelLayouter singleLevelLayouter,
        Random? random,
        int componentId,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken,
        SfdpRepulsiveExponentResolution? repulsiveExponentResolution)
    {
        if (options.Smoothing == SfdpSmoothingMode.None || options.SmoothingIterations <= 0)
        {
            diagnostics.Write("postprocess", "smoothing_skipped", componentId: componentId,
            [
                ("mode", options.Smoothing),
                ("iterations", options.SmoothingIterations)
            ]);
            return;
        }

        diagnostics.Write("postprocess", "smoothing_start", componentId: componentId,
        [
            ("mode", options.Smoothing),
            ("iterations", options.SmoothingIterations)
        ]);

        if (options.Smoothing == SfdpSmoothingMode.Spring)
        {
            var refinedLayout = singleLevelLayouter.LayoutComponent(
                graph,
                options,
                random,
                cancellationToken,
                x,
                y,
                Math.Min(options.MaxIterations, options.SmoothingIterations),
                false,
                options.NaturalLength,
                repulsiveExponentResolution,
                componentId: componentId,
                diagnostics: diagnostics);

            Array.Copy(refinedLayout.X, x, x.Length);
            Array.Copy(refinedLayout.Y, y, y.Length);
            diagnostics.WriteWithCoordinates("postprocess", "smoothing_finish", x, y, componentId,
                data: [("mode", options.Smoothing)]);
            return;
        }

        SfdpStressMajorizationSmoother.Smooth(
            graph,
            x,
            y,
            options.Smoothing,
            options.SmoothingIterations,
            componentId,
            diagnostics,
            cancellationToken);

        diagnostics.WriteWithCoordinates("postprocess", "smoothing_finish", x, y, componentId,
            data: [("mode", options.Smoothing)]);
    }

}
