using System.Globalization;
using System.Xml.Linq;

namespace PSGraphView.Graphviz;

public sealed class GraphSceneSvgRenderer
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    public string Render(GraphScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var bounds = scene.Bounds ?? new SceneRect(0, 0, 0, 0);
        var renderState = new SvgRenderState(bounds);
        var root = new XElement(
            SvgNamespace + "svg",
            new XAttribute("xmlns", SvgNamespace.NamespaceName),
            new XAttribute("version", "1.1"),
            new XAttribute("width", FormatDouble(bounds.Width)),
            new XAttribute("height", FormatDouble(bounds.Height)),
            new XAttribute("viewBox", $"0 0 {FormatDouble(bounds.Width)} {FormatDouble(bounds.Height)}"));

        if (renderState.Definitions.HasElements)
        {
            root.Add(renderState.Definitions);
        }

        var graphGroup = CreateGroup("graph", scene.Name);
        AppendCommands(graphGroup, scene.Commands, renderState);

        foreach (var sceneObject in scene.Objects)
        {
            var objectGroup = CreateGroup(sceneObject.Kind.ToString().ToLowerInvariant(), sceneObject.Name);
            AppendCommands(objectGroup, sceneObject.Commands, renderState);
            graphGroup.Add(objectGroup);
        }

        if (renderState.Definitions.HasElements)
        {
            root.AddFirst(renderState.Definitions);
        }

        root.Add(graphGroup);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement CreateGroup(string kind, string? name)
    {
        var group = new XElement(
            SvgNamespace + "g",
            new XAttribute("data-kind", kind));

        if (!string.IsNullOrWhiteSpace(name))
        {
            group.SetAttributeValue("data-name", name);
        }

        return group;
    }

    private static void AppendCommands(XElement parent, IReadOnlyList<SceneCommand> commands, SvgRenderState state)
    {
        foreach (var command in commands)
        {
            parent.Add(RenderCommand(command, state));
        }
    }

    private static XElement RenderCommand(SceneCommand command, SvgRenderState state)
    {
        return command switch
        {
            EllipseCommand ellipse => RenderEllipse(ellipse, state),
            PolygonCommand polygon => RenderPolygon(polygon, state),
            BezierCommand bezier => RenderBezier(bezier, state),
            PolylineCommand polyline => RenderPolyline(polyline, state),
            TextCommand text => RenderText(text, state),
            _ => throw new NotSupportedException($"The scene command '{command.GetType().Name}' is not supported.")
        };
    }

    private static XElement RenderEllipse(EllipseCommand command, SvgRenderState state)
    {
        var element = new XElement(
            SvgNamespace + "ellipse",
            new XAttribute("cx", FormatDouble(ToSvgX(state.Bounds, command.Center.X))),
            new XAttribute("cy", FormatDouble(ToSvgY(state.Bounds, command.Center.Y))),
            new XAttribute("rx", FormatDouble(command.RadiusX)),
            new XAttribute("ry", FormatDouble(command.RadiusY)));

        ApplyShapePaint(element, command.Stroke, command.Fill, command.StrokeStyle, state);
        return element;
    }

    private static XElement RenderPolygon(PolygonCommand command, SvgRenderState state)
    {
        var element = new XElement(
            SvgNamespace + "polygon",
            new XAttribute("points", FormatPointList(command.Points, state.Bounds)));

        ApplyShapePaint(element, command.Stroke, command.Fill, command.StrokeStyle, state);
        return element;
    }

    private static XElement RenderBezier(BezierCommand command, SvgRenderState state)
    {
        if (command.Points.Count < 4 || (command.Points.Count - 1) % 3 != 0)
        {
            throw new InvalidDataException("Bezier commands must contain 4 + 3n points.");
        }

        var pathData = new List<string>
        {
            "M",
            FormatDouble(ToSvgX(state.Bounds, command.Points[0].X)),
            FormatDouble(ToSvgY(state.Bounds, command.Points[0].Y))
        };

        for (var index = 1; index < command.Points.Count; index += 3)
        {
            pathData.Add("C");
            pathData.Add(FormatDouble(ToSvgX(state.Bounds, command.Points[index].X)));
            pathData.Add(FormatDouble(ToSvgY(state.Bounds, command.Points[index].Y)));
            pathData.Add(FormatDouble(ToSvgX(state.Bounds, command.Points[index + 1].X)));
            pathData.Add(FormatDouble(ToSvgY(state.Bounds, command.Points[index + 1].Y)));
            pathData.Add(FormatDouble(ToSvgX(state.Bounds, command.Points[index + 2].X)));
            pathData.Add(FormatDouble(ToSvgY(state.Bounds, command.Points[index + 2].Y)));
        }

        if (command.Fill is not null)
        {
            pathData.Add("Z");
        }

        var element = new XElement(
            SvgNamespace + "path",
            new XAttribute("d", string.Join(' ', pathData)));

        ApplyShapePaint(element, command.Stroke, command.Fill, command.StrokeStyle, state);
        return element;
    }

    private static XElement RenderPolyline(PolylineCommand command, SvgRenderState state)
    {
        var element = new XElement(
            SvgNamespace + "polyline",
            new XAttribute("points", FormatPointList(command.Points, state.Bounds)));

        ApplyLinePaint(element, command.Stroke, command.StrokeStyle, state);
        return element;
    }

    private static XElement RenderText(TextCommand command, SvgRenderState state)
    {
        var element = new XElement(
            SvgNamespace + "text",
            new XAttribute("x", FormatDouble(ToSvgX(state.Bounds, command.Anchor.X))),
            new XAttribute("y", FormatDouble(ToSvgY(state.Bounds, command.Anchor.Y))),
            new XAttribute("font-family", command.Font.Family),
            new XAttribute("font-size", FormatDouble(command.Font.Size)),
            new XAttribute("text-anchor", command.Alignment switch
            {
                SceneTextAlignment.Left => "start",
                SceneTextAlignment.Center => "middle",
                SceneTextAlignment.Right => "end",
                _ => throw new NotSupportedException($"The text alignment '{command.Alignment}' is not supported.")
            }),
            new XText(command.Text));

        ApplyColorPaint(element, "fill", "fill-opacity", command.Color);
        ApplyFontStyle(element, command.Font.Style);
        return element;
    }

    private static void ApplyShapePaint(
        XElement element,
        ScenePaint? stroke,
        ScenePaint? fill,
        SceneStrokeStyle? strokeStyle,
        SvgRenderState state)
    {
        ApplyPaint(element, "stroke", "stroke-opacity", stroke, state, "none");
        ApplyPaint(element, "fill", "fill-opacity", fill, state, "none");

        if (strokeStyle is not null)
        {
            element.SetAttributeValue("stroke-width", FormatDouble(strokeStyle.Width));
            if (strokeStyle.Pattern != SceneLinePattern.Solid)
            {
                element.SetAttributeValue(
                    "stroke-dasharray",
                    strokeStyle.Pattern switch
                    {
                        SceneLinePattern.Dashed => "5 3",
                        SceneLinePattern.Dotted => "1 3",
                        _ => throw new NotSupportedException($"The line pattern '{strokeStyle.Pattern}' is not supported.")
                    });
            }
        }
    }

    private static void ApplyLinePaint(
        XElement element,
        ScenePaint? stroke,
        SceneStrokeStyle? strokeStyle,
        SvgRenderState state)
    {
        ApplyPaint(element, "stroke", "stroke-opacity", stroke, state, "none");
        element.SetAttributeValue("fill", "none");

        if (strokeStyle is not null)
        {
            element.SetAttributeValue("stroke-width", FormatDouble(strokeStyle.Width));
            if (strokeStyle.Pattern != SceneLinePattern.Solid)
            {
                element.SetAttributeValue(
                    "stroke-dasharray",
                    strokeStyle.Pattern switch
                    {
                        SceneLinePattern.Dashed => "5 3",
                        SceneLinePattern.Dotted => "1 3",
                        _ => throw new NotSupportedException($"The line pattern '{strokeStyle.Pattern}' is not supported.")
                    });
            }
        }
    }

    private static void ApplyPaint(
        XElement element,
        string attributeName,
        string opacityAttributeName,
        ScenePaint? paint,
        SvgRenderState state,
        string defaultValue)
    {
        if (paint is null)
        {
            element.SetAttributeValue(attributeName, defaultValue);
            return;
        }

        switch (paint)
        {
            case SceneColor color:
                ApplyColorPaint(element, attributeName, opacityAttributeName, color);
                return;
            case SceneGradient gradient:
                element.SetAttributeValue(attributeName, $"url(#{state.RegisterGradient(gradient)})");
                return;
            default:
                throw new NotSupportedException($"The paint '{paint.GetType().Name}' is not supported.");
        }
    }

    private static void ApplyColorPaint(
        XElement element,
        string attributeName,
        string opacityAttributeName,
        SceneColor color)
    {
        element.SetAttributeValue(attributeName, FormatColor(color));

        if (color.Alpha != byte.MaxValue)
        {
            element.SetAttributeValue(opacityAttributeName, FormatDouble(color.Alpha / 255.0));
        }
    }

    private static void ApplyFontStyle(XElement element, SceneFontStyle style)
    {
        if (style.HasFlag(SceneFontStyle.Bold))
        {
            element.SetAttributeValue("font-weight", "bold");
        }

        if (style.HasFlag(SceneFontStyle.Italic))
        {
            element.SetAttributeValue("font-style", "italic");
        }

        var decorations = new List<string>();
        if (style.HasFlag(SceneFontStyle.Underline))
        {
            decorations.Add("underline");
        }

        if (style.HasFlag(SceneFontStyle.Strikethrough))
        {
            decorations.Add("line-through");
        }

        if (decorations.Count > 0)
        {
            element.SetAttributeValue("text-decoration", string.Join(' ', decorations));
        }

        if (style.HasFlag(SceneFontStyle.Superscript) && !style.HasFlag(SceneFontStyle.Subscript))
        {
            element.SetAttributeValue("baseline-shift", "super");
        }
        else if (style.HasFlag(SceneFontStyle.Subscript) && !style.HasFlag(SceneFontStyle.Superscript))
        {
            element.SetAttributeValue("baseline-shift", "sub");
        }
    }

    private static string FormatPointList(IReadOnlyList<ScenePoint> points, SceneRect bounds)
    {
        return string.Join(
            ' ',
            points.Select(point => $"{FormatDouble(ToSvgX(bounds, point.X))},{FormatDouble(ToSvgY(bounds, point.Y))}"));
    }

    private static string FormatColor(SceneColor color)
    {
        return $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}";
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static double ToSvgX(SceneRect bounds, double x)
    {
        return x - bounds.X;
    }

    private static double ToSvgY(SceneRect bounds, double y)
    {
        return bounds.Height - (y - bounds.Y);
    }

    private sealed class SvgRenderState
    {
        private int _gradientId;

        public SvgRenderState(SceneRect bounds)
        {
            Bounds = bounds;
            Definitions = new XElement(SvgNamespace + "defs");
        }

        public SceneRect Bounds { get; }

        public XElement Definitions { get; }

        public string RegisterGradient(SceneGradient gradient)
        {
            var id = $"gradient-{++_gradientId}";
            Definitions.Add(CreateGradientElement(id, gradient, Bounds));
            return id;
        }
    }

    private static XElement CreateGradientElement(string id, SceneGradient gradient, SceneRect bounds)
    {
        XElement element = gradient.Kind switch
        {
            SceneGradientKind.Linear => new XElement(
                SvgNamespace + "linearGradient",
                new XAttribute("id", id),
                new XAttribute("gradientUnits", "userSpaceOnUse"),
                new XAttribute("x1", FormatDouble(ToSvgX(bounds, gradient.Start.X))),
                new XAttribute("y1", FormatDouble(ToSvgY(bounds, gradient.Start.Y))),
                new XAttribute("x2", FormatDouble(ToSvgX(bounds, gradient.End.X))),
                new XAttribute("y2", FormatDouble(ToSvgY(bounds, gradient.End.Y)))),
            SceneGradientKind.Radial => new XElement(
                SvgNamespace + "radialGradient",
                new XAttribute("id", id),
                new XAttribute("gradientUnits", "userSpaceOnUse"),
                new XAttribute("fx", FormatDouble(ToSvgX(bounds, gradient.Start.X))),
                new XAttribute("fy", FormatDouble(ToSvgY(bounds, gradient.Start.Y))),
                new XAttribute("cx", FormatDouble(ToSvgX(bounds, gradient.End.X))),
                new XAttribute("cy", FormatDouble(ToSvgY(bounds, gradient.End.Y))),
                new XAttribute("r", FormatDouble(gradient.EndRadius ?? 0))),
            _ => throw new NotSupportedException($"The gradient kind '{gradient.Kind}' is not supported.")
        };

        if (gradient.Kind == SceneGradientKind.Radial && gradient.StartRadius is not null)
        {
            element.SetAttributeValue("fr", FormatDouble(gradient.StartRadius.Value));
        }

        foreach (var stop in gradient.Stops)
        {
            var stopElement = new XElement(
                SvgNamespace + "stop",
                new XAttribute("offset", $"{FormatDouble(stop.Offset * 100)}%"),
                new XAttribute("stop-color", FormatColor(stop.Color)));

            if (stop.Color.Alpha != byte.MaxValue)
            {
                stopElement.SetAttributeValue("stop-opacity", FormatDouble(stop.Color.Alpha / 255.0));
            }

            element.Add(stopElement);
        }

        return element;
    }
}
