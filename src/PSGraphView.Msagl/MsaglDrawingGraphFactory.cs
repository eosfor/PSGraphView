using System.Globalization;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Drawing;
using PSGraph.Model;
using MsaglColor = Microsoft.Msagl.Drawing.Color;

namespace PSGraphView.Msagl;

internal static class MsaglDrawingGraphFactory
{
    private static readonly MsaglColor[] GroupPalette =
    {
        MsaglColor.SteelBlue,
        MsaglColor.Orange,
        MsaglColor.Green,
        MsaglColor.Red,
        MsaglColor.Violet,
        MsaglColor.SaddleBrown,
        MsaglColor.Pink,
        MsaglColor.Gray,
        MsaglColor.Olive,
        MsaglColor.Cyan
    };

    public static Graph BuildFlatDrawingGraph(
        GraphView graph,
        string groupMetadataKey,
        bool disableGroupColors,
        bool showLabels,
        bool showArrows,
        double edgeLineWidth)
    {
        var drawingGraph = new Graph();
        var nodesById = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var viewNode in graph.Nodes)
        {
            var node = drawingGraph.AddNode(viewNode.Id);
            node.LabelText = showLabels ? viewNode.Label : string.Empty;
            node.Attr.Shape = Shape.Circle;
            node.Attr.Color = MsaglColor.DimGray;
            node.Attr.LineWidth = 1.0;
            node.Attr.LabelMargin = 0;
            node.Attr.FillColor = ResolveFill(viewNode, groupMetadataKey, disableGroupColors);
            nodesById[viewNode.Id] = node;
        }

        foreach (var viewEdge in graph.Edges)
        {
            if (!nodesById.ContainsKey(viewEdge.SourceId) || !nodesById.ContainsKey(viewEdge.TargetId))
            {
                continue;
            }

            var edge = drawingGraph.AddEdge(viewEdge.SourceId, viewEdge.TargetId);
            edge.Attr.Color = MsaglColor.LightGray;
            edge.Attr.ArrowheadAtTarget = showArrows ? ArrowStyle.Normal : ArrowStyle.None;
            edge.Attr.ArrowheadAtSource = ArrowStyle.None;
            edge.Attr.LineWidth = edgeLineWidth;
        }

        return drawingGraph;
    }

    public static Graph BuildClassicDrawingGraph(GraphView graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var drawingGraph = new Graph();
        var nodesById = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var viewNode in graph.Nodes)
        {
            var node = drawingGraph.AddNode(viewNode.Id);
            node.LabelText = viewNode.Label;
            node.Attr.FillColor = MsaglColor.Azure;
            nodesById[viewNode.Id] = node;
        }

        foreach (var viewEdge in graph.Edges)
        {
            if (!nodesById.ContainsKey(viewEdge.SourceId) || !nodesById.ContainsKey(viewEdge.TargetId))
            {
                continue;
            }

            drawingGraph.AddEdge(viewEdge.SourceId, viewEdge.TargetId);
        }

        return drawingGraph;
    }

    public static void ApplyCircularNodeBoundaries(Graph drawingGraph, double nodeRadius, bool showLabels, double labelFontSize)
    {
        foreach (var node in drawingGraph.Nodes)
        {
            node.GeometryNode.BoundaryCurve = CurveFactory.CreateCircle(nodeRadius, new Point(0, 0));
            if (node.Label is null)
            {
                continue;
            }

            node.Label.FontSize = labelFontSize;
            if (showLabels)
            {
                node.Label.Width = Math.Max(6.0, node.LabelText.Length * labelFontSize * 0.56);
                node.Label.Height = Math.Max(6.0, labelFontSize + 2.0);
            }
            else
            {
                node.Label.Width = 0;
                node.Label.Height = 0;
            }
        }
    }

    public static void ApplyClassicNodeBoundaries(Graph drawingGraph)
    {
        foreach (var node in drawingGraph.Nodes)
        {
            var labelFontSize = node.Label?.FontSize ?? 12.0;
            var width = 0.68 * (node.LabelText.Length * labelFontSize);
            node.GeometryNode.BoundaryCurve = CurveFactory.CreateRectangleWithRoundedCorners(width, 40, 3, 2, new Point(0, 0));

            if (node.Label is null)
            {
                continue;
            }

            node.Label.Width = node.Width * 0.99;
            node.Label.Height = 40;
            node.Attr.FillColor = MsaglColor.Azure;
        }
    }

    private static MsaglColor ResolveFill(GraphViewNode node, string groupMetadataKey, bool disableGroupColors)
    {
        if (disableGroupColors || string.IsNullOrWhiteSpace(groupMetadataKey))
        {
            return MsaglColor.DimGray;
        }

        if (node.Metadata.TryGetValue(groupMetadataKey, out var value) && TryGetGroup(value, out var group))
        {
            return GroupPalette[Math.Abs(group) % GroupPalette.Length];
        }

        return MsaglColor.DimGray;
    }

    private static bool TryGetGroup(object? value, out int group)
    {
        group = 0;
        switch (value)
        {
            case int i:
                group = i;
                return true;
            case long l when l >= int.MinValue && l <= int.MaxValue:
                group = (int)l;
                return true;
            case double d:
                group = (int)d;
                return true;
            case float f:
                group = (int)f;
                return true;
            default:
                return value is not null && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out group);
        }
    }
}