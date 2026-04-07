using SkiaSharp;

namespace PSGraphView.Graphviz.Tests;

public sealed class GraphSceneRasterRendererTests
{
    private readonly GraphSceneRasterRenderer _renderer = new();

    [Fact]
    public void Render_Png_ProducesDecodableImageWithExpectedSize()
    {
        var scene = CreateScene();

        var bytes = _renderer.Render(scene, RasterOutputKind.Png);

        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(60, bitmap.Height);
    }

    [Fact]
    public void Render_Jpeg_ProducesDecodableImageWithExpectedSize()
    {
        var scene = CreateScene();

        var bytes = _renderer.Render(scene, RasterOutputKind.Jpeg);

        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);

        using var bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);
        Assert.Equal(100, bitmap.Width);
        Assert.Equal(60, bitmap.Height);
    }

    private static GraphScene CreateScene()
    {
        return new GraphScene(
            "G",
            new SceneRect(0, 0, 100, 60),
            commands:
            [
                new PolylineCommand(
                    [new ScenePoint(0, 0), new ScenePoint(100, 0), new ScenePoint(100, 60)],
                    new SceneColor(0x11, 0x22, 0x33),
                    new SceneStrokeStyle(1.5, SceneLinePattern.Dashed))
            ],
            objects:
            [
                new SceneObject(
                    SceneObjectKind.Node,
                    "A",
                    commands:
                    [
                        new EllipseCommand(
                            new ScenePoint(20, 15),
                            12,
                            8,
                            Stroke: new SceneColor(0x44, 0x55, 0x66),
                            Fill: new SceneGradient(
                                SceneGradientKind.Linear,
                                new ScenePoint(8, 15),
                                new ScenePoint(32, 15),
                                [new SceneGradientStop(0, new SceneColor(0xff, 0xff, 0xff)), new SceneGradientStop(1, new SceneColor(0xaa, 0xaa, 0xaa))])),
                        new TextCommand(
                            new ScenePoint(20, 15),
                            "Node A",
                            new SceneFont("Helvetica", 12, SceneFontStyle.Bold | SceneFontStyle.Underline),
                            new SceneColor(0x10, 0x20, 0x30),
                            40,
                            SceneTextAlignment.Center)
                    ]),
                new SceneObject(
                    SceneObjectKind.Edge,
                    "edge:0",
                    commands:
                    [
                        new BezierCommand(
                            [new ScenePoint(20, 15), new ScenePoint(35, 20), new ScenePoint(45, 24), new ScenePoint(55, 25)],
                            new SceneColor(0x77, 0x88, 0x99)),
                        new PolygonCommand(
                            [new ScenePoint(55, 25), new ScenePoint(50, 27), new ScenePoint(50, 23)],
                            stroke: new SceneColor(0x77, 0x88, 0x99),
                            fill: new SceneColor(0x77, 0x88, 0x99))
                    ])
            ]);
    }
}
