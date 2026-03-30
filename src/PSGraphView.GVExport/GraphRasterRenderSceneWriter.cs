using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace PSGraphView.GVExport;

public static class GraphRasterRenderSceneWriter
{
    private const double SvgDpi = 72.0;
    private const double RasterDpi = 96.0;
    private const int DefaultJpegQuality = 90;

    public static GraphRasterRenderResult RenderPng(GraphRenderScene scene)
        => Render(scene, GraphRasterImageFormat.Png, DefaultJpegQuality);

    public static GraphRasterRenderResult RenderJpg(GraphRenderScene scene)
        => Render(scene, GraphRasterImageFormat.Jpg, DefaultJpegQuality);

    private static GraphRasterRenderResult Render(
        GraphRenderScene scene,
        GraphRasterImageFormat format,
        int quality)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var rasterWidthPoints = Math.Max(scene.Viewport.RasterWidthPoints, 1.0);
        var rasterHeightPoints = Math.Max(scene.Viewport.RasterHeightPoints, 1.0);
        var pixelWidth = Math.Max(1, (int)Math.Round(rasterWidthPoints * RasterDpi / SvgDpi));
        var pixelHeight = Math.Max(1, (int)Math.Round(rasterHeightPoints * RasterDpi / SvgDpi));
        var scaleX = pixelWidth / rasterWidthPoints;
        var scaleY = pixelHeight / rasterHeightPoints;
        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using SKSurface surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create Skia raster surface.");

        var canvas = surface.Canvas;
        var clearColor = format switch
        {
            GraphRasterImageFormat.Png when scene.Style.ShowBackgroundRect => ParseColor(scene.Style.BackgroundColor),
            GraphRasterImageFormat.Png => SKColors.Transparent,
            _ => GetJpegFallbackBackground()
        };
        canvas.Clear(clearColor);
        canvas.Scale((float)scaleX, (float)scaleY);

        var transform = ParseTransform(scene.Canvas.Transform);
        canvas.Translate((float)transform.TranslateX, (float)transform.TranslateY);
        if (Math.Abs(transform.RotationDegrees) > double.Epsilon)
        {
            canvas.RotateDegrees((float)transform.RotationDegrees);
        }

        if (Math.Abs(transform.ScaleX - 1.0) > double.Epsilon || Math.Abs(transform.ScaleY - 1.0) > double.Epsilon)
        {
            canvas.Scale((float)transform.ScaleX, (float)transform.ScaleY);
        }

        if (scene.Style.ShowBackgroundRect)
        {
            DrawBackgroundPolygon(canvas, scene);
        }

        DrawEdges(canvas, scene);
        DrawNodes(canvas, scene);
        canvas.Flush();

        using SKImage renderedImage = surface.Snapshot();
        using SKImage encodedImage = format == GraphRasterImageFormat.Jpg
            ? FlattenForJpeg(renderedImage, info, scene)
            : renderedImage;
        using SKData data = encodedImage.Encode(
            format == GraphRasterImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
            quality);

        return new GraphRasterRenderResult(
            data.ToArray(),
            pixelWidth,
            pixelHeight,
            scaleX,
            scaleY,
            GraphRasterBackendSelection.Selected.ToString(),
            format.ToString(),
            format == GraphRasterImageFormat.Jpg);
    }

    private static void DrawBackgroundPolygon(SKCanvas canvas, GraphRenderScene scene)
    {
        var points = ParsePolygonPoints(scene.Canvas.BackgroundPolygonPoints);
        using var path = new SKPath();
        path.MoveTo(points[0]);
        for (var index = 1; index < points.Count; index++)
        {
            path.LineTo(points[index]);
        }

        path.Close();
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = ParseColor(scene.Style.BackgroundColor),
            IsAntialias = true,
        };

        canvas.DrawPath(path, paint);
    }

    private static void DrawEdges(SKCanvas canvas, GraphRenderScene scene)
    {
        foreach (var edge in scene.Edges)
        {
            using SKPath path = SKPath.ParseSvgPathData(edge.PathData);
            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = ParseColor(edge.Stroke),
                StrokeWidth = (float)edge.StrokeWidth,
                StrokeCap = edge.StrokeLineCap.Equals("round", StringComparison.OrdinalIgnoreCase)
                    ? SKStrokeCap.Round
                    : SKStrokeCap.Butt,
                IsAntialias = true,
            };

            canvas.DrawPath(path, strokePaint);

            if (!string.IsNullOrWhiteSpace(edge.MarkerEnd) && scene.Style.ArrowStyle is not null)
            {
                DrawArrowHead(canvas, path, scene.Style.ArrowStyle, ParseColor(edge.Stroke));
            }
        }
    }

    private static void DrawNodes(SKCanvas canvas, GraphRenderScene scene)
    {
        foreach (var node in scene.Nodes)
        {
            var oval = SKRect.Create(
                (float)(node.X - node.RadiusX),
                (float)(node.Y - node.RadiusY),
                (float)(node.RadiusX * 2.0),
                (float)(node.RadiusY * 2.0));

            using (var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = ParseColor(node.Fill),
                IsAntialias = true,
            })
            {
                canvas.DrawOval(oval, fillPaint);
            }

            using (var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = ParseColor(node.Stroke),
                StrokeWidth = (float)node.StrokeWidth,
                IsAntialias = true,
            })
            {
                canvas.DrawOval(oval, strokePaint);
            }

            if (node.Label is null)
            {
                continue;
            }

            using var textPaint = new SKPaint
            {
                Color = ParseColor(node.Label.Fill),
                TextSize = (float)node.Label.FontSize,
                IsAntialias = true,
            };
            canvas.DrawText(node.Label.Text, (float)node.Label.X, (float)node.Label.BaselineY, textPaint);
        }
    }

    private static void DrawArrowHead(SKCanvas canvas, SKPath edgePath, GraphRenderArrowStyle arrowStyle, SKColor strokeColor)
    {
        using var measure = new SKPathMeasure(edgePath, false);
        var length = measure.Length;
        if (length <= 0.0f)
        {
            return;
        }

        if (!measure.GetPositionAndTangent(Math.Max(0.0f, length - 0.01f), out var position, out var tangent))
        {
            return;
        }

        var scale = Math.Max(0.05, arrowStyle.ArrowSize);
        var arrowLength = 6.0f * (float)scale;
        var arrowHeight = 6.0f * (float)scale;
        var normal = new SKPoint(-tangent.Y, tangent.X);
        var basePoint = new SKPoint(position.X - (tangent.X * arrowLength), position.Y - (tangent.Y * arrowLength));
        var leftPoint = new SKPoint(basePoint.X + (normal.X * (arrowHeight / 2.0f)), basePoint.Y + (normal.Y * (arrowHeight / 2.0f)));
        var rightPoint = new SKPoint(basePoint.X - (normal.X * (arrowHeight / 2.0f)), basePoint.Y - (normal.Y * (arrowHeight / 2.0f)));

        using var path = new SKPath();
        path.MoveTo(position);
        path.LineTo(leftPoint);
        path.LineTo(rightPoint);
        path.Close();

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = strokeColor,
            IsAntialias = true,
        };

        canvas.DrawPath(path, fillPaint);
    }

    private static SKImage FlattenForJpeg(SKImage sourceImage, SKImageInfo info, GraphRenderScene scene)
    {
        using SKSurface surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Failed to create Skia JPEG flatten surface.");

        var canvas = surface.Canvas;
        canvas.Clear(scene.Style.ShowBackgroundRect ? ParseColor(scene.Style.BackgroundColor) : GetJpegFallbackBackground());
        canvas.DrawImage(sourceImage, 0, 0);
        canvas.Flush();
        return surface.Snapshot();
    }

    private static SKColor ParseColor(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var hex = value.StartsWith('#') ? value[1..] : value;
        return hex.Length switch
        {
            6 => new SKColor(
                ParseByte(hex, 0),
                ParseByte(hex, 2),
                ParseByte(hex, 4),
                byte.MaxValue),
            8 => new SKColor(
                ParseByte(hex, 0),
                ParseByte(hex, 2),
                ParseByte(hex, 4),
                ParseByte(hex, 6)),
            _ => throw new NotSupportedException($"Color '{value}' is not supported. Expected #RRGGBB or #RRGGBBAA.")
        };
    }

    private static byte ParseByte(string hex, int startIndex)
        => byte.Parse(hex.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static SKColor GetJpegFallbackBackground() => new(255, 255, 254, 255);

    private static IReadOnlyList<SKPoint> ParsePolygonPoints(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var points = new List<SKPoint>();
        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = token.Split(',', StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
            {
                throw new FormatException($"Invalid polygon point token '{token}'.");
            }

            points.Add(new SKPoint(
                float.Parse(pair[0], CultureInfo.InvariantCulture),
                float.Parse(pair[1], CultureInfo.InvariantCulture)));
        }

        if (points.Count < 3)
        {
            throw new FormatException("Polygon must contain at least three points.");
        }

        return points;
    }

    private static GraphTransform ParseTransform(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var match = Regex.Match(
            value,
            @"^scale\((?<scaleX>-?\d+(?:\.\d+)?) (?<scaleY>-?\d+(?:\.\d+)?)\) rotate\((?<rotation>-?\d+(?:\.\d+)?)\) translate\((?<translateX>-?\d+(?:\.\d+)?) (?<translateY>-?\d+(?:\.\d+)?)\)$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            throw new FormatException($"Unsupported graph transform '{value}'.");
        }

        return new GraphTransform(
            double.Parse(match.Groups["scaleX"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["scaleY"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["rotation"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["translateX"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["translateY"].Value, CultureInfo.InvariantCulture));
    }

    private readonly record struct GraphTransform(
        double ScaleX,
        double ScaleY,
        double RotationDegrees,
        double TranslateX,
        double TranslateY);
}

public sealed record GraphRasterRenderResult(
    byte[] Bytes,
    int PixelWidth,
    int PixelHeight,
    double ScaleX,
    double ScaleY,
    string Backend,
    string Format,
    bool FlattenedForOpaqueOutput);

internal enum GraphRasterImageFormat
{
    Png = 1,
    Jpg = 2,
}
