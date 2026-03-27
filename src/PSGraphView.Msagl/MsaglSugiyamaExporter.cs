using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using PSGraph.Model;

namespace PSGraphView.Msagl;

public sealed class MsaglSugiyamaExporter
{
    public string Export(GraphView graph, MsaglSugiyamaOptions? options = null, CancelToken? cancelToken = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new MsaglSugiyamaOptions();

        var drawingGraph = MsaglDrawingGraphFactory.BuildFlatDrawingGraph(
            graph,
            options.GroupMetadataKey,
            options.DisableGroupColors,
            options.ShowLabels,
            options.ShowArrows,
            options.EdgeLineWidth);

        drawingGraph.CreateGeometryGraph();
        MsaglDrawingGraphFactory.ApplyCircularNodeBoundaries(drawingGraph, options.NodeRadius, options.ShowLabels, options.LabelFontSize);

        var settings = new SugiyamaLayoutSettings
        {
            LayerSeparation = options.LayerSeparation,
            NodeSeparation = options.NodeSeparation,
            Transformation = string.Equals(options.Direction, "Horizontal", StringComparison.OrdinalIgnoreCase)
                ? PlaneTransformation.Rotation(Math.PI / 2.0)
                : PlaneTransformation.UnitTransformation
        };

        settings.EdgeRoutingSettings.EdgeRoutingMode = ResolveEdgeRouting(options.EdgeRouting);
        settings.EdgeRoutingSettings.CornerRadius = 6.0;
        settings.EdgeRoutingSettings.PolylinePadding = 2.0;

        MsaglLayoutRunner.CalculateLayout(drawingGraph.GeometryGraph, settings, cancelToken);
        var svg = MsaglSvgRenderer.RenderSvg(
            drawingGraph,
            options.BackgroundColor,
            options.ShowArrows,
            options.ShowLabels ? options.LabelFontSize : null,
            options.Width,
            options.Height);

        return MsaglSugiyamaSvgPostProcessor.PostProcess(svg, graph, options);
    }

    private static EdgeRoutingMode ResolveEdgeRouting(string edgeRouting)
    {
        return Enum.TryParse<EdgeRoutingMode>(edgeRouting, true, out var mode)
            ? mode
            : EdgeRoutingMode.SugiyamaSplines;
    }
}
