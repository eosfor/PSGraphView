using System.Text.Json;

namespace PSGraphView.Graphviz.Tests;

public sealed class SceneModelTests
{
    [Fact]
    public void GraphScene_SerializesPolymorphicCommands()
    {
        var scene = new GraphScene(
            "G",
            new SceneRect(0, 0, 120, 80),
            commands:
            [
                new EllipseCommand(
                    new ScenePoint(12, 14),
                    10,
                    6,
                    Stroke: new SceneColor(1, 2, 3),
                    Fill: new SceneColor(250, 251, 252),
                    StrokeStyle: new SceneStrokeStyle(1.5, SceneLinePattern.Dashed))
            ],
            objects:
            [
                new SceneObject(
                    SceneObjectKind.Node,
                    "A",
                    new SceneRect(8, 10, 20, 12),
                    [
                        new TextCommand(
                            new ScenePoint(12, 14),
                            "node-A",
                            new SceneFont("Helvetica", 12),
                            new SceneColor(10, 20, 30),
                            42,
                            SceneTextAlignment.Center)
                    ])
            ]);

        var json = JsonSerializer.Serialize(scene);

        Assert.Contains("\"$type\":\"ellipse\"", json, StringComparison.Ordinal);
        Assert.Contains("\"$type\":\"text\"", json, StringComparison.Ordinal);
        Assert.Contains("\"$type\":\"color\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Name\":\"G\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Kind\":1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SceneModel_NormalizesNullCollectionsToEmpty()
    {
        var gradient = new SceneGradient(
            SceneGradientKind.Linear,
            new ScenePoint(0, 0),
            new ScenePoint(10, 10),
            stops: null);

        var sceneObject = new SceneObject(SceneObjectKind.Edge, "A->B", commands: null);
        var scene = new GraphScene("G", commands: null, objects: null);

        Assert.Empty(gradient.Stops);
        Assert.Empty(sceneObject.Commands);
        Assert.Empty(scene.Commands);
        Assert.Empty(scene.Objects);
    }
}
