using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Miscellaneous;
using PSGraph.Model;

namespace PSGraphView.Msagl;

public sealed class MsaglMdsExporter
{
    public string Export(GraphView graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var drawingGraph = MsaglDrawingGraphFactory.BuildClassicDrawingGraph(graph);
        drawingGraph.CreateGeometryGraph();
        MsaglDrawingGraphFactory.ApplyClassicNodeBoundaries(drawingGraph);

        LayoutHelpers.CalculateLayout(drawingGraph.GeometryGraph, new MdsLayoutSettings(), null);
        return MsaglSvgRenderer.RenderRawSvg(drawingGraph);
    }
}