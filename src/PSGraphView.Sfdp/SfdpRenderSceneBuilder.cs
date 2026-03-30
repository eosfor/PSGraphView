using System.Globalization;
using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

internal static class SfdpRenderSceneBuilder
{
    private const string DefaultNodeStroke = "#555555";
    private const string DefaultLabelFill = "#222222";
    private const string DefaultLabelFontFamily = "sans-serif";
    private const string ArrowMarkerId = "arrowhead";

    private static readonly string[] GroupPalette =
    [
        "#4682b4",
        "#ffa500",
        "#008000",
        "#ff0000",
        "#ee82ee",
        "#8b4513",
        "#ffc0cb",
        "#808080",
        "#808000",
        "#00ffff"
    ];

    public static SfdpRenderSceneData Build(
        GraphView graph,
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        SfdpBoundingBox contentBounds,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labelPlacements,
        IReadOnlyList<SfdpEdgeRouter.RoutedEdge> routedEdges,
        SfdpViewportMetrics viewportMetrics,
        SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(contentBounds);
        ArgumentNullException.ThrowIfNull(labelPlacements);
        ArgumentNullException.ThrowIfNull(routedEdges);
        ArgumentNullException.ThrowIfNull(viewportMetrics);
        ArgumentNullException.ThrowIfNull(options);

        var graphTranslateX = contentBounds.MinX;
        var graphTranslateY = contentBounds.MaxY;
        var canvas = new GraphRenderCanvas(
            GraphId: "graph0",
            GraphClass: "graph",
            GraphTitle: "G",
            Transform: BuildGraphTransform(graphTranslateX, graphTranslateY),
            BackgroundPolygonPoints: BuildBackgroundPolygonPoints(contentBounds));

        var style = new GraphRenderStyle(
            ShowBackgroundRect: true,
            BackgroundColor: options.BackgroundColor,
            ArrowStyle: options.ShowArrows
                ? new GraphRenderArrowStyle(ArrowMarkerId, options.EdgeColor, options.ArrowSize)
                : null);

        var edges = new GraphRenderEdge[routedEdges.Count];
        for (var edgeIndex = 0; edgeIndex < routedEdges.Count; edgeIndex++)
        {
            var edge = routedEdges[edgeIndex];
            var sourceNode = graph.Nodes[edge.SourceIndex];
            var targetNode = graph.Nodes[edge.TargetIndex];
            edges[edgeIndex] = new GraphRenderEdge(
                Id: $"edge{edgeIndex + 1}",
                Title: $"{sourceNode.Id}->{targetNode.Id}",
                PathData: BuildGraphvizPathData(edge.Points, viewportMetrics.PaddingX, graphTranslateY),
                Stroke: options.EdgeColor,
                StrokeWidth: options.EdgeLineWidth,
                StrokeLineCap: "round",
                MarkerEnd: options.ShowArrows ? $"url(#{ArrowMarkerId})" : null);
        }

        var nodes = new GraphRenderNode[graph.Nodes.Count];
        for (var index = 0; index < graph.Nodes.Count; index++)
        {
            var node = graph.Nodes[index];
            var label = options.ShowLabels
                ? new GraphRenderLabel(
                    Text: node.Label,
                    X: labelPlacements[index].X - graphTranslateX,
                    BaselineY: (labelPlacements[index].Y + (options.LabelFontSize * 0.8)) - graphTranslateY,
                    FontSize: options.LabelFontSize,
                    FontFamily: DefaultLabelFontFamily,
                    Fill: DefaultLabelFill)
                : null;

            nodes[index] = new GraphRenderNode(
                Id: $"node{index + 1}",
                DataNodeId: node.Id,
                Title: node.Id,
                X: x[index] - graphTranslateX,
                Y: y[index] - graphTranslateY,
                RadiusX: options.NodeRadius,
                RadiusY: options.NodeRadius,
                Fill: ResolveFill(node, options),
                Stroke: DefaultNodeStroke,
                StrokeWidth: 1.0,
                Label: label);
        }

        return new SfdpRenderSceneData(
            new GraphRenderScene(viewportMetrics.Viewport, style, canvas, edges, nodes),
            contentBounds,
            viewportMetrics);
    }

    private static string BuildGraphTransform(double translateX, double translateY)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"scale(1 1) rotate(0) translate({Format(translateX)} {Format(translateY)})");
    }

    private static string BuildBackgroundPolygonPoints(SfdpBoundingBox contentBounds)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Format(-contentBounds.MinX)},{Format(contentBounds.MinY)} {Format(-contentBounds.MinX)},{Format(-contentBounds.MaxY)} {Format(contentBounds.MaxX - contentBounds.MinX)},{Format(-contentBounds.MaxY)} {Format(contentBounds.MaxX - contentBounds.MinX)},{Format(contentBounds.MinY)}");
    }

    private static string BuildGraphvizPathData(
        IReadOnlyList<SfdpEdgeRouter.RoutedPoint> points,
        double translateX,
        double translateY)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{Format(points[0].X - translateX)},{Format(points[0].Y - translateY)}C{Format(points[1].X - translateX)},{Format(points[1].Y - translateY)} {Format(points[2].X - translateX)},{Format(points[2].Y - translateY)} {Format(points[3].X - translateX)},{Format(points[3].Y - translateY)}");
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string ResolveFill(GraphViewNode node, SfdpOptions options)
    {
        if (options.DisableGroupColors || string.IsNullOrWhiteSpace(options.GroupMetadataKey))
        {
            return "#696969";
        }

        if (node.Metadata.TryGetValue(options.GroupMetadataKey, out var value) && TryGetGroup(value, out var group))
        {
            return GroupPalette[Math.Abs(group) % GroupPalette.Length];
        }

        return "#696969";
    }

    private static bool TryGetGroup(object? value, out int group)
    {
        group = 0;
        return value switch
        {
            int i => (group = i) == i,
            long l when l >= int.MinValue && l <= int.MaxValue => (group = (int)l) == l,
            double d => (group = (int)d) == (int)d,
            float f => (group = (int)f) == (int)f,
            _ => value is not null && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out group)
        };
    }
}

internal sealed record SfdpRenderSceneData(
    GraphRenderScene Scene,
    SfdpBoundingBox ContentBounds,
    SfdpViewportMetrics ViewportMetrics);
