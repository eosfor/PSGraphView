using PSGraph.Model;

namespace PSGraphView.Sfdp;

public sealed class SfdpLayoutEngine
{
    private readonly SfdpMultilevelLayouter _multilevelLayouter = new();

    public SfdpLayoutResult Layout(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new SfdpOptions();

        using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
        return LayoutCore(graph, options, diagnostics, cancellationToken);
    }

    internal SfdpLayoutResult Layout(
        GraphView graph,
        SfdpOptions options,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return LayoutCore(graph, options, diagnostics, cancellationToken);
    }

    private SfdpLayoutResult LayoutCore(
        GraphView graph,
        SfdpOptions options,
        SfdpDiagnosticsWriter diagnostics,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        cancellationToken.ThrowIfCancellationRequested();

        var indexedGraph = SfdpGraphBuilder.BuildIndexed(graph);
        var csrGraph = SfdpGraphBuilder.BuildUndirectedCsr(indexedGraph);
        var components = SfdpConnectedComponents.Find(csrGraph);

        diagnostics.Write("layout", "start",
        [
            ("nodeCount", indexedGraph.NodeCount),
            ("edgeCount", graph.Edges.Count),
            ("componentCount", components.ComponentCount),
            ("seed", options.Seed),
            ("maxIterations", options.MaxIterations),
            ("initialStep", options.InitialStep),
            ("tolerance", options.Tolerance),
            ("adaptiveCooling", options.AdaptiveCooling),
            ("useBarnesHut", options.UseBarnesHut),
            ("quadtreeMode", options.QuadtreeMode),
            ("barnesHutThreshold", options.BarnesHutThreshold),
            ("barnesHutTheta", options.BarnesHutTheta),
            ("quadtreeHybridThreshold", options.QuadtreeHybridThreshold),
            ("quadtreeMaxDepth", options.QuadtreeMaxDepth),
            ("enableMultilevel", options.EnableMultilevel),
            ("multilevelThreshold", options.MultilevelThreshold),
            ("maxMultilevelDepth", options.MaxMultilevelDepth),
            ("refinementIterations", options.RefinementIterations),
            ("naturalLength", options.NaturalLength),
            ("repulsiveExponent", options.RepulsiveExponent),
            ("smoothing", options.Smoothing),
            ("smoothingIterations", options.SmoothingIterations),
            ("applyPrincipalComponentRotation", options.ApplyPrincipalComponentRotation),
            ("rotationDegrees", options.RotationDegrees),
            ("enableOverlapRemoval", options.EnableOverlapRemoval),
            ("overlapRemovalIterations", options.OverlapRemovalIterations),
            ("overlapRemovalPadding", options.OverlapRemovalPadding),
            ("overlapRemovalBoxUnits", options.OverlapRemovalBoxUnits),
            ("overlapRemovalHalfWidth", options.OverlapRemovalHalfWidth),
            ("overlapRemovalHalfHeight", options.OverlapRemovalHalfHeight),
            ("nodeRadius", options.NodeRadius),
            ("componentGap", options.ComponentGap),
            ("width", options.Width),
            ("height", options.Height)
        ]);

        var x = new double[indexedGraph.NodeCount];
        var y = new double[indexedGraph.NodeCount];
        var componentLayouts = new List<SfdpComponentLayout>(components.ComponentCount);
        var random = options.Seed.HasValue ? new Random(options.Seed.Value) : null;

        for (var componentId = 0; componentId < components.Components.Count; componentId++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            componentLayouts.Add(_multilevelLayouter.LayoutComponent(
                componentId,
                components.Components[componentId],
                csrGraph,
                options,
                random,
                diagnostics,
                cancellationToken));
        }

        diagnostics.Write("layout", "geometry",
        [
            ("stage", "before_packing"),
            ("componentCount", componentLayouts.Count)
        ]);

        var packingOptions = new SfdpPackingOptions
        {
            ComponentGap = options.ComponentGap,
            MaxRowWidth = options.Width ?? 1600.0
        };
        var packingResult = SfdpComponentPacker.PackDetailed(
            componentLayouts,
            csrGraph,
            options.NodeRadius,
            packingOptions);

        diagnostics.Write("packing", "start",
        [
            ("componentCount", componentLayouts.Count),
            ("strategy", packingResult.Strategy),
            ("gap", packingResult.Gap),
            ("margin", packingResult.Margin),
            ("scale", packingResult.Scale),
            ("step", packingResult.Step),
            ("normalizationApplied", packingResult.NormalizationApplied),
            ("normalizeOffsetX", packingResult.NormalizeOffsetX),
            ("normalizeOffsetY", packingResult.NormalizeOffsetY),
            ("rawPackedWidth", packingResult.RawPackedBounds.Width),
            ("rawPackedHeight", packingResult.RawPackedBounds.Height),
            ("maxRowWidth", packingResult.MaxRowWidth)
        ]);

        foreach (var packedComponent in packingResult.Components)
        {
            diagnostics.Write("packing", "component", packedComponent.ComponentId,
            [
                ("componentIndex", packedComponent.PlacementIndex),
                ("componentNodeCount", packedComponent.NodeCount),
                ("componentWidth", packedComponent.SourceBounds.Width),
                ("componentHeight", packedComponent.SourceBounds.Height),
                ("componentWidthScaled", packedComponent.SourceWidthScaled),
                ("componentHeightScaled", packedComponent.SourceHeightScaled),
                ("roundedMinXScaled", packedComponent.RoundedMinXScaled),
                ("roundedMinYScaled", packedComponent.RoundedMinYScaled),
                ("cellCount", packedComponent.CellCount),
                ("gridWidth", packedComponent.GridWidth),
                ("gridHeight", packedComponent.GridHeight),
                ("perimeter", packedComponent.Perimeter),
                ("offsetX", packedComponent.PackedComponent.OffsetX),
                ("offsetY", packedComponent.PackedComponent.OffsetY),
                ("packedMinX", packedComponent.PackedComponent.Bounds.MinX),
                ("packedMinY", packedComponent.PackedComponent.Bounds.MinY),
                ("packedMaxX", packedComponent.PackedComponent.Bounds.MaxX),
                ("packedMaxY", packedComponent.PackedComponent.Bounds.MaxY),
                ("packedWidth", packedComponent.PackedComponent.Bounds.Width),
                ("packedHeight", packedComponent.PackedComponent.Bounds.Height),
                ("gap", packingResult.Gap)
            ]);
        }

        diagnostics.Write("packing", "finish",
        [
            ("componentCount", packingResult.Components.Count),
            ("strategy", packingResult.Strategy),
            ("step", packingResult.Step),
            ("gap", packingResult.Gap),
            ("margin", packingResult.Margin),
            ("scale", packingResult.Scale),
            ("normalizationApplied", packingResult.NormalizationApplied),
            ("normalizeOffsetX", packingResult.NormalizeOffsetX),
            ("normalizeOffsetY", packingResult.NormalizeOffsetY),
            ("rawPackedWidth", packingResult.RawPackedBounds.Width),
            ("rawPackedHeight", packingResult.RawPackedBounds.Height),
            ("packedWidth", packingResult.PackedBounds.Width),
            ("packedHeight", packingResult.PackedBounds.Height)
        ]);

        var packedComponents = packingResult.Components.Select(static component => component.PackedComponent).ToArray();

        foreach (var packed in packedComponents)
        {
            var layout = componentLayouts.First(component => component.ComponentId == packed.ComponentId);
            for (var i = 0; i < layout.NodeIndices.Length; i++)
            {
                var nodeIndex = layout.NodeIndices[i];
                x[nodeIndex] = layout.X[i] + packed.OffsetX;
                y[nodeIndex] = layout.Y[i] + packed.OffsetY;
            }
        }

        var packedGeometry = SfdpGeometrySummary.Create(csrGraph, x, y);
        diagnostics.Write("layout", "geometry",
        [
            ("stage", "after_packing"),
            ("componentCount", components.ComponentCount),
            ("minX", packedGeometry.MinX),
            ("minY", packedGeometry.MinY),
            ("maxX", packedGeometry.MaxX),
            ("maxY", packedGeometry.MaxY),
            ("width", packedGeometry.Width),
            ("height", packedGeometry.Height),
            ("diagonal", packedGeometry.Diagonal),
            ("averageEdgeLength", packedGeometry.AverageEdgeLength)
        ]);

        var bounds = SfdpComponentPacker.ComputeBounds(
            x,
            y,
            Enumerable.Range(0, indexedGraph.NodeCount).ToArray(),
            options.NodeRadius);
        var finishGeometry = SfdpGeometrySummary.Create(csrGraph, x, y);

        diagnostics.WriteWithCoordinates("layout", "finish", x, y,
            data:
            [
                ("componentCount", components.ComponentCount),
                ("minX", bounds.MinX),
                ("minY", bounds.MinY),
                ("maxX", bounds.MaxX),
                ("maxY", bounds.MaxY),
                ("width", bounds.Width),
                ("height", bounds.Height),
                ("diagonal", finishGeometry.Diagonal),
                ("averageEdgeLength", finishGeometry.AverageEdgeLength)
            ]);

        return new SfdpLayoutResult(x, y, bounds, components.ComponentCount);
    }
}
