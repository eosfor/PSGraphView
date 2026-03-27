using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using PSGraph.Model;

namespace PSGraphView.Msagl;

public sealed class MsaglSugiyamaExporter
{
    public string Export(GraphView graph, MsaglSugiyamaOptions? options = null)
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

        LayoutHelpers.CalculateLayout(drawingGraph.GeometryGraph, settings, null);
        var svg = MsaglSvgRenderer.RenderSvg(
            drawingGraph,
            options.BackgroundColor,
            options.ShowArrows,
            options.ShowLabels ? options.LabelFontSize : null);

        return MsaglSugiyamaSvgPostProcessor.PostProcess(svg, graph, options);
    }

    private static EdgeRoutingMode ResolveEdgeRouting(string edgeRouting)
    {
        return Enum.TryParse<EdgeRoutingMode>(edgeRouting, true, out var mode)
            ? mode
            : EdgeRoutingMode.SugiyamaSplines;
    }
}