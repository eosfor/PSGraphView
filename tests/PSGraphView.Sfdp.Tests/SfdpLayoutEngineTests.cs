using System.Text.Json;
using PSGraph.Model;
using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpLayoutEngineTests
{
    private readonly SfdpLayoutEngine _engine = new();

    [Fact]
    public void Layout_WithSeed_IsDeterministic()
    {
        var graph = CreateCycleGraph();

        var first = _engine.Layout(graph, new SfdpOptions { Seed = 123, MaxIterations = 50 });
        var second = _engine.Layout(graph, new SfdpOptions { Seed = 123, MaxIterations = 50 });

        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
    }

    [Fact]
    public void Layout_ForConnectedGraph_ProducesNonDegenerateBounds()
    {
        var graph = CreateCycleGraph();

        var result = _engine.Layout(graph, new SfdpOptions { Seed = 7, MaxIterations = 50 });

        Assert.True(result.Bounds.Width > 0.0);
        Assert.True(result.Bounds.Height > 0.0);
        Assert.Equal(1, result.ComponentCount);
    }

    [Fact]
    public void Layout_ForDisconnectedGraph_PacksComponentsSeparately()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>()),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>()),
                new GraphViewNode("D", "D", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("C", "D", null, 1)
            ]);

        var result = _engine.Layout(graph, new SfdpOptions { Seed = 5, MaxIterations = 50, ComponentGap = 25.0 });

        Assert.Equal(2, result.ComponentCount);
        Assert.True(Math.Abs(result.X[0] - result.X[2]) >= 25.0 || Math.Abs(result.Y[0] - result.Y[2]) >= 25.0);
    }

    [Fact]
    public void Layout_WithSpringSmoothing_RemainsDeterministic()
    {
        var graph = CreateCycleGraph();

        var first = _engine.Layout(graph, new SfdpOptions
        {
            Seed = 17,
            MaxIterations = 40,
            Smoothing = SfdpSmoothingMode.Spring,
            SmoothingIterations = 12
        });
        var second = _engine.Layout(graph, new SfdpOptions
        {
            Seed = 17,
            MaxIterations = 40,
            Smoothing = SfdpSmoothingMode.Spring,
            SmoothingIterations = 12
        });

        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
    }

    [Theory]
    [InlineData(SfdpSmoothingMode.GraphDistance)]
    [InlineData(SfdpSmoothingMode.AverageDistance)]
    [InlineData(SfdpSmoothingMode.PowerDistance)]
    public void Layout_WithStressSmoothingModes_ProducesStableBounds(SfdpSmoothingMode mode)
    {
        var graph = CreateCycleGraph();

        var result = _engine.Layout(graph, new SfdpOptions
        {
            Seed = 23,
            MaxIterations = 40,
            Smoothing = mode,
            SmoothingIterations = 8
        });

        Assert.True(result.Bounds.Width > 0.0);
        Assert.True(result.Bounds.Height > 0.0);
    }

    [Fact]
    public void Layout_WithDiagnostics_WritesTraceFile()
    {
        var graph = CreateCycleGraph();
        var diagnosticsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}-sfdp.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 29,
                MaxIterations = 25,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            Assert.True(File.Exists(diagnosticsPath));
            var diagnostics = File.ReadAllText(diagnosticsPath);
            Assert.Contains("\"Phase\":\"layout\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"Name\":\"start\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("\"Name\":\"finish\"", diagnostics, StringComparison.Ordinal);
            Assert.Contains("terminationReason", diagnostics, StringComparison.Ordinal);
            Assert.Contains("repulsiveExponentSource", diagnostics, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_ForDisconnectedGraph_WithDiagnostics_WritesPackingEvents()
    {
        var graph = new GraphView(
            [
                new GraphViewNode("A", "A", null, new Dictionary<string, object?>()),
                new GraphViewNode("B", "B", null, new Dictionary<string, object?>()),
                new GraphViewNode("C", "C", null, new Dictionary<string, object?>()),
                new GraphViewNode("D", "D", null, new Dictionary<string, object?>())
            ],
            [
                new GraphViewEdge("A", "B", null, 1),
                new GraphViewEdge("C", "D", null, 1)
            ]);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-packing.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 31,
                MaxIterations = 25,
                ComponentGap = 25.0,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var packingStartSeen = false;
            var packingFinishSeen = false;
            var componentEvents = 0;

            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !string.Equals(phase.GetString(), "packing", StringComparison.Ordinal) ||
                    !root.TryGetProperty("Name", out var name))
                {
                    continue;
                }

                switch (name.GetString())
                {
                    case "start":
                        packingStartSeen = true;
                        Assert.Equal(2, root.GetProperty("Data").GetProperty("componentCount").GetInt32());
                        Assert.True(root.GetProperty("Data").GetProperty("scale").GetDouble() > 1.0);
                        Assert.True(root.GetProperty("Data").GetProperty("step").GetInt32() > 0);
                        break;
                    case "component":
                        componentEvents++;
                        Assert.True(root.GetProperty("Data").GetProperty("componentNodeCount").GetInt32() > 0);
                        Assert.True(root.GetProperty("Data").GetProperty("cellCount").GetInt32() > 0);
                        Assert.True(root.GetProperty("Data").GetProperty("gridWidth").GetInt32() > 0);
                        Assert.True(root.GetProperty("Data").GetProperty("gridHeight").GetInt32() > 0);
                        Assert.True(root.GetProperty("Data").GetProperty("perimeter").GetInt32() > 0);
                        Assert.True(root.GetProperty("Data").GetProperty("packedWidth").GetDouble() > 0.0);
                        break;
                    case "finish":
                        packingFinishSeen = true;
                        Assert.True(root.GetProperty("Data").GetProperty("scale").GetDouble() > 1.0);
                        Assert.True(root.GetProperty("Data").GetProperty("packedWidth").GetDouble() > 0.0);
                        Assert.True(root.GetProperty("Data").GetProperty("packedHeight").GetDouble() > 0.0);
                        break;
                }
            }

            Assert.True(packingStartSeen);
            Assert.True(packingFinishSeen);
            Assert.Equal(2, componentEvents);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithMultilevelDiagnostics_PropagatesNaturalLengthUsingPreviousLevel()
    {
        var graph = CreateCycleGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-multilevel.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 37,
                MaxIterations = 20,
                RefinementIterations = 5,
                MultilevelThreshold = 8,
                MaxMultilevelDepth = 4,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var prolongations = new List<(int Level, double Previous, double Expected, double Actual)>();
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                if (!string.Equals(phase.GetString(), "multilevel", StringComparison.Ordinal) ||
                    !string.Equals(name.GetString(), "prolongate", StringComparison.Ordinal))
                {
                    continue;
                }

                prolongations.Add((
                    root.GetProperty("Level").GetInt32(),
                    data.GetProperty("previousNaturalLength").GetDouble(),
                    data.GetProperty("expectedGraphvizNaturalLength").GetDouble(),
                    data.GetProperty("actualManagedNaturalLength").GetDouble()));
            }

            Assert.NotEmpty(prolongations);

            foreach (var prolongation in prolongations)
            {
                Assert.Equal(prolongation.Previous * 0.75, prolongation.Expected, 12);
                Assert.Equal(prolongation.Expected, prolongation.Actual, 12);
            }

            for (var i = 1; i < prolongations.Count; i++)
            {
                Assert.True(prolongations[i - 1].Actual > prolongations[i].Actual);
            }
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithMultilevelDiagnostics_UsesComponentLevelRepulsiveExponentAcrossLevels()
    {
        var graph = CreateStarGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-repulsive.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 41,
                MaxIterations = 20,
                RefinementIterations = 5,
                MultilevelThreshold = 8,
                MaxMultilevelDepth = 4,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            double? componentRepulsiveExponent = null;
            string? componentRepulsiveExponentSource = null;
            var singleLevelControls = new List<(double RepulsiveExponent, string Source)>();

            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                var phaseName = phase.GetString();
                var eventName = name.GetString();
                if (string.Equals(phaseName, "component", StringComparison.Ordinal) &&
                    string.Equals(eventName, "control", StringComparison.Ordinal))
                {
                    componentRepulsiveExponent = data.GetProperty("repulsiveExponent").GetDouble();
                    componentRepulsiveExponentSource = data.GetProperty("repulsiveExponentSource").GetString();
                    continue;
                }

                if (!string.Equals(phaseName, "singlelevel", StringComparison.Ordinal) ||
                    !string.Equals(eventName, "control", StringComparison.Ordinal))
                {
                    continue;
                }

                singleLevelControls.Add((
                    data.GetProperty("repulsiveExponent").GetDouble(),
                    data.GetProperty("repulsiveExponentSource").GetString()!));
            }

            Assert.Equal(-1.8, componentRepulsiveExponent);
            Assert.Equal("component_auto", componentRepulsiveExponentSource);
            Assert.True(singleLevelControls.Count > 1);
            Assert.All(singleLevelControls, control =>
            {
                Assert.Equal(componentRepulsiveExponent, control.RepulsiveExponent);
                Assert.Equal("component_auto", control.Source);
            });
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithDefaultMultilevelDepth_DoesNotStopAtArtificialMaxDepth()
    {
        var graph = CreateCycleGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-depth-default.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 43,
                MaxIterations = 20,
                RefinementIterations = 5,
                MultilevelThreshold = 8,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var sawMaxDepth = false;
            var sawThreshold = false;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                if (!string.Equals(phase.GetString(), "multilevel", StringComparison.Ordinal) ||
                    !string.Equals(name.GetString(), "base_case", StringComparison.Ordinal))
                {
                    continue;
                }

                var reason = data.GetProperty("reason").GetString();
                sawMaxDepth |= string.Equals(reason, "max_depth", StringComparison.Ordinal);
                sawThreshold |= string.Equals(reason, "threshold", StringComparison.Ordinal);
            }

            Assert.False(sawMaxDepth);
            Assert.True(sawThreshold);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithExplicitMultilevelDepth_StillStopsAtRequestedMaxDepth()
    {
        var graph = CreateCycleGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-depth-explicit.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 47,
                MaxIterations = 20,
                RefinementIterations = 5,
                MultilevelThreshold = 8,
                MaxMultilevelDepth = 4,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var sawMaxDepth = false;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                if (!string.Equals(phase.GetString(), "multilevel", StringComparison.Ordinal) ||
                    !string.Equals(name.GetString(), "base_case", StringComparison.Ordinal))
                {
                    continue;
                }

                sawMaxDepth |= string.Equals(data.GetProperty("reason").GetString(), "max_depth", StringComparison.Ordinal);
            }

            Assert.True(sawMaxDepth);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithDefaultMultilevelThreshold_DoesNotStopAtLegacyThreshold()
    {
        var graph = CreateCycleGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-threshold-default.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 53,
                MaxIterations = 20,
                RefinementIterations = 5,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var sawThreshold = false;
            var sawCoarseningNotSmaller = false;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                if (!string.Equals(phase.GetString(), "multilevel", StringComparison.Ordinal) ||
                    !string.Equals(name.GetString(), "base_case", StringComparison.Ordinal))
                {
                    continue;
                }

                var reason = data.GetProperty("reason").GetString();
                sawThreshold |= string.Equals(reason, "threshold", StringComparison.Ordinal);
                sawCoarseningNotSmaller |= string.Equals(reason, "coarsening_not_smaller", StringComparison.Ordinal);
            }

            Assert.False(sawThreshold);
            Assert.True(sawCoarseningNotSmaller);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithExplicitMultilevelThreshold_StillStopsAtRequestedThreshold()
    {
        var graph = CreateCycleGraph(96);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-threshold-explicit.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 59,
                MaxIterations = 20,
                RefinementIterations = 5,
                MultilevelThreshold = 64,
                EnableOverlapRemoval = false,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = false
                }
            });

            var sawThreshold = false;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("Phase", out var phase) ||
                    !root.TryGetProperty("Name", out var name) ||
                    !root.TryGetProperty("Data", out var data))
                {
                    continue;
                }

                if (!string.Equals(phase.GetString(), "multilevel", StringComparison.Ordinal) ||
                    !string.Equals(name.GetString(), "base_case", StringComparison.Ordinal))
                {
                    continue;
                }

                sawThreshold |= string.Equals(data.GetProperty("reason").GetString(), "threshold", StringComparison.Ordinal);
            }

            Assert.True(sawThreshold);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithHybridQuadtree_ResolvesToNormalBelowHybridThreshold()
    {
        var graph = CreateCycleGraph(60);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-quadtree-normal.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 61,
                MaxIterations = 5,
                EnableMultilevel = false,
                EnableOverlapRemoval = false,
                QuadtreeMode = SfdpQuadtreeMode.Hybrid,
                BarnesHutThreshold = 45,
                QuadtreeHybridThreshold = 100,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = true
                }
            });

            string? effectiveMode = null;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!string.Equals(root.GetProperty("Phase").GetString(), "singlelevel", StringComparison.Ordinal) ||
                    !string.Equals(root.GetProperty("Name").GetString(), "control", StringComparison.Ordinal))
                {
                    continue;
                }

                effectiveMode = root.GetProperty("Data").GetProperty("effectiveQuadtreeMode").GetString();
                break;
            }

            Assert.Equal(nameof(SfdpQuadtreeMode.Normal), effectiveMode);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithHybridQuadtree_ResolvesToFastAboveHybridThreshold()
    {
        var graph = CreateCycleGraph(60);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-quadtree-fast.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 67,
                MaxIterations = 5,
                EnableMultilevel = false,
                EnableOverlapRemoval = false,
                QuadtreeMode = SfdpQuadtreeMode.Hybrid,
                BarnesHutThreshold = 45,
                QuadtreeHybridThreshold = 50,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = true
                }
            });

            string? effectiveMode = null;
            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!string.Equals(root.GetProperty("Phase").GetString(), "singlelevel", StringComparison.Ordinal) ||
                    !string.Equals(root.GetProperty("Name").GetString(), "control", StringComparison.Ordinal))
                {
                    continue;
                }

                effectiveMode = root.GetProperty("Data").GetProperty("effectiveQuadtreeMode").GetString();
                break;
            }

            Assert.Equal(nameof(SfdpQuadtreeMode.Fast), effectiveMode);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    [Fact]
    public void Layout_WithNormalQuadtree_ReportsSupernodeDiagnosticsAndAdjustsQuadtreeLevel()
    {
        var graph = CreateCycleGraph(80);
        var diagnosticsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-quadtree-supernodes.jsonl");

        try
        {
            _engine.Layout(graph, new SfdpOptions
            {
                Seed = 71,
                MaxIterations = 5,
                EnableMultilevel = false,
                EnableOverlapRemoval = false,
                QuadtreeMode = SfdpQuadtreeMode.Normal,
                BarnesHutThreshold = 45,
                QuadtreeMaxDepth = 10,
                Diagnostics = new SfdpDiagnosticsOptions
                {
                    Path = diagnosticsPath,
                    IncludeIterations = true
                }
            });

            var quadtreeLevels = new List<int>();
            var sawSupernodeStats = false;

            foreach (var line in File.ReadLines(diagnosticsPath))
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!string.Equals(root.GetProperty("Phase").GetString(), "singlelevel", StringComparison.Ordinal) ||
                    !string.Equals(root.GetProperty("Name").GetString(), "iter", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = root.GetProperty("Data");
                if (!string.Equals(data.GetProperty("effectiveQuadtreeMode").GetString(), nameof(SfdpQuadtreeMode.Normal), StringComparison.Ordinal))
                {
                    continue;
                }

                if (data.TryGetProperty("quadtreeLevel", out var quadtreeLevel) && quadtreeLevel.ValueKind == JsonValueKind.Number)
                {
                    quadtreeLevels.Add(quadtreeLevel.GetInt32());
                }

                if (data.TryGetProperty("nsuperAverage", out var nsuperAverage) &&
                    data.TryGetProperty("countsAverage", out var countsAverage) &&
                    nsuperAverage.ValueKind == JsonValueKind.Number &&
                    countsAverage.ValueKind == JsonValueKind.Number &&
                    nsuperAverage.GetDouble() > 0.0 &&
                    countsAverage.GetDouble() > 0.0)
                {
                    sawSupernodeStats = true;
                }
            }

            Assert.True(sawSupernodeStats);
            Assert.True(quadtreeLevels.Count >= 2);
            Assert.Equal(10, quadtreeLevels[0]);
            Assert.Contains(11, quadtreeLevels);
        }
        finally
        {
            if (File.Exists(diagnosticsPath))
            {
                File.Delete(diagnosticsPath);
            }
        }
    }

    private static GraphView CreateCycleGraph(int nodeCount = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeCount, 3);

        var nodes = new List<GraphViewNode>(nodeCount);
        var edges = new List<GraphViewEdge>(nodeCount);
        for (var i = 0; i < nodeCount; i++)
        {
            var id = $"N{i}";
            nodes.Add(new GraphViewNode(id, id, null, new Dictionary<string, object?>()));
            var nextId = $"N{(i + 1) % nodeCount}";
            edges.Add(new GraphViewEdge(id, nextId, null, 1));
        }

        return new GraphView(
            nodes,
            edges);
    }

    private static GraphView CreateStarGraph(int nodeCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeCount, 4);

        var nodes = new List<GraphViewNode>(nodeCount);
        var edges = new List<GraphViewEdge>(nodeCount - 1);
        for (var i = 0; i < nodeCount; i++)
        {
            var id = $"S{i}";
            nodes.Add(new GraphViewNode(id, id, null, new Dictionary<string, object?>()));
            if (i == 0)
            {
                continue;
            }

            edges.Add(new GraphViewEdge("S0", id, null, 1));
        }

        return new GraphView(nodes, edges);
    }
}
