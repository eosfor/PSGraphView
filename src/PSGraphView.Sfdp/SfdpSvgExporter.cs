using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

public sealed class SfdpSvgExporter
{
    public string Export(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new SfdpOptions();

        using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
        var prepared = SfdpRenderScenePipeline.Prepare(graph, options, diagnostics, cancellationToken);
        WriteSvgGeometry(diagnostics, "export_input", prepared.GraphGeometry, options, prepared.LabelPlacements, null, null, null, null, null);
        WriteSvgGeometry(
            diagnostics,
            "viewbox",
            prepared.GraphGeometry,
            options,
            prepared.LabelPlacements,
            prepared.ContentBounds,
            prepared.ViewportMetrics.PaddingX,
            prepared.Scene.Viewport.OutputWidth,
            prepared.Scene.Viewport.OutputHeight,
            $"{Format(prepared.Scene.Viewport.ViewBoxMinX)} {Format(prepared.Scene.Viewport.ViewBoxMinY)} {Format(prepared.Scene.Viewport.ViewBoxWidth)} {Format(prepared.Scene.Viewport.ViewBoxHeight)}");
        var document = GraphSvgRenderSceneWriter.CreateDocument(prepared.Scene);
        SfdpRenderDiagnostics.WriteSvgStructure(diagnostics, document.Root!);
        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    internal static void WriteSvgGeometry(
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
    internal static string Format(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
