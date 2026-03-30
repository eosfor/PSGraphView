using System.Globalization;
using System.Text.RegularExpressions;
using PSGraph.Model;
using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed partial class SfdpEdgeRouterTests
{
    [Fact]
    public void RouteEdges_ForVeryShortEdge_KeepsPathBetweenNodes()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);
        var x = new[] { 10.0, 10.5 };
        var y = new[] { 2.0, 2.0 };

        var routed = SfdpEdgeRouter.RouteEdges(graph, x, y, new SfdpOptions
        {
            NodeRadius = 0.72,
            ShowArrows = false
        });

        var coordinates = ParseCoordinates(routed[0].PathData);
        var minX = Math.Min(x[0], x[1]);
        var maxX = Math.Max(x[0], x[1]);

        Assert.All(coordinates.Where((_, index) => index % 2 == 0), value =>
        {
            Assert.InRange(value, minX - 0.001, maxX + 0.001);
        });
    }

    [Fact]
    public void RouteEdges_ForVeryShortArrowEdge_KeepsPathBetweenNodes()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1)
            ]);
        var x = new[] { 10.0, 10.5 };
        var y = new[] { 2.0, 2.0 };

        var routed = SfdpEdgeRouter.RouteEdges(graph, x, y, new SfdpOptions
        {
            NodeRadius = 0.72,
            ShowArrows = true
        });

        var coordinates = ParseCoordinates(routed[0].PathData);
        var minX = Math.Min(x[0], x[1]);
        var maxX = Math.Max(x[0], x[1]);

        Assert.All(coordinates.Where((_, index) => index % 2 == 0), value =>
        {
            Assert.InRange(value, minX - 0.001, maxX + 0.001);
        });
    }

    private static double[] ParseCoordinates(string pathData)
    {
        return CoordinateRegex()
            .Matches(pathData)
            .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex CoordinateRegex();
}
