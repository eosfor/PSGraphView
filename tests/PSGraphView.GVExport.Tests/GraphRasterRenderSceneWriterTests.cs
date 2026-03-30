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

        using SKData data = SKData.CreateCopy(result.Bytes);
        using SKImage image = SKImage.FromEncodedData(data)!;
        Assert.Equal(64, image.Width);
        Assert.Equal(32, image.Height);
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
}
