using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

public sealed class SfdpSvgExporter
{
    private readonly SfdpLayoutEngine _layoutEngine = new();

    public string Export(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new SfdpOptions();

        using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
        var layout = _layoutEngine.Layout(graph, options, diagnostics, cancellationToken);
        var layoutScale = SfdpViewportCalculator.GetLayoutScale(options);
        var outputX = SfdpViewportCalculator.ScaleCoordinates(layout.X, layoutScale);
        var outputY = SfdpViewportCalculator.ScaleCoordinates(layout.Y, layoutScale);
        var outputBounds = ComputeOutputBounds(outputX, outputY, options.NodeRadius);
        var labelPlacements = options.ShowLabels
            ? SfdpLabelLayouter.PlaceLabels(graph.Nodes.Select(node => node.Label).ToArray(), outputX, outputY, options)
            : Array.Empty<SfdpLabelLayouter.LabelPlacement>();
        var contentBounds = ExpandBoundsForLabels(outputBounds, labelPlacements);
        var viewportMetrics = SfdpViewportCalculator.CalculateSvgViewport(contentBounds, options);
        var translatedX = TranslateCoordinates(outputX, viewportMetrics.TranslationX);
        var translatedY = TranslateCoordinates(outputY, viewportMetrics.TranslationY);
        var translatedBounds = TranslateBounds(outputBounds, viewportMetrics.TranslationX, viewportMetrics.TranslationY);
        var translatedLabels = TranslateLabels(labelPlacements, viewportMetrics.TranslationX, viewportMetrics.TranslationY);
        var routedEdges = SfdpEdgeRouter.RouteEdges(graph, translatedX, translatedY, options);
        var renderScene = SfdpRenderSceneBuilder.Build(graph, translatedX, translatedY, translatedBounds, translatedLabels, routedEdges, viewportMetrics, options);
        SfdpRenderDiagnostics.WriteScene(diagnostics, graph, renderScene.Scene, options);
        var graphGeometry = SfdpGeometrySummary.Create(SfdpGraphBuilder.BuildUndirectedCsr(SfdpGraphBuilder.BuildIndexed(graph)), layout.X, layout.Y);
        WriteSvgGeometry(diagnostics, "export_input", graphGeometry, options, labelPlacements, null, null, null, null, null);
        WriteSvgGeometry(
            diagnostics,
            "viewbox",
            graphGeometry,
            options,
            translatedLabels,
            renderScene.ContentBounds,
            renderScene.ViewportMetrics.PaddingX,
            renderScene.Scene.Viewport.OutputWidth,
            renderScene.Scene.Viewport.OutputHeight,
            $"{Format(renderScene.Scene.Viewport.ViewBoxMinX)} {Format(renderScene.Scene.Viewport.ViewBoxMinY)} {Format(renderScene.Scene.Viewport.ViewBoxWidth)} {Format(renderScene.Scene.Viewport.ViewBoxHeight)}");
        SfdpRenderDiagnostics.WriteViewport(
            diagnostics,
            renderScene.ContentBounds,
            renderScene.ViewportMetrics,
            options);
        var document = GraphSvgRenderSceneWriter.CreateDocument(renderScene.Scene);
        SfdpRenderDiagnostics.WriteSvgStructure(diagnostics, document.Root!);
        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static void WriteSvgGeometry(
        SfdpDiagnosticsWriter diagnostics,
        string stage,
        SfdpGeometrySummary graphGeometry,
        SfdpOptions options,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labels,
        SfdpBoundingBox? contentBounds,
        double? padding,
        double? outputWidth,
        double? outputHeight,
        string? viewBox)
    {
        var averageLabelSize = default(double?);
        if (labels.Count > 0)
        {
            averageLabelSize = 0.0;
            foreach (var label in labels)
            {
                averageLabelSize += label.Width + label.Height;
            }

            averageLabelSize /= labels.Count;
        }

        diagnostics.Write("svg", "geometry",
        [
            ("stage", stage),
            ("minX", graphGeometry.MinX),
            ("minY", graphGeometry.MinY),
            ("maxX", graphGeometry.MaxX),
            ("maxY", graphGeometry.MaxY),
            ("width", graphGeometry.Width),
            ("height", graphGeometry.Height),
            ("diagonal", graphGeometry.Diagonal),
            ("averageEdgeLength", graphGeometry.AverageEdgeLength),
            ("averageLabelSize", averageLabelSize),
            ("padding", padding),
            ("contentMinX", contentBounds?.MinX),
            ("contentMinY", contentBounds?.MinY),
            ("contentMaxX", contentBounds?.MaxX),
            ("contentMaxY", contentBounds?.MaxY),
            ("contentWidth", contentBounds?.Width),
            ("contentHeight", contentBounds?.Height),
            ("outputWidth", outputWidth),
            ("outputHeight", outputHeight),
            ("nodeRadius", options.NodeRadius),
            ("viewBox", viewBox)
        ]);
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

    private static string Format(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
