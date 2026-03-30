namespace PSGraphView.GVExport;

public sealed record GraphRenderViewport(
    double ViewBoxMinX,
    double ViewBoxMinY,
    double ViewBoxWidth,
    double ViewBoxHeight,
    double OutputWidth,
    double OutputHeight);

public sealed record GraphRenderArrowStyle(
    string MarkerId,
    string Color,
    double ArrowSize);

public sealed record GraphRenderStyle(
    bool ShowBackgroundRect,
    string BackgroundColor,
    GraphRenderArrowStyle? ArrowStyle);

public sealed record GraphRenderLabel(
    string Text,
    double X,
    double BaselineY,
    double FontSize,
    string FontFamily,
    string Fill);

public sealed record GraphRenderNode(
    string Id,
    string DataNodeId,
    string Title,
    double X,
    double Y,
    double Radius,
    string Fill,
    string Stroke,
    double StrokeWidth,
    GraphRenderLabel? Label);

public sealed record GraphRenderEdge(
    string Id,
    string Title,
    string PathData,
    string Stroke,
    double StrokeWidth,
    string StrokeLineCap,
    string? MarkerEnd);

public sealed record GraphRenderScene(
    GraphRenderViewport Viewport,
    GraphRenderStyle Style,
    IReadOnlyList<GraphRenderEdge> Edges,
    IReadOnlyList<GraphRenderNode> Nodes);
