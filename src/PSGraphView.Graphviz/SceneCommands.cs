using System.Text.Json.Serialization;

namespace PSGraphView.Graphviz;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(EllipseCommand), "ellipse")]
[JsonDerivedType(typeof(PolygonCommand), "polygon")]
[JsonDerivedType(typeof(BezierCommand), "bezier")]
[JsonDerivedType(typeof(PolylineCommand), "polyline")]
[JsonDerivedType(typeof(TextCommand), "text")]
public abstract record SceneCommand;

public sealed record EllipseCommand(
    ScenePoint Center,
    double RadiusX,
    double RadiusY,
    ScenePaint? Stroke = null,
    ScenePaint? Fill = null,
    SceneStrokeStyle? StrokeStyle = null) : SceneCommand;

public sealed record PolygonCommand
    : SceneCommand
{
    public PolygonCommand(
        IReadOnlyList<ScenePoint> points,
        ScenePaint? stroke = null,
        ScenePaint? fill = null,
        SceneStrokeStyle? strokeStyle = null)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
        Stroke = stroke;
        Fill = fill;
        StrokeStyle = strokeStyle;
    }

    public IReadOnlyList<ScenePoint> Points { get; }

    public ScenePaint? Stroke { get; }

    public ScenePaint? Fill { get; }

    public SceneStrokeStyle? StrokeStyle { get; }
}

public sealed record BezierCommand
    : SceneCommand
{
    public BezierCommand(
        IReadOnlyList<ScenePoint> points,
        ScenePaint? stroke = null,
        ScenePaint? fill = null,
        SceneStrokeStyle? strokeStyle = null)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
        Stroke = stroke;
        Fill = fill;
        StrokeStyle = strokeStyle;
    }

    public IReadOnlyList<ScenePoint> Points { get; }

    public ScenePaint? Stroke { get; }

    public ScenePaint? Fill { get; }

    public SceneStrokeStyle? StrokeStyle { get; }
}

public sealed record PolylineCommand
    : SceneCommand
{
    public PolylineCommand(
        IReadOnlyList<ScenePoint> points,
        ScenePaint? stroke = null,
        SceneStrokeStyle? strokeStyle = null)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
        Stroke = stroke;
        StrokeStyle = strokeStyle;
    }

    public IReadOnlyList<ScenePoint> Points { get; }

    public ScenePaint? Stroke { get; }

    public SceneStrokeStyle? StrokeStyle { get; }
}

public sealed record TextCommand : SceneCommand
{
    public TextCommand(
        ScenePoint anchor,
        string text,
        SceneFont font,
        SceneColor color,
        double width,
        SceneTextAlignment alignment = SceneTextAlignment.Center)
    {
        Anchor = anchor;
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Font = font ?? throw new ArgumentNullException(nameof(font));
        Color = color ?? throw new ArgumentNullException(nameof(color));
        Width = width >= 0
            ? width
            : throw new ArgumentOutOfRangeException(nameof(width), "Text width cannot be negative.");
        Alignment = alignment;
    }

    public ScenePoint Anchor { get; }

    public string Text { get; }

    public SceneFont Font { get; }

    public SceneColor Color { get; }

    public double Width { get; }

    public SceneTextAlignment Alignment { get; }
}
