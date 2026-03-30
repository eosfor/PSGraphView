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
        SfdpLayoutResult layout,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labelPlacements,
        IReadOnlyList<SfdpEdgeRouter.RoutedEdge> routedEdges,
        SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(labelPlacements);
        ArgumentNullException.ThrowIfNull(routedEdges);
        ArgumentNullException.ThrowIfNull(options);

        var padding = Math.Max(options.NodeRadius * 2.0, 12.0);
        var contentBounds = ExpandBoundsForLabels(layout.Bounds, labelPlacements);
        var viewBoxMinX = contentBounds.MinX - padding;
        var viewBoxMinY = contentBounds.MinY - padding;
        var viewBoxWidth = Math.Max(1.0, contentBounds.Width + padding * 2.0);
        var viewBoxHeight = Math.Max(1.0, contentBounds.Height + padding * 2.0);
        var outputWidth = options.Width ?? Math.Ceiling(viewBoxWidth);
        var outputHeight = options.Height ?? Math.Ceiling(viewBoxHeight);

        var viewport = new GraphRenderViewport(
            viewBoxMinX,
            viewBoxMinY,
            viewBoxWidth,
            viewBoxHeight,
            outputWidth,
            outputHeight);

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
                PathData: edge.PathData,
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
                    X: labelPlacements[index].X,
                    BaselineY: labelPlacements[index].Y + (options.LabelFontSize * 0.8),
                    FontSize: options.LabelFontSize,
                    FontFamily: DefaultLabelFontFamily,
                    Fill: DefaultLabelFill)
                : null;

            nodes[index] = new GraphRenderNode(
                Id: $"node{index + 1}",
                DataNodeId: node.Id,
                Title: node.Id,
                X: layout.X[index],
                Y: layout.Y[index],
                Radius: options.NodeRadius,
                Fill: ResolveFill(node, options),
                Stroke: DefaultNodeStroke,
                StrokeWidth: 1.0,
                Label: label);
        }

        return new SfdpRenderSceneData(
            new GraphRenderScene(viewport, style, edges, nodes),
            contentBounds,
            padding);
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
    double Padding);
