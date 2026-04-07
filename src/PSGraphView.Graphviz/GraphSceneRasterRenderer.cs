using SkiaSharp;

namespace PSGraphView.Graphviz;

public sealed class GraphSceneRasterRenderer
{
    public byte[] Render(GraphScene scene, RasterOutputKind outputKind)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SkiaSharpNativeLoader.EnsureLoaded();

        var bounds = scene.Bounds ?? new SceneRect(0, 0, 0, 0);
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Failed to create a raster drawing surface.");

        var canvas = surface.Canvas;
        canvas.Clear(outputKind == RasterOutputKind.Jpeg ? SKColors.White : SKColors.Transparent);

        foreach (var command in scene.Commands)
        {
            RenderCommand(canvas, command, bounds);
        }

        foreach (var sceneObject in scene.Objects)
        {
            foreach (var command in sceneObject.Commands)
            {
                RenderCommand(canvas, command, bounds);
            }
        }

        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(
            outputKind switch
            {
                RasterOutputKind.Png => SKEncodedImageFormat.Png,
                RasterOutputKind.Jpeg => SKEncodedImageFormat.Jpeg,
                _ => throw new NotSupportedException($"The raster output kind '{outputKind}' is not supported.")
            },
            outputKind == RasterOutputKind.Jpeg ? 90 : 100);

        return data.ToArray();
    }

    private static void RenderCommand(SKCanvas canvas, SceneCommand command, SceneRect bounds)
    {
        switch (command)
        {
            case EllipseCommand ellipse:
                RenderEllipse(canvas, ellipse, bounds);
                return;
            case PolygonCommand polygon:
                RenderPolygon(canvas, polygon, bounds);
                return;
            case BezierCommand bezier:
                RenderBezier(canvas, bezier, bounds);
                return;
            case PolylineCommand polyline:
                RenderPolyline(canvas, polyline, bounds);
                return;
            case TextCommand text:
                RenderText(canvas, text, bounds);
                return;
            default:
                throw new NotSupportedException($"The scene command '{command.GetType().Name}' is not supported.");
        }
    }

    private static void RenderEllipse(SKCanvas canvas, EllipseCommand command, SceneRect bounds)
    {
        var center = ToRasterPoint(bounds, command.Center);
        using var fillPaint = CreateFillPaint(command.Fill, bounds);
        using var strokePaint = CreateStrokePaint(command.Stroke, command.StrokeStyle, bounds);

        if (fillPaint is not null)
        {
            canvas.DrawOval(center.X, center.Y, (float)command.RadiusX, (float)command.RadiusY, fillPaint);
        }

        if (strokePaint is not null)
        {
            canvas.DrawOval(center.X, center.Y, (float)command.RadiusX, (float)command.RadiusY, strokePaint);
        }
    }

    private static void RenderPolygon(SKCanvas canvas, PolygonCommand command, SceneRect bounds)
    {
        if (command.Points.Count == 0)
        {
            return;
        }

        using var path = CreatePath(command.Points, bounds, close: true, curve: false);
        using var fillPaint = CreateFillPaint(command.Fill, bounds);
        using var strokePaint = CreateStrokePaint(command.Stroke, command.StrokeStyle, bounds);

        if (fillPaint is not null)
        {
            canvas.DrawPath(path, fillPaint);
        }

        if (strokePaint is not null)
        {
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void RenderBezier(SKCanvas canvas, BezierCommand command, SceneRect bounds)
    {
        if (command.Points.Count < 4 || (command.Points.Count - 1) % 3 != 0)
        {
            throw new InvalidDataException("Bezier commands must contain 4 + 3n points.");
        }

        using var path = CreatePath(command.Points, bounds, close: command.Fill is not null, curve: true);
        using var fillPaint = CreateFillPaint(command.Fill, bounds);
        using var strokePaint = CreateStrokePaint(command.Stroke, command.StrokeStyle, bounds);

        if (fillPaint is not null)
        {
            canvas.DrawPath(path, fillPaint);
        }

        if (strokePaint is not null)
        {
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void RenderPolyline(SKCanvas canvas, PolylineCommand command, SceneRect bounds)
    {
        if (command.Points.Count == 0)
        {
            return;
        }

        using var path = CreatePath(command.Points, bounds, close: false, curve: false);
        using var strokePaint = CreateStrokePaint(command.Stroke, command.StrokeStyle, bounds);
        if (strokePaint is not null)
        {
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void RenderText(SKCanvas canvas, TextCommand command, SceneRect bounds)
    {
        var anchor = ToRasterPoint(bounds, command.Anchor);
        using var paint = CreateTextPaint(command);

        var measuredWidth = paint.MeasureText(command.Text);
        var x = command.Alignment switch
        {
            SceneTextAlignment.Left => anchor.X,
            SceneTextAlignment.Center => anchor.X - measuredWidth / 2f,
            SceneTextAlignment.Right => anchor.X - measuredWidth,
            _ => throw new NotSupportedException($"The text alignment '{command.Alignment}' is not supported.")
        };

        var baselineShift = 0f;
        if (command.Font.Style.HasFlag(SceneFontStyle.Superscript) &&
            !command.Font.Style.HasFlag(SceneFontStyle.Subscript))
        {
            baselineShift = -(float)(command.Font.Size * 0.35);
        }
        else if (command.Font.Style.HasFlag(SceneFontStyle.Subscript) &&
                 !command.Font.Style.HasFlag(SceneFontStyle.Superscript))
        {
            baselineShift = (float)(command.Font.Size * 0.2);
        }

        var baselineY = anchor.Y + baselineShift;
        canvas.DrawText(command.Text, x, baselineY, paint);

        var decorationStrokeWidth = Math.Max(1f, paint.TextSize / 14f);
        using var decorationPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = decorationStrokeWidth,
            Color = paint.Color
        };

        if (command.Font.Style.HasFlag(SceneFontStyle.Underline))
        {
            var underlineY = baselineY + decorationStrokeWidth;
            canvas.DrawLine(x, underlineY, x + measuredWidth, underlineY, decorationPaint);
        }

        if (command.Font.Style.HasFlag(SceneFontStyle.Strikethrough))
        {
            var strikeY = baselineY - paint.TextSize * 0.3f;
            canvas.DrawLine(x, strikeY, x + measuredWidth, strikeY, decorationPaint);
        }
    }

    private static SKPath CreatePath(IReadOnlyList<ScenePoint> points, SceneRect bounds, bool close, bool curve)
    {
        var path = new SKPath();
        var start = ToRasterPoint(bounds, points[0]);
        path.MoveTo(start);

        if (curve)
        {
            for (var index = 1; index < points.Count; index += 3)
            {
                var controlPoint1 = ToRasterPoint(bounds, points[index]);
                var controlPoint2 = ToRasterPoint(bounds, points[index + 1]);
                var endPoint = ToRasterPoint(bounds, points[index + 2]);
                path.CubicTo(controlPoint1, controlPoint2, endPoint);
            }
        }
        else
        {
            for (var index = 1; index < points.Count; index++)
            {
                path.LineTo(ToRasterPoint(bounds, points[index]));
            }
        }

        if (close)
        {
            path.Close();
        }

        return path;
    }

    private static SKPaint? CreateFillPaint(ScenePaint? paint, SceneRect bounds)
    {
        if (paint is null)
        {
            return null;
        }

        var skPaint = CreatePaint(paint, bounds);
        skPaint.Style = SKPaintStyle.Fill;
        return skPaint;
    }

    private static SKPaint? CreateStrokePaint(ScenePaint? paint, SceneStrokeStyle? strokeStyle, SceneRect bounds)
    {
        if (paint is null)
        {
            return null;
        }

        var skPaint = CreatePaint(paint, bounds);
        skPaint.Style = SKPaintStyle.Stroke;
        skPaint.StrokeWidth = (float)(strokeStyle?.Width ?? 1.0);

        switch (strokeStyle?.Pattern ?? SceneLinePattern.Solid)
        {
            case SceneLinePattern.Solid:
                return skPaint;
            case SceneLinePattern.Dashed:
                skPaint.PathEffect = SKPathEffect.CreateDash([5f, 3f], 0);
                return skPaint;
            case SceneLinePattern.Dotted:
                skPaint.PathEffect = SKPathEffect.CreateDash([1f, 3f], 0);
                return skPaint;
            default:
                skPaint.Dispose();
                throw new NotSupportedException(
                    $"The line pattern '{strokeStyle?.Pattern}' is not supported.");
        }
    }

    private static SKPaint CreatePaint(ScenePaint paint, SceneRect bounds)
    {
        var skPaint = new SKPaint
        {
            IsAntialias = true
        };

        switch (paint)
        {
            case SceneColor color:
                skPaint.Color = ToSkColor(color);
                return skPaint;
            case SceneGradient gradient:
                skPaint.Shader = CreateShader(gradient, bounds);
                return skPaint;
            default:
                skPaint.Dispose();
                throw new NotSupportedException($"The paint '{paint.GetType().Name}' is not supported.");
        }
    }

    private static SKShader CreateShader(SceneGradient gradient, SceneRect bounds)
    {
        var colors = gradient.Stops.Select(stop => ToSkColor(stop.Color)).ToArray();
        var positions = gradient.Stops.Select(stop => (float)stop.Offset).ToArray();

        if (colors.Length == 0)
        {
            colors = [SKColors.Transparent, SKColors.Transparent];
            positions = [0f, 1f];
        }
        else if (colors.Length == 1)
        {
            colors = [colors[0], colors[0]];
            positions = [0f, 1f];
        }

        return gradient.Kind switch
        {
            SceneGradientKind.Linear => SKShader.CreateLinearGradient(
                ToRasterPoint(bounds, gradient.Start),
                ToRasterPoint(bounds, gradient.End),
                colors,
                positions,
                SKShaderTileMode.Clamp),
            SceneGradientKind.Radial => SKShader.CreateRadialGradient(
                ToRasterPoint(bounds, gradient.End),
                ResolveRadialRadius(gradient),
                colors,
                positions,
                SKShaderTileMode.Clamp),
            _ => throw new NotSupportedException($"The gradient kind '{gradient.Kind}' is not supported.")
        };
    }

    private static float ResolveRadialRadius(SceneGradient gradient)
    {
        if (gradient.EndRadius is > 0)
        {
            return (float)gradient.EndRadius.Value;
        }

        if (gradient.StartRadius is > 0)
        {
            return (float)gradient.StartRadius.Value;
        }

        var dx = gradient.End.X - gradient.Start.X;
        var dy = gradient.End.Y - gradient.Start.Y;
        return Math.Max(1f, (float)Math.Sqrt((dx * dx) + (dy * dy)));
    }

    private static SKPaint CreateTextPaint(TextCommand command)
    {
        var fontStyle = command.Font.Style;
        var typeface = SKTypeface.FromFamilyName(
            command.Font.Family,
            fontStyle.HasFlag(SceneFontStyle.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            fontStyle.HasFlag(SceneFontStyle.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        return new SKPaint
        {
            IsAntialias = true,
            Color = ToSkColor(command.Color),
            Typeface = typeface,
            TextSize = (float)command.Font.Size,
            Style = SKPaintStyle.Fill
        };
    }

    private static SKColor ToSkColor(SceneColor color)
    {
        return new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
    }

    private static SKPoint ToRasterPoint(SceneRect bounds, ScenePoint point)
    {
        return new SKPoint(
            (float)(point.X - bounds.X),
            (float)(bounds.Height - (point.Y - bounds.Y)));
    }
}

public enum RasterOutputKind
{
    Png = 0,
    Jpeg = 1
}
