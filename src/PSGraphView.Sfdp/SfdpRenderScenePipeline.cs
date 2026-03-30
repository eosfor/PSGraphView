using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

internal static class SfdpRenderScenePipeline
{
    public static SfdpPreparedRenderScene Prepare(
        GraphView graph,
        SfdpOptions options,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var layoutEngine = new SfdpLayoutEngine();
        var layout = layoutEngine.Layout(graph, options, diagnostics, cancellationToken);
        var layoutScale = SfdpViewportCalculator.GetLayoutScale(options);
        var outputX = SfdpViewportCalculator.ScaleCoordinates(layout.X, layoutScale);
        var outputY = SfdpViewportCalculator.ScaleCoordinates(layout.Y, layoutScale);
        var outputBounds = ComputeOutputBounds(outputX, outputY, options.NodeRadius);
        var labelPlacements = options.ShowLabels
            ? SfdpLabelLayouter.PlaceLabels(graph.Nodes.Select(node => node.Label).ToArray(), outputX, outputY, options)
            : Array.Empty<SfdpLabelLayouter.LabelPlacement>();
        var routedEdges = SfdpEdgeRouter.RouteEdges(graph, outputX, outputY, options);
        var contentBounds = ExpandBoundsForLabels(outputBounds, labelPlacements);
        contentBounds = ExpandBoundsForEdges(contentBounds, routedEdges);
        var viewportMetrics = SfdpViewportCalculator.CalculateSvgViewport(contentBounds, options);
        var translatedX = TranslateCoordinates(outputX, viewportMetrics.TranslationX);
        var translatedY = TranslateCoordinates(outputY, viewportMetrics.TranslationY);
        var translatedBounds = TranslateBounds(contentBounds, viewportMetrics.TranslationX, viewportMetrics.TranslationY);
        var translatedLabels = TranslateLabels(labelPlacements, viewportMetrics.TranslationX, viewportMetrics.TranslationY);
        var translatedEdges = TranslateEdges(routedEdges, viewportMetrics.TranslationX, viewportMetrics.TranslationY);
        var renderScene = SfdpRenderSceneBuilder.Build(graph, translatedX, translatedY, translatedBounds, translatedLabels, translatedEdges, viewportMetrics, options);

        SfdpRenderDiagnostics.WriteScene(diagnostics, graph, renderScene.Scene, options);
        SfdpRenderDiagnostics.WriteViewport(diagnostics, renderScene.ContentBounds, renderScene.ViewportMetrics, options);

        return new SfdpPreparedRenderScene(
            renderScene.Scene,
            renderScene.ContentBounds,
            renderScene.ViewportMetrics,
            translatedLabels,
            SfdpGeometrySummary.Create(SfdpGraphBuilder.BuildUndirectedCsr(SfdpGraphBuilder.BuildIndexed(graph)), layout.X, layout.Y));
    }

    private static SfdpBoundingBox ExpandBoundsForLabels(
        SfdpBoundingBox bounds,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labels)
    {
        if (labels.Count == 0)
        {
            return bounds;
        }

        var minX = bounds.MinX;
        var minY = bounds.MinY;
        var maxX = bounds.MaxX;
        var maxY = bounds.MaxY;

        foreach (var label in labels)
        {
            minX = Math.Min(minX, label.X);
            minY = Math.Min(minY, label.Y);
            maxX = Math.Max(maxX, label.X + label.Width);
            maxY = Math.Max(maxY, label.Y + label.Height);
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }

    private static double[] TranslateCoordinates(IReadOnlyList<double> coordinates, double offset)
    {
        var translated = new double[coordinates.Count];
        for (var index = 0; index < coordinates.Count; index++)
        {
            translated[index] = coordinates[index] + offset;
        }

        return translated;
    }

    private static IReadOnlyList<SfdpLabelLayouter.LabelPlacement> TranslateLabels(
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labels,
        double offsetX,
        double offsetY)
    {
        if (labels.Count == 0)
        {
            return labels;
        }

        var translated = new SfdpLabelLayouter.LabelPlacement[labels.Count];
        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            translated[index] = new SfdpLabelLayouter.LabelPlacement(
                label.NodeIndex,
                label.X + offsetX,
                label.Y + offsetY,
                label.Width,
                label.Height);
        }

        return translated;
    }

    private static SfdpBoundingBox TranslateBounds(SfdpBoundingBox bounds, double offsetX, double offsetY)
    {
        return new SfdpBoundingBox(
            bounds.MinX + offsetX,
            bounds.MinY + offsetY,
            bounds.MaxX + offsetX,
            bounds.MaxY + offsetY);
    }

    private static IReadOnlyList<SfdpEdgeRouter.RoutedEdge> TranslateEdges(
        IReadOnlyList<SfdpEdgeRouter.RoutedEdge> edges,
        double offsetX,
        double offsetY)
    {
        if (edges.Count == 0)
        {
            return edges;
        }

        var translated = new SfdpEdgeRouter.RoutedEdge[edges.Count];
        for (var index = 0; index < edges.Count; index++)
        {
            var edge = edges[index];
            var translatedPoints = new SfdpEdgeRouter.RoutedPoint[edge.Points.Count];
            for (var pointIndex = 0; pointIndex < edge.Points.Count; pointIndex++)
            {
                var point = edge.Points[pointIndex];
                translatedPoints[pointIndex] = new SfdpEdgeRouter.RoutedPoint(point.X + offsetX, point.Y + offsetY);
            }

            translated[index] = new SfdpEdgeRouter.RoutedEdge(
                edge.SourceIndex,
                edge.TargetIndex,
                SfdpEdgeRouter.BuildPathData(translatedPoints),
                translatedPoints);
        }

        return translated;
    }

    private static SfdpBoundingBox ExpandBoundsForEdges(
        SfdpBoundingBox bounds,
        IReadOnlyList<SfdpEdgeRouter.RoutedEdge> routedEdges)
    {
        if (routedEdges.Count == 0)
        {
            return bounds;
        }

        var minX = bounds.MinX;
        var minY = bounds.MinY;
        var maxX = bounds.MaxX;
        var maxY = bounds.MaxY;

        foreach (var routedEdge in routedEdges)
        {
            var edgeBounds = SfdpEdgeRouter.ComputeBounds(routedEdge.Points);
            minX = Math.Min(minX, edgeBounds.MinX);
            minY = Math.Min(minY, edgeBounds.MinY);
            maxX = Math.Max(maxX, edgeBounds.MaxX);
            maxY = Math.Max(maxY, edgeBounds.MaxY);
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }

    private static SfdpBoundingBox ComputeOutputBounds(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        double nodeRadius)
    {
        if (x.Count != y.Count)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        if (x.Count == 0)
        {
            return new SfdpBoundingBox(0.0, 0.0, 0.0, 0.0);
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        for (var index = 0; index < x.Count; index++)
        {
            minX = Math.Min(minX, x[index] - nodeRadius);
            minY = Math.Min(minY, y[index] - nodeRadius);
            maxX = Math.Max(maxX, x[index] + nodeRadius);
            maxY = Math.Max(maxY, y[index] + nodeRadius);
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }
}

internal sealed record SfdpPreparedRenderScene(
    GraphRenderScene Scene,
    SfdpBoundingBox ContentBounds,
    SfdpViewportMetrics ViewportMetrics,
    IReadOnlyList<SfdpLabelLayouter.LabelPlacement> LabelPlacements,
    SfdpGeometrySummary GraphGeometry);
