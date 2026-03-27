using Microsoft.Msagl.Core;
using Microsoft.Msagl.Layout.MDS;
using PSGraph.Model;

namespace PSGraphView.Msagl;

public sealed class MsaglMdsExporter
{
    public string Export(GraphView graph, double? width = null, double? height = null, CancelToken? cancelToken = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var drawingGraph = MsaglDrawingGraphFactory.BuildClassicDrawingGraph(graph);
        drawingGraph.CreateGeometryGraph();
        MsaglDrawingGraphFactory.ApplyClassicNodeBoundaries(drawingGraph);

        MsaglLayoutRunner.CalculateLayout(drawingGraph.GeometryGraph, new MdsLayoutSettings(), cancelToken);
        return MsaglSvgRenderer.RenderRawSvg(drawingGraph, width, height);
    }
}
