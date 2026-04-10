namespace PSGraphView.Graphviz.Tests;

public sealed class GraphvizXdotJsonSceneInterpreterTests
{
    private readonly GraphvizXdotJsonSceneInterpreter _interpreter = new();

    [Fact]
    public void Interpret_ParsesGraphNodeSubgraphAndEdgeCommands()
    {
        const string xdotJson = """
        {
          "name": "G",
          "directed": true,
          "strict": false,
          "_subgraph_cnt": 1,
          "bb": "0,0,100,50",
          "_draw_": [
            { "op": "c", "grad": "none", "color": "#112233" },
            { "op": "L", "points": [[0,0], [100,0], [100,50], [0,50], [0,0]] }
          ],
          "objects": [
            {
              "_gvid": 0,
              "name": "cluster_0",
              "bb": "5,5,45,25",
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#445566" },
                { "op": "p", "points": [[5,5], [45,5], [45,25], [5,25]] }
              ]
            },
            {
              "_gvid": 1,
              "name": "A",
              "bb": "10,10,30,20",
              "_draw_": [
                { "op": "C", "grad": "none", "color": "#ffffff" },
                { "op": "c", "grad": "none", "color": "#000000" },
                { "op": "S", "style": "setlinewidth(2),dashed" },
                { "op": "P", "points": [[10,10], [30,10], [30,20], [10,20]] }
              ],
              "_ldraw_": [
                { "op": "F", "size": 14, "face": "Helvetica" },
                { "op": "c", "grad": "none", "color": "#123456" },
                { "op": "T", "pt": [20,15], "align": "c", "width": 22, "text": "Node A" }
              ]
            }
          ],
          "edges": [
            {
              "_gvid": 0,
              "tail": 1,
              "head": 1,
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#010203" },
                { "op": "B", "points": [[30,15], [40,20], [50,20], [60,15]] }
              ],
              "_hdraw_": [
                { "op": "C", "grad": "none", "color": "#010203" },
                { "op": "c", "grad": "none", "color": "#010203" },
                { "op": "P", "points": [[60,15], [56,17], [56,13]] }
              ],
              "_ldraw_": [
                { "op": "F", "size": 10, "face": "Helvetica" },
                { "op": "c", "grad": "none", "color": "#0a0b0c" },
                { "op": "T", "pt": [45,24], "align": "l", "width": 30, "text": "edge" }
              ]
            }
          ]
        }
        """;

        var scene = _interpreter.Interpret(xdotJson);

        Assert.Equal("G", scene.Name);
        Assert.Equal(new SceneRect(0, 0, 100, 50), scene.Bounds);

        var graphLine = Assert.IsType<PolylineCommand>(Assert.Single(scene.Commands));
        Assert.Equal(5, graphLine.Points.Count);
        Assert.Equal(new SceneColor(0x11, 0x22, 0x33), graphLine.Stroke);

        Assert.Equal(3, scene.Objects.Count);
        Assert.Equal(SceneObjectKind.Subgraph, scene.Objects[0].Kind);
        Assert.Equal(SceneObjectKind.Node, scene.Objects[1].Kind);
        Assert.Equal(SceneObjectKind.Edge, scene.Objects[2].Kind);

        var subgraphPolygon = Assert.IsType<PolygonCommand>(Assert.Single(scene.Objects[0].Commands));
        Assert.Equal(new SceneColor(0x44, 0x55, 0x66), subgraphPolygon.Stroke);
        Assert.Null(subgraphPolygon.Fill);

        Assert.Equal(2, scene.Objects[1].Commands.Count);
        var nodePolygon = Assert.IsType<PolygonCommand>(scene.Objects[1].Commands[0]);
        Assert.Equal(new SceneColor(0x00, 0x00, 0x00), nodePolygon.Stroke);
        Assert.Equal(new SceneColor(0xff, 0xff, 0xff), nodePolygon.Fill);
        Assert.Equal(new SceneStrokeStyle(2, SceneLinePattern.Dashed), nodePolygon.StrokeStyle);

        var nodeText = Assert.IsType<TextCommand>(scene.Objects[1].Commands[1]);
        Assert.Equal("Node A", nodeText.Text);
        Assert.Equal(new SceneFont("Helvetica", 14), nodeText.Font);
        Assert.Equal(new SceneColor(0x12, 0x34, 0x56), nodeText.Color);
        Assert.Equal(SceneTextAlignment.Center, nodeText.Alignment);

        Assert.Equal(3, scene.Objects[2].Commands.Count);
        var edgeBezier = Assert.IsType<BezierCommand>(scene.Objects[2].Commands[0]);
        Assert.Equal(new SceneColor(0x01, 0x02, 0x03), edgeBezier.Stroke);

        var edgeArrow = Assert.IsType<PolygonCommand>(scene.Objects[2].Commands[1]);
        Assert.Equal(new SceneColor(0x01, 0x02, 0x03), edgeArrow.Fill);

        var edgeText = Assert.IsType<TextCommand>(scene.Objects[2].Commands[2]);
        Assert.Equal("edge", edgeText.Text);
        Assert.Equal(SceneTextAlignment.Left, edgeText.Alignment);
        Assert.Equal(new SceneColor(0x0a, 0x0b, 0x0c), edgeText.Color);
    }

    [Fact]
    public void Interpret_ParsesGradientPaint()
    {
        const string xdotJson = """
        {
          "name": "G",
          "directed": true,
          "strict": false,
          "_subgraph_cnt": 0,
          "objects": [
            {
              "_gvid": 0,
              "name": "A",
              "_draw_": [
                { "op": "C", "grad": "linear", "p0": [0,0], "p1": [10,10], "stops": [ { "frac": 0.0, "color": "#000000" }, { "frac": 1.0, "color": "#ffffff" } ] },
                { "op": "c", "grad": "none", "color": "#111111" },
                { "op": "E", "rect": [5,5,4,3] }
              ]
            }
          ]
        }
        """;

        var scene = _interpreter.Interpret(xdotJson);

        var ellipse = Assert.IsType<EllipseCommand>(Assert.Single(scene.Objects[0].Commands));
        var gradient = Assert.IsType<SceneGradient>(ellipse.Fill);
        Assert.Equal(SceneGradientKind.Linear, gradient.Kind);
        Assert.Equal(2, gradient.Stops.Count);
        Assert.Equal(new SceneColor(0x11, 0x11, 0x11), ellipse.Stroke);
    }

    [Fact]
    public void Interpret_ParsesFontCharacterFlagsAndMixedLabelDrawCommands()
    {
        const string xdotJson = """
        {
          "name": "G",
          "_subgraph_cnt": 0,
          "objects": [
            {
              "_gvid": 0,
              "name": "RecordNode",
              "_ldraw_": [
                { "op": "c", "grad": "none", "color": "#222222" },
                { "op": "L", "points": [[10,10], [50,10], [50,30], [10,30], [10,10]] },
                { "op": "F", "size": 11, "face": "Helvetica" },
                { "op": "t", "fontchar": 5 },
                { "op": "T", "pt": [30,20], "align": "c", "width": 26, "text": "Cell" }
              ]
            }
          ]
        }
        """;

        var scene = _interpreter.Interpret(xdotJson);

        Assert.Equal(2, scene.Objects[0].Commands.Count);

        var outline = Assert.IsType<PolylineCommand>(scene.Objects[0].Commands[0]);
        Assert.Equal(5, outline.Points.Count);
        Assert.Equal(new SceneColor(0x22, 0x22, 0x22), outline.Stroke);

        var label = Assert.IsType<TextCommand>(scene.Objects[0].Commands[1]);
        Assert.Equal("Cell", label.Text);
        Assert.Equal(new SceneFont("Helvetica", 11, SceneFontStyle.Bold | SceneFontStyle.Underline), label.Font);
        Assert.Equal(SceneTextAlignment.Center, label.Alignment);
    }

    [Fact]
    public void Interpret_AllowsEmptyTextDrawCommands()
    {
        const string xdotJson = """
        {
          "name": "G",
          "_subgraph_cnt": 0,
          "objects": [
            {
              "_gvid": 0,
              "name": "RecordNode",
              "_ldraw_": [
                { "op": "F", "size": 11, "face": "Helvetica" },
                { "op": "c", "grad": "none", "color": "#222222" },
                { "op": "T", "pt": [30,20], "align": "c", "width": 26, "text": "" }
              ]
            }
          ]
        }
        """;

        var scene = _interpreter.Interpret(xdotJson);

        var label = Assert.IsType<TextCommand>(Assert.Single(scene.Objects[0].Commands));
        Assert.Equal(string.Empty, label.Text);
        Assert.Equal(new SceneColor(0x22, 0x22, 0x22), label.Color);
    }

    [Fact]
    public void Interpret_ThrowsForUnsupportedOperation()
    {
        const string xdotJson = """
        {
          "name": "G",
          "directed": true,
          "strict": false,
          "_subgraph_cnt": 0,
          "objects": [
            {
              "_gvid": 0,
              "name": "A",
              "_draw_": [
                { "op": "I", "x": 1, "y": 2, "w": 3, "h": 4, "name": "icon.png" }
              ]
            }
          ]
        }
        """;

        var exception = Assert.Throws<NotSupportedException>(() => _interpreter.Interpret(xdotJson));

        Assert.Contains("operation 'I'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Interpret_TraversesNestedSubgraphsUsingMembershipReferences()
    {
        const string xdotJson = """
        {
          "name": "G",
          "_subgraph_cnt": 2,
          "objects": [
            {
              "_gvid": 0,
              "name": "cluster_outer",
              "subgraphs": [1],
              "nodes": [2, 3],
              "edges": [0],
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#101010" },
                { "op": "p", "points": [[0,0], [90,0], [90,90], [0,90]] }
              ],
              "_ldraw_": [
                { "op": "F", "size": 14, "face": "Helvetica" },
                { "op": "c", "grad": "none", "color": "#202020" },
                { "op": "T", "pt": [45,80], "align": "c", "width": 40, "text": "Outer" }
              ]
            },
            {
              "_gvid": 1,
              "name": "cluster_inner",
              "nodes": [3],
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#303030" },
                { "op": "p", "points": [[10,10], [60,10], [60,60], [10,60]] }
              ],
              "_ldraw_": [
                { "op": "F", "size": 12, "face": "Helvetica" },
                { "op": "c", "grad": "none", "color": "#404040" },
                { "op": "T", "pt": [35,52], "align": "c", "width": 36, "text": "Inner" }
              ]
            },
            {
              "_gvid": 2,
              "name": "B",
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#505050" },
                { "op": "e", "rect": [70,20,10,8] }
              ]
            },
            {
              "_gvid": 3,
              "name": "A",
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#606060" },
                { "op": "e", "rect": [30,30,10,8] }
              ]
            }
          ],
          "edges": [
            {
              "_gvid": 0,
              "tail": 3,
              "head": 2,
              "_draw_": [
                { "op": "c", "grad": "none", "color": "#707070" },
                { "op": "B", "points": [[40,28], [50,24], [58,22], [64,20]] }
              ]
            }
          ]
        }
        """;

        var scene = _interpreter.Interpret(xdotJson);

        Assert.Equal(5, scene.Objects.Count);
        Assert.Equal(
            ["cluster_outer", "cluster_inner", "A", "B", "edge:0"],
            scene.Objects.Select(item => item.Name ?? string.Empty).ToArray());
        Assert.Equal(
            [SceneObjectKind.Subgraph, SceneObjectKind.Subgraph, SceneObjectKind.Node, SceneObjectKind.Node, SceneObjectKind.Edge],
            scene.Objects.Select(item => item.Kind).ToArray());

        var outerLabel = Assert.IsType<TextCommand>(scene.Objects[0].Commands[1]);
        Assert.Equal("Outer", outerLabel.Text);

        var innerLabel = Assert.IsType<TextCommand>(scene.Objects[1].Commands[1]);
        Assert.Equal("Inner", innerLabel.Text);

        Assert.IsType<EllipseCommand>(Assert.Single(scene.Objects[2].Commands));
        Assert.IsType<EllipseCommand>(Assert.Single(scene.Objects[3].Commands));
        Assert.IsType<BezierCommand>(Assert.Single(scene.Objects[4].Commands));
    }

    [Fact]
    public void Interpret_ThrowsForOutOfRangeMembershipReference()
    {
        const string xdotJson = """
        {
          "name": "G",
          "_subgraph_cnt": 1,
          "objects": [
            {
              "_gvid": 0,
              "name": "cluster_outer",
              "nodes": [3]
            },
            {
              "_gvid": 1,
              "name": "A"
            }
          ]
        }
        """;

        var exception = Assert.Throws<InvalidDataException>(() => _interpreter.Interpret(xdotJson));

        Assert.Contains("node index '3' is out of range", exception.Message, StringComparison.Ordinal);
    }

}

[Collection(GraphvizNativeCollection.Name)]
public sealed class GraphvizXdotJsonSceneInterpreterNativeTests
{
    private readonly GraphvizXdotJsonSceneInterpreter _interpreter = new();

    [GraphvizNativeFact]
    public void Interpret_CanHandleNativeXdotJsonPayload()
    {
        GraphvizNativeSessionTests.EnsureNativeLibraryAvailable();

        using var session = new GraphvizNativeSession();
        var result = session.LayoutDot(
            "digraph G { graph [rankdir=LR]; A [shape=box]; A -> B [label=\"edge\"]; }",
            new GraphvizNativeLayoutRequest("dot", IncludeXdot: true));

        var scene = _interpreter.Interpret(result.XdotJson);

        Assert.Equal("G", scene.Name);
        Assert.NotEmpty(scene.Objects);
        Assert.Contains(scene.Objects, item => item.Kind == SceneObjectKind.Node);
        Assert.Contains(scene.Objects, item => item.Kind == SceneObjectKind.Edge);
        Assert.Contains(scene.Objects, item => item.Commands.Count > 0);
    }
}
