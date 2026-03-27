using Microsoft.Msagl.Core;
using Microsoft.Msagl.Layout.Incremental;
using PSGraph.Model;

namespace PSGraphView.Msagl;

public sealed class MsaglFastIncrementalExporter
{
    public string Export(GraphView graph, MsaglFastIncrementalOptions? options = null, CancelToken? cancelToken = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new MsaglFastIncrementalOptions();

        var drawingGraph = MsaglDrawingGraphFactory.BuildFlatDrawingGraph(
            graph,
            options.GroupMetadataKey,
            options.DisableGroupColors,
            options.ShowLabels,
            options.ShowArrows,
            options.EdgeLineWidth);

        drawingGraph.CreateGeometryGraph();
        MsaglDrawingGraphFactory.ApplyCircularNodeBoundaries(drawingGraph, options.NodeRadius, options.ShowLabels, options.LabelFontSize);

        var settings = new FastIncrementalLayoutSettings
        {
            ApplyForces = true,
            InterComponentForces = true,
            AvoidOverlaps = true,
            ApproximateRepulsion = true,
            RouteEdges = false,
            RespectEdgePorts = false,
            RepulsiveForceConstant = options.RepulsiveForceConstant,
            AttractiveForceConstant = options.AttractiveForceConstant,
            AttractiveInterClusterForceConstant = options.AttractiveInterClusterForceConstant,
            GravityConstant = options.GravityConstant,
            InitialStepSize = 2.0,
            Friction = 0.8,
            Decay = 0.85,
            NodeSeparation = options.NodeSeparation,
            MaxIterations = options.MaxIterations,
            MinorIterations = options.MinorIterations,
            ProjectionIterations = options.ProjectionIterations
        };

        MsaglLayoutRunner.CalculateLayout(drawingGraph.GeometryGraph, settings, cancelToken);
        return MsaglSvgRenderer.RenderSvg(
            drawingGraph,
            options.BackgroundColor,
            options.ShowArrows,
            options.ShowLabels ? options.LabelFontSize : null,
            options.Width,
            options.Height);
    }
}
