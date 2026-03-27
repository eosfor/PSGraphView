using Newtonsoft.Json.Linq;
using PSGraph.Model;

namespace PSGraphView.Vega;

public sealed class DsmNodeAndEdgeViewBuilder
{
    public JObject Build(
        IReadOnlyList<PSVertex> orderedVertices,
        Func<int, int, double> getMatrixValue,
        IReadOnlyList<IReadOnlyList<PSVertex>>? partitions = null)
    {
        ArgumentNullException.ThrowIfNull(orderedVertices);
        ArgumentNullException.ThrowIfNull(getMatrixValue);

        var nodeValues = new JArray();
        var edgeValues = new JArray();
        var vertexToGroup = BuildVertexGroupMap(partitions);

        for (var i = 0; i < orderedVertices.Count; i++)
        {
            var vertex = orderedVertices[i];
            nodeValues.Add(new JObject
            {
                ["name"] = vertex.ToString(),
                ["index"] = i,
                ["group"] = vertexToGroup.TryGetValue(vertex, out var group) ? group : 0
            });
        }

        for (var row = 0; row < orderedVertices.Count; row++)
        {
            for (var column = 0; column < orderedVertices.Count; column++)
            {
                if (getMatrixValue(row, column) == 0)
                {
                    continue;
                }

                var sourceGroup = (int)nodeValues[row]!["group"]!;
                var targetGroup = (int)nodeValues[column]!["group"]!;
                var edgeGroup = sourceGroup == targetGroup ? sourceGroup : -1;

                edgeValues.Add(new JObject
                {
                    ["source"] = row,
                    ["target"] = column,
                    ["group"] = edgeGroup,
                    ["x"] = row,
                    ["y"] = column
                });
            }
        }

        return new JObject
        {
            ["nodes"] = new JObject { ["values"] = nodeValues },
            ["edges"] = new JObject { ["values"] = edgeValues }
        };
    }

    private static Dictionary<PSVertex, int> BuildVertexGroupMap(IReadOnlyList<IReadOnlyList<PSVertex>>? partitions)
    {
        var vertexToGroup = new Dictionary<PSVertex, int>();
        if (partitions is null)
        {
            return vertexToGroup;
        }

        for (var groupIndex = 0; groupIndex < partitions.Count; groupIndex++)
        {
            foreach (var vertex in partitions[groupIndex])
            {
                vertexToGroup[vertex] = groupIndex + 1;
            }
        }

        return vertexToGroup;
    }
}