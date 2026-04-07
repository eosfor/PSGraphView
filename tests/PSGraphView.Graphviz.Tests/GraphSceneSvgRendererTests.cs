namespace PSGraphView.Graphviz.Tests;

public sealed class GraphSceneSvgRendererTests
{
    private readonly GraphSceneSvgRenderer _renderer = new();

    [Fact]
    public void Render_ProducesSvgForSceneCommandsAndObjects()
    {
        var scene = new GraphScene(
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

        var svg = _renderer.Render(scene);

        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 100 60\"", svg, StringComparison.Ordinal);
        Assert.Contains("<defs>", svg, StringComparison.Ordinal);
        Assert.Contains("<linearGradient", svg, StringComparison.Ordinal);
        Assert.Contains("<ellipse", svg, StringComparison.Ordinal);
        Assert.Contains("<polyline", svg, StringComparison.Ordinal);
        Assert.Contains("<path", svg, StringComparison.Ordinal);
        Assert.Contains("<polygon", svg, StringComparison.Ordinal);
        Assert.Contains("<text", svg, StringComparison.Ordinal);
        Assert.Contains("data-kind=\"node\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-name=\"A\"", svg, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\"", svg, StringComparison.Ordinal);
        Assert.Contains("text-decoration=\"underline\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_FlipsVerticalCoordinatesIntoSvgSpace()
    {
        var scene = new GraphScene(
            "G",
            new SceneRect(0, 0, 100, 60),
            objects:
            [
                new SceneObject(
                    SceneObjectKind.Node,
                    "A",
                    commands:
                    [
                        new EllipseCommand(
                            new ScenePoint(20, 15),
                            10,
                            6,
                            Stroke: new SceneColor(0, 0, 0)),
                        new TextCommand(
                            new ScenePoint(20, 15),
                            "A",
                            new SceneFont("Helvetica", 10, SceneFontStyle.Italic | SceneFontStyle.Strikethrough),
                            new SceneColor(0, 0, 0),
                            10)
                    ])
            ]);

        var svg = _renderer.Render(scene);

        Assert.Contains("cx=\"20\"", svg, StringComparison.Ordinal);
        Assert.Contains("cy=\"45\"", svg, StringComparison.Ordinal);
        Assert.Contains("y=\"45\"", svg, StringComparison.Ordinal);
        Assert.Contains("font-style=\"italic\"", svg, StringComparison.Ordinal);
        Assert.Contains("text-decoration=\"line-through\"", svg, StringComparison.Ordinal);
    }
}
