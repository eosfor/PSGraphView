namespace PSGraphView.Sfdp;

public sealed record SfdpBoundingBox(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

public sealed record SfdpComponentLayout(
    int ComponentId,
    int[] NodeIndices,
    double[] X,
    double[] Y);

public sealed record SfdpPackedComponent(
    int ComponentId,
    double OffsetX,
    double OffsetY,
    SfdpBoundingBox Bounds);

internal enum SfdpPackingStrategy
{
    Empty,
    SingleComponent,
    DominantComponent,
    SpiralGrid
}

internal sealed record SfdpPackingComponentPlacement(
    int ComponentId,
    int PlacementIndex,
    int NodeCount,
    SfdpBoundingBox SourceBounds,
    SfdpPackedComponent PackedComponent);

internal sealed record SfdpPackingResult(
    SfdpPackingStrategy Strategy,
    double Gap,
    double Margin,
    double MaxRowWidth,
    int Step,
    SfdpBoundingBox PackedBounds,
    IReadOnlyList<SfdpPackingComponentPlacement> Components);

public sealed class SfdpPackingOptions
{
    public double ComponentGap { get; init; } = 40.0;
    public double MaxRowWidth { get; init; } = 1600.0;
}

public static class SfdpComponentPacker
{
    public static SfdpBoundingBox ComputeBounds(
        double[] x,
        double[] y,
        IReadOnlyList<int> nodeIndices,
        double nodeRadius)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(nodeIndices);

        if (nodeIndices.Count == 0)
        {
            return new SfdpBoundingBox(0, 0, 0, 0);
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var nodeIndex in nodeIndices)
        {
            minX = Math.Min(minX, x[nodeIndex] - nodeRadius);
            minY = Math.Min(minY, y[nodeIndex] - nodeRadius);
            maxX = Math.Max(maxX, x[nodeIndex] + nodeRadius);
            maxY = Math.Max(maxY, y[nodeIndex] + nodeRadius);
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }

    public static IReadOnlyList<SfdpPackedComponent> Pack(
        IReadOnlyList<SfdpComponentLayout> componentLayouts,
        double nodeRadius,
        SfdpPackingOptions? options = null)
        => PackDetailed(componentLayouts, nodeRadius, options)
            .Components
            .Select(static component => component.PackedComponent)
            .ToArray();

    internal static SfdpPackingResult PackDetailed(
        IReadOnlyList<SfdpComponentLayout> componentLayouts,
        double nodeRadius,
        SfdpPackingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(componentLayouts);
        options ??= new SfdpPackingOptions();

        var preparedLayouts = componentLayouts
            .OrderByDescending(static layout => layout.NodeIndices.Length)
            .ThenBy(static layout => layout.ComponentId)
            .Select(layout =>
            {
                var localIndices = Enumerable.Range(0, layout.NodeIndices.Length).ToArray();
                var bounds = ComputeBounds(layout.X, layout.Y, localIndices, nodeRadius);
                return new PreparedComponentLayout(
                    layout,
                    bounds,
                    Math.Max(bounds.Width, nodeRadius * 2.0),
                    Math.Max(bounds.Height, nodeRadius * 2.0));
            })
            .ToArray();

        if (preparedLayouts.Length == 0)
        {
            return new SfdpPackingResult(
                SfdpPackingStrategy.Empty,
                options.ComponentGap,
                0.0,
                options.MaxRowWidth,
                0,
                new SfdpBoundingBox(0.0, 0.0, 0.0, 0.0),
                []);
        }

        if (preparedLayouts.Length == 1)
        {
            var layout = preparedLayouts[0];
            var packedComponents =
                new[]
                {
                new SfdpPackedComponent(
                    layout.Layout.ComponentId,
                    -layout.Bounds.MinX,
                    -layout.Bounds.MinY,
                    ShiftBounds(layout.Bounds, -layout.Bounds.MinX, -layout.Bounds.MinY))
                };

            return CreatePackingResult(
                preparedLayouts,
                packedComponents,
                SfdpPackingStrategy.SingleComponent,
                options.ComponentGap,
                0.0,
                options.MaxRowWidth,
                0);
        }

        if (ShouldPackAroundDominantComponent(preparedLayouts))
        {
            return PackAroundDominantComponent(preparedLayouts, options);
        }

        return PackBySpiralGrid(preparedLayouts, options);
    }

    private static bool ShouldPackAroundDominantComponent(IReadOnlyList<PreparedComponentLayout> layouts)
    {
        if (layouts.Count < 3)
        {
            return false;
        }

        var dominant = layouts[0].Layout.NodeIndices.Length;
        var second = layouts[1].Layout.NodeIndices.Length;
        return dominant >= 100 && second * 10 <= dominant;
    }

    private static SfdpPackingResult PackAroundDominantComponent(
        IReadOnlyList<PreparedComponentLayout> layouts,
        SfdpPackingOptions options)
    {
        var margin = Math.Max(options.ComponentGap / 2.0, 1.0);
        var dominant = layouts[0];
        var remainder = layouts.Skip(1).ToArray();
        var step = remainder.Length > 0 ? ComputeStep(remainder, margin) : 1;
        var occupiedCells = new HashSet<long>();
        var packed = new List<SfdpPackedComponent>(layouts.Count);

        var dominantGridWidth = Grid(dominant.Bounds.Width + (2.0 * margin), step);
        var dominantGridHeight = Grid(dominant.Bounds.Height + (2.0 * margin), step);
        var dominantGridX = -dominantGridWidth / 2;
        var dominantGridY = -dominantGridHeight / 2;
        if (!TryPlace(dominant, dominantGridWidth, dominantGridHeight, dominantGridX, dominantGridY, step, margin, occupiedCells, out var dominantPlacement))
        {
            throw new InvalidOperationException("Failed to place dominant component during prism packing.");
        }

        packed.Add(dominantPlacement);

        for (var i = 0; i < remainder.Length; i++)
        {
            var layout = remainder[i];
            var placement = FindPlacement(i + 1, layout, step, margin, occupiedCells);
            packed.Add(placement);
        }

        return CreatePackingResult(
            layouts,
            NormalizePackedBounds(packed),
            SfdpPackingStrategy.DominantComponent,
            options.ComponentGap,
            margin,
            options.MaxRowWidth,
            step);
    }

    private static SfdpPackingResult PackBySpiralGrid(
        IReadOnlyList<PreparedComponentLayout> layouts,
        SfdpPackingOptions options)
    {
        var margin = Math.Max(options.ComponentGap / 2.0, 1.0);
        var step = ComputeStep(layouts, margin);
        var occupiedCells = new HashSet<long>();
        var packed = new List<SfdpPackedComponent>(layouts.Count);

        for (var i = 0; i < layouts.Count; i++)
        {
            var layout = layouts[i];
            var placement = FindPlacement(i, layout, step, margin, occupiedCells);
            packed.Add(placement);
        }

        return CreatePackingResult(
            layouts,
            NormalizePackedBounds(packed),
            SfdpPackingStrategy.SpiralGrid,
            options.ComponentGap,
            margin,
            options.MaxRowWidth,
            step);
    }

    private static SfdpPackedComponent FindPlacement(
        int index,
        PreparedComponentLayout layout,
        int step,
        double margin,
        HashSet<long> occupiedCells)
    {
        var gridWidth = Grid(layout.Bounds.Width + (2.0 * margin), step);
        var gridHeight = Grid(layout.Bounds.Height + (2.0 * margin), step);

        if (index == 0)
        {
            var centeredX = -gridWidth / 2;
            var centeredY = -gridHeight / 2;
            if (TryPlace(layout, gridWidth, gridHeight, centeredX, centeredY, step, margin, occupiedCells, out var centeredPlacement))
            {
                return centeredPlacement;
            }
        }

        if (TryPlace(layout, gridWidth, gridHeight, 0, 0, step, margin, occupiedCells, out var originPlacement))
        {
            return originPlacement;
        }

        if (gridWidth >= gridHeight)
        {
            for (var bound = 1; ; bound++)
            {
                var x = 0;
                var y = -bound;
                for (; x < bound; x++)
                {
                    if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; y < bound; y++)
                {
                    if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; x > -bound; x--)
                {
                    if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; y > -bound; y--)
                {
                    if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; x < 0; x++)
                {
                    if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }
            }
        }

        for (var bound = 1; ; bound++)
        {
            var y = 0;
            var x = -bound;
            for (; y > -bound; y--)
            {
                if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; x < bound; x++)
            {
                if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; y < bound; y++)
            {
                if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; x > -bound; x--)
            {
                if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; y > 0; y--)
            {
                if (TryPlace(layout, gridWidth, gridHeight, x, y, step, margin, occupiedCells, out var placement))
                {
                    return placement;
                }
            }
        }
    }

    private static bool TryPlace(
        PreparedComponentLayout layout,
        int gridWidth,
        int gridHeight,
        int gridX,
        int gridY,
        int step,
        double margin,
        HashSet<long> occupiedCells,
        out SfdpPackedComponent placement)
    {
        for (var x = 0; x < gridWidth; x++)
        {
            for (var y = 0; y < gridHeight; y++)
            {
                if (occupiedCells.Contains(ToCellKey(gridX + x, gridY + y)))
                {
                    placement = default!;
                    return false;
                }
            }
        }

        for (var x = 0; x < gridWidth; x++)
        {
            for (var y = 0; y < gridHeight; y++)
            {
                occupiedCells.Add(ToCellKey(gridX + x, gridY + y));
            }
        }

        var offsetX = step * gridX + margin - layout.Bounds.MinX;
        var offsetY = step * gridY + margin - layout.Bounds.MinY;
        placement = new SfdpPackedComponent(
            layout.Layout.ComponentId,
            offsetX,
            offsetY,
            ShiftBounds(layout.Bounds, offsetX, offsetY));
        return true;
    }

    private static int Grid(double size, int step)
        => Math.Max(1, (int)Math.Ceiling(size / step));

    private static int ComputeStep(IReadOnlyList<PreparedComponentLayout> layouts, double margin)
    {
        const double c = 100.0;
        var a = c * layouts.Count - 1.0;
        var b = 0.0;
        var quadraticC = 0.0;

        foreach (var layout in layouts)
        {
            var width = layout.Bounds.Width + 2.0 * margin;
            var height = layout.Bounds.Height + 2.0 * margin;
            b -= width + height;
            quadraticC -= width * height;
        }

        var discriminant = (b * b) - (4.0 * a * quadraticC);
        var root = (-b + Math.Sqrt(discriminant)) / (2.0 * a);
        return Math.Max(1, (int)root);
    }

    private static IReadOnlyList<SfdpPackedComponent> NormalizePackedBounds(IReadOnlyList<SfdpPackedComponent> packed)
    {
        var minX = packed.Min(static component => component.Bounds.MinX);
        var minY = packed.Min(static component => component.Bounds.MinY);

        if (minX >= 0.0 && minY >= 0.0)
        {
            return packed;
        }

        var normalized = new List<SfdpPackedComponent>(packed.Count);
        foreach (var component in packed)
        {
            var offsetX = component.OffsetX - minX;
            var offsetY = component.OffsetY - minY;
            normalized.Add(new SfdpPackedComponent(
                component.ComponentId,
                offsetX,
                offsetY,
                ShiftBounds(component.Bounds, -minX, -minY)));
        }

        return normalized;
    }

    private static SfdpBoundingBox ShiftBounds(SfdpBoundingBox bounds, double offsetX, double offsetY)
        => new(
            bounds.MinX + offsetX,
            bounds.MinY + offsetY,
            bounds.MaxX + offsetX,
            bounds.MaxY + offsetY);

    private static long ToCellKey(int x, int y)
        => ((long)x << 32) ^ (uint)y;

    private static SfdpPackingResult CreatePackingResult(
        IReadOnlyList<PreparedComponentLayout> preparedLayouts,
        IReadOnlyList<SfdpPackedComponent> packedComponents,
        SfdpPackingStrategy strategy,
        double gap,
        double margin,
        double maxRowWidth,
        int step)
    {
        var preparedByComponentId = preparedLayouts.ToDictionary(static layout => layout.Layout.ComponentId);
        var placements = new List<SfdpPackingComponentPlacement>(packedComponents.Count);
        for (var i = 0; i < packedComponents.Count; i++)
        {
            var packedComponent = packedComponents[i];
            var prepared = preparedByComponentId[packedComponent.ComponentId];
            placements.Add(new SfdpPackingComponentPlacement(
                packedComponent.ComponentId,
                i,
                prepared.Layout.NodeIndices.Length,
                prepared.Bounds,
                packedComponent));
        }

        return new SfdpPackingResult(
            strategy,
            gap,
            margin,
            maxRowWidth,
            step,
            ComputePackedBounds(packedComponents),
            placements);
    }

    private static SfdpBoundingBox ComputePackedBounds(IReadOnlyList<SfdpPackedComponent> packedComponents)
    {
        if (packedComponents.Count == 0)
        {
            return new SfdpBoundingBox(0.0, 0.0, 0.0, 0.0);
        }

        return new SfdpBoundingBox(
            packedComponents.Min(static component => component.Bounds.MinX),
            packedComponents.Min(static component => component.Bounds.MinY),
            packedComponents.Max(static component => component.Bounds.MaxX),
            packedComponents.Max(static component => component.Bounds.MaxY));
    }

    private sealed record PreparedComponentLayout(
        SfdpComponentLayout Layout,
        SfdpBoundingBox Bounds,
        double Width,
        double Height);
}
