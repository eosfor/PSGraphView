using SkiaSharp;

namespace PSGraphView.GVExport.Tests;

public sealed class GraphRasterRenderSceneWriterTests
{
    [Fact]
    public void RenderPng_WritesExpectedPixelDimensions()
    {
        var scene = CreateScene();

        var result = GraphRasterRenderSceneWriter.RenderPng(scene);

        Assert.Equal(64, result.PixelWidth);
        Assert.Equal(32, result.PixelHeight);
        Assert.Equal("SkiaSharp", result.Backend);
        Assert.False(result.FlattenedForOpaqueOutput);
        Assert.Equal("Slight", result.TextHintingLevel);
        Assert.True(result.SubpixelText);
        Assert.True(result.LcdRenderText);
        Assert.True(result.AutohintedText);

        using SKData data = SKData.CreateCopy(result.Bytes);
        using SKImage image = SKImage.FromEncodedData(data)!;
        Assert.Equal(64, image.Width);
        Assert.Equal(32, image.Height);
    }

    [Fact]
    public void RenderJpg_WritesOpaqueRaster()
    {
        var scene = CreateScene();

        var result = GraphRasterRenderSceneWriter.RenderJpg(scene);

        Assert.Equal(64, result.PixelWidth);
        Assert.Equal(32, result.PixelHeight);
        Assert.True(result.FlattenedForOpaqueOutput);
        Assert.Equal(90, result.EncodeQuality);
        Assert.Equal("GraphvizLikeGdThreshold", result.OpaqueOutputPolicy);
        Assert.Equal("#fffffeff", result.OpaqueFallbackColor);
        Assert.Equal((byte)64, result.OpaqueAlphaThreshold);
        Assert.Equal("Slight", result.TextHintingLevel);
        Assert.True(result.SubpixelText);
        Assert.True(result.LcdRenderText);
        Assert.True(result.AutohintedText);

        using SKData data = SKData.CreateCopy(result.Bytes);
        using SKImage image = SKImage.FromEncodedData(data)!;
        Assert.Equal(64, image.Width);
        Assert.Equal(32, image.Height);
    }

    [Fact]
    public void RenderJpg_UsesGraphvizLikeThresholdForMostlyTransparentPixels()
    {
        var scene = CreateSceneWithTransparentNode("#2080ff30");

        var result = GraphRasterRenderSceneWriter.RenderJpg(scene);

        using SKData data = SKData.CreateCopy(result.Bytes);
        using SKImage image = SKImage.FromEncodedData(data)!;
        using var bitmap = ReadBitmap(image);

        SKColor center = bitmap.GetPixel(24, 16);
        Assert.InRange(center.Red, 240, 255);
        Assert.InRange(center.Green, 240, 255);
        Assert.InRange(center.Blue, 238, 255);
    }

    [Fact]
    public void RenderJpg_KeepsRgbForOpaqueEnoughPixelsWithoutBlendingToWhite()
    {
        var scene = CreateSceneWithTransparentNode("#2080ff80");

        var result = GraphRasterRenderSceneWriter.RenderJpg(scene);

        using SKData data = SKData.CreateCopy(result.Bytes);
        using SKImage image = SKImage.FromEncodedData(data)!;
        using var bitmap = ReadBitmap(image);

        SKColor center = bitmap.GetPixel(24, 16);
        Assert.InRange(center.Red, 0, 90);
        Assert.InRange(center.Green, 64, 176);
        Assert.InRange(center.Blue, 180, 255);
    }

    [Fact]
    public void RenderPng_ReportsResolvedLabelFontFamilies()
    {
        var viewport = new GraphRenderViewport(0.0, 0.0, 48.0, 24.0, 48.0, 24.0, 48.0, 24.0);
        var style = new GraphRenderStyle(true, "#ffffff", null);
        var canvas = new GraphRenderCanvas("graph0", "graph", "G", "scale(1 1) rotate(0) translate(0 0)", "0,0 0,0 0,0 0,0");
        var label = new GraphRenderLabel("NodeA", 24, 16, 14, "Times,serif", "middle", "#000000");
        var nodes = new[]
        {
            new GraphRenderNode("node1", "A", "A", 24, 16, 8, 8, "#ffffff", "#000000", 1.0, label)
        };
        var scene = new GraphRenderScene(viewport, style, canvas, Array.Empty<GraphRenderEdge>(), nodes);

        var result = GraphRasterRenderSceneWriter.RenderPng(scene);

        Assert.False(string.IsNullOrWhiteSpace(result.ResolvedLabelFontFamilies));
    }

    private static GraphRenderScene CreateScene()
    {
        var viewport = new GraphRenderViewport(0.0, 0.0, 48.0, 24.0, 48.0, 24.0, 48.0, 24.0);
        var style = new GraphRenderStyle(true, "#ffffff", new GraphRenderArrowStyle("arrowhead", "#000000", 1.0));
        var canvas = new GraphRenderCanvas("graph0", "graph", "G", "scale(1 1) rotate(0) translate(4 20)", "-4,4 -4,-20 44,-20 44,4");
        var edges = new[]
        {
            new GraphRenderEdge("edge1", "A->B", "M4,10C10,10 18,10 24,10", "#000000", 0.8, "round", "url(#arrowhead)")
        };
        var nodes = new[]
        {
            new GraphRenderNode("node1", "A", "A", 4, 10, 1, 1, "#696969", "#555555", 1.0, null),
            new GraphRenderNode("node2", "B", "B", 24, 10, 1, 1, "#696969", "#555555", 1.0, null)
        };

        return new GraphRenderScene(viewport, style, canvas, edges, nodes);
    }

    private static GraphRenderScene CreateSceneWithTransparentNode(string fill)
    {
        var viewport = new GraphRenderViewport(0.0, 0.0, 48.0, 24.0, 48.0, 24.0, 48.0, 24.0);
        var style = new GraphRenderStyle(false, "#ffffff", null);
        var canvas = new GraphRenderCanvas("graph0", "graph", "G", "scale(1 1) rotate(0) translate(0 0)", "0,0 0,0 0,0 0,0");
        var nodes = new[]
        {
            new GraphRenderNode("node1", "A", "A", 24, 16, 8, 8, fill, fill, 0.0, null)
        };

        return new GraphRenderScene(viewport, style, canvas, Array.Empty<GraphRenderEdge>(), nodes);
    }

    private static SKBitmap ReadBitmap(SKImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        var ok = image.ReadPixels(info, bitmap.GetPixels(), info.RowBytes, 0, 0);
        Assert.True(ok);
        return bitmap;
    }
}
