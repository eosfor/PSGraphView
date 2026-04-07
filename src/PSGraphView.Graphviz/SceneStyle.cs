using System.Text.Json.Serialization;

namespace PSGraphView.Graphviz;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SceneColor), "color")]
[JsonDerivedType(typeof(SceneGradient), "gradient")]
public abstract record ScenePaint;

public sealed record SceneColor(byte Red, byte Green, byte Blue, byte Alpha = 255) : ScenePaint;

public sealed record SceneGradient : ScenePaint
{
    public SceneGradient(
        SceneGradientKind kind,
        ScenePoint start,
        ScenePoint end,
        IReadOnlyList<SceneGradientStop>? stops = null,
        double? startRadius = null,
        double? endRadius = null)
    {
        Kind = kind;
        Start = start;
        End = end;
        Stops = stops ?? Array.Empty<SceneGradientStop>();
        StartRadius = startRadius;
        EndRadius = endRadius;
    }

    public SceneGradientKind Kind { get; }

    public ScenePoint Start { get; }

    public ScenePoint End { get; }

    public IReadOnlyList<SceneGradientStop> Stops { get; }

    public double? StartRadius { get; }

    public double? EndRadius { get; }
}

public readonly record struct SceneGradientStop(double Offset, SceneColor Color);

public enum SceneGradientKind
{
    Linear = 0,
    Radial = 1
}

public sealed record SceneFont
{
    public SceneFont(string family, double size)
    {
        Family = string.IsNullOrWhiteSpace(family)
            ? throw new ArgumentException("Font family is required.", nameof(family))
            : family;
        Size = size > 0
            ? size
            : throw new ArgumentOutOfRangeException(nameof(size), "Font size must be positive.");
    }

    public string Family { get; }

    public double Size { get; }
}

public sealed record SceneStrokeStyle
{
    public SceneStrokeStyle(double width = 1.0, SceneLinePattern pattern = SceneLinePattern.Solid)
    {
        Width = width > 0
            ? width
            : throw new ArgumentOutOfRangeException(nameof(width), "Stroke width must be positive.");
        Pattern = pattern;
    }

    public double Width { get; }

    public SceneLinePattern Pattern { get; }
}

public enum SceneLinePattern
{
    Solid = 0,
    Dashed = 1,
    Dotted = 2
}

public enum SceneTextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2
}
