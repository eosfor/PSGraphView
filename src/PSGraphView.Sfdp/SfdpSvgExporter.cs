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
        var labelPlacements = options.ShowLabels
            ? SfdpLabelLayouter.PlaceLabels(graph.Nodes.Select(node => node.Label).ToArray(), layout.X, layout.Y, options)
            : Array.Empty<SfdpLabelLayouter.LabelPlacement>();
        var routedEdges = SfdpEdgeRouter.RouteEdges(graph, layout.X, layout.Y, options);
        var renderScene = SfdpRenderSceneBuilder.Build(graph, layout, labelPlacements, routedEdges, options);
        SfdpRenderDiagnostics.WriteScene(diagnostics, graph, renderScene.Scene, options);
        var graphGeometry = SfdpGeometrySummary.Create(SfdpGraphBuilder.BuildUndirectedCsr(SfdpGraphBuilder.BuildIndexed(graph)), layout.X, layout.Y);
        WriteSvgGeometry(diagnostics, "export_input", graphGeometry, options, labelPlacements, null, null, null, null, null);
        var viewport = renderScene.Scene.Viewport;
        WriteSvgGeometry(
            diagnostics,
            "viewbox",
            graphGeometry,
            options,
            labelPlacements,
            renderScene.ContentBounds,
            renderScene.Padding,
            viewport.OutputWidth,
            viewport.OutputHeight,
            $"{Format(viewport.ViewBoxMinX)} {Format(viewport.ViewBoxMinY)} {Format(viewport.ViewBoxWidth)} {Format(viewport.ViewBoxHeight)}");
        SfdpRenderDiagnostics.WriteViewport(
            diagnostics,
            renderScene.ContentBounds,
            viewport,
            renderScene.Padding,
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

    private static string Format(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
