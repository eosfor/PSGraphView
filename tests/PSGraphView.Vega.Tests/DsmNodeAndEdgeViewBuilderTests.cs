using Newtonsoft.Json.Linq;
using PSGraph.Model;
using PSGraphView.Vega;

namespace PSGraphView.Vega.Tests;

public class DsmNodeAndEdgeViewBuilderTests
{
    private readonly DsmNodeAndEdgeViewBuilder _builder = new();

    [Fact]
    public void Build_WithPartitions_AssignsGroupsAndCrossGroupEdges()
    {
        var a = new PSVertex("A");
        var b = new PSVertex("B");
        var c = new PSVertex("C");
        var orderedVertices = new List<PSVertex> { a, b, c };
        var matrix = new double[,]
        {
            { 0, 1, 0 },
            { 1, 0, 1 },
            { 0, 0, 0 }
        };
        var partitions = new List<IReadOnlyList<PSVertex>>
        {
            new List<PSVertex> { a, b },
            new List<PSVertex> { c }
        };

        var json = _builder.Build(orderedVertices, (row, column) => matrix[row, column], partitions);

        var nodes = (JArray)json["nodes"]!["values"]!;
        var edges = (JArray)json["edges"]!["values"]!;

        Assert.Equal(3, nodes.Count);
        var groupMap = nodes.ToDictionary(node => (string)node["name"]!, node => (int)node["group"]!);
        Assert.Equal(1, groupMap["A"]);
        Assert.Equal(1, groupMap["B"]);
        Assert.Equal(2, groupMap["C"]);

        var indexMap = nodes.ToDictionary(node => (string)node["name"]!, node => (int)node["index"]!);
        var aToB = edges.Single(edge => (int)edge["source"]! == indexMap["A"] && (int)edge["target"]! == indexMap["B"]);
        var bToA = edges.Single(edge => (int)edge["source"]! == indexMap["B"] && (int)edge["target"]! == indexMap["A"]);
        var bToC = edges.Single(edge => (int)edge["source"]! == indexMap["B"] && (int)edge["target"]! == indexMap["C"]);

        Assert.Equal(1, (int)aToB["group"]!);
        Assert.Equal(1, (int)bToA["group"]!);
        Assert.Equal(-1, (int)bToC["group"]!);
    }

    [Fact]
    public void Build_WithoutPartitions_DefaultsGroupsToZero()
    {
        var orderedVertices = new List<PSVertex>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var matrix = new double[,]
        {
            { 0, 1, 0 },
            { 0, 0, 1 },
            { 0, 0, 0 }
        };

        var json = _builder.Build(orderedVertices, (row, column) => matrix[row, column]);

        var nodes = (JArray)json["nodes"]!["values"]!;
        Assert.All(nodes, node => Assert.Equal(0, (int)node["group"]!));
    }
}