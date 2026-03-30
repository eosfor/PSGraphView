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
    public double ComponentGap { get; init; } = 16.0;
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
        => PackDetailed(componentLayouts, null, nodeRadius, options)
            .Components
            .Select(static component => component.PackedComponent)
            .ToArray();

    internal static SfdpPackingResult PackDetailed(
        IReadOnlyList<SfdpComponentLayout> componentLayouts,
        SfdpCsrGraph? graph,
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

        return PackByPolyomino(preparedLayouts, graph, nodeRadius, options);
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

    private static SfdpPackingResult PackByPolyomino(
        IReadOnlyList<PreparedComponentLayout> layouts,
        SfdpCsrGraph? graph,
        double nodeRadius,
        SfdpPackingOptions options)
    {
        var margin = Math.Max(options.ComponentGap / 2.0, 0.0);
        var step = ComputeStep(layouts, margin);
        var occupiedCells = new HashSet<long>();
        var packed = new List<SfdpPackedComponent>(layouts.Count);
        var shapes = layouts.ToDictionary(
            static layout => layout.Layout.ComponentId,
            layout => BuildPackingShape(layout, graph, nodeRadius, step, margin));
        var orderedLayouts = layouts
            .OrderByDescending(layout => shapes[layout.Layout.ComponentId].Perimeter)
            .ThenBy(static layout => layout.Layout.ComponentId)
            .ToArray();

        for (var i = 0; i < orderedLayouts.Length; i++)
        {
            var layout = orderedLayouts[i];
            var shape = shapes[layout.Layout.ComponentId];
            packed.Add(FindPlacement(i, layout, shape, step, occupiedCells));
        }

        return CreatePackingResult(
            layouts,
            NormalizePackedBounds(packed),
            ShouldPackAroundDominantComponent(layouts)
                ? SfdpPackingStrategy.DominantComponent
                : SfdpPackingStrategy.SpiralGrid,
            options.ComponentGap,
            margin,
            options.MaxRowWidth,
            step);
    }

    private static SfdpPackedComponent FindPlacement(
        int index,
        PreparedComponentLayout layout,
        SfdpPackingShape shape,
        int step,
        HashSet<long> occupiedCells)
    {
        if (index == 0)
        {
            var centeredX = -shape.GridWidth / 2;
            var centeredY = -shape.GridHeight / 2;
            if (TryPlace(layout, shape, centeredX, centeredY, step, occupiedCells, out var centeredPlacement))
            {
                return centeredPlacement;
            }
        }

        if (TryPlace(layout, shape, 0, 0, step, occupiedCells, out var originPlacement))
        {
            return originPlacement;
        }

        if (shape.GridWidth >= shape.GridHeight)
        {
            for (var bound = 1; ; bound++)
            {
                var x = 0;
                var y = -bound;
                for (; x < bound; x++)
                {
                    if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; y < bound; y++)
                {
                    if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; x > -bound; x--)
                {
                    if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; y > -bound; y--)
                {
                    if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                    {
                        return placement;
                    }
                }

                for (; x < 0; x++)
                {
                    if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
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
                if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; x < bound; x++)
            {
                if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; y < bound; y++)
            {
                if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; x > -bound; x--)
            {
                if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                {
                    return placement;
                }
            }

            for (; y > 0; y--)
            {
                if (TryPlace(layout, shape, x, y, step, occupiedCells, out var placement))
                {
                    return placement;
                }
            }
        }
    }

    private static bool TryPlace(
        PreparedComponentLayout layout,
        SfdpPackingShape shape,
        int gridX,
        int gridY,
        int step,
        HashSet<long> occupiedCells,
        out SfdpPackedComponent placement)
    {
        foreach (var cell in shape.Cells)
        {
            if (occupiedCells.Contains(ToCellKey(gridX + cell.X, gridY + cell.Y)))
            {
                placement = default!;
                return false;
            }
        }

        foreach (var cell in shape.Cells)
        {
            occupiedCells.Add(ToCellKey(gridX + cell.X, gridY + cell.Y));
        }

        var offsetX = step * gridX - shape.RoundedMinX;
        var offsetY = step * gridY - shape.RoundedMinY;
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

    private static SfdpPackingShape BuildPackingShape(
        PreparedComponentLayout layout,
        SfdpCsrGraph? graph,
        double nodeRadius,
        int step,
        double margin)
    {
        var roundedMinX = Math.Round(layout.Bounds.MinX);
        var roundedMinY = Math.Round(layout.Bounds.MinY);
        var nodeHalfSize = Math.Max(0, (int)Math.Round(nodeRadius + margin));
        var cells = new HashSet<long>();

        for (var i = 0; i < layout.Layout.NodeIndices.Length; i++)
        {
            var centerX = (int)(Math.Round(layout.Layout.X[i]) - roundedMinX);
            var centerY = (int)(Math.Round(layout.Layout.Y[i]) - roundedMinY);
            var minCellX = CellValue(centerX - nodeHalfSize, step);
            var minCellY = CellValue(centerY - nodeHalfSize, step);
            var maxCellX = CellValue(centerX + nodeHalfSize, step);
            var maxCellY = CellValue(centerY + nodeHalfSize, step);

            for (var x = minCellX; x <= maxCellX; x++)
            {
                for (var y = minCellY; y <= maxCellY; y++)
                {
                    cells.Add(ToCellKey(x, y));
                }
            }
        }

        if (graph is not null)
        {
            AddEdgeCells(layout, graph, roundedMinX, roundedMinY, step, cells);
        }

        if (cells.Count == 0)
        {
            cells.Add(ToCellKey(0, 0));
        }

        return new SfdpPackingShape(
            Cells: cells.Select(static key => new SfdpPackingCell((int)(key >> 32), (int)key)).ToArray(),
            GridWidth: Grid(layout.Bounds.Width + (2.0 * margin), step),
            GridHeight: Grid(layout.Bounds.Height + (2.0 * margin), step),
            Perimeter: Grid(layout.Bounds.Width + (2.0 * margin), step) + Grid(layout.Bounds.Height + (2.0 * margin), step),
            RoundedMinX: roundedMinX,
            RoundedMinY: roundedMinY);
    }

    private static void AddEdgeCells(
        PreparedComponentLayout layout,
        SfdpCsrGraph graph,
        double roundedMinX,
        double roundedMinY,
        int step,
        HashSet<long> cells)
    {
        var localIndexByGlobal = new Dictionary<int, int>(layout.Layout.NodeIndices.Length);
        for (var i = 0; i < layout.Layout.NodeIndices.Length; i++)
        {
            localIndexByGlobal[layout.Layout.NodeIndices[i]] = i;
        }

        for (var localIndex = 0; localIndex < layout.Layout.NodeIndices.Length; localIndex++)
        {
            var globalIndex = layout.Layout.NodeIndices[localIndex];
            var start = graph.Offsets[globalIndex];
            var end = graph.Offsets[globalIndex + 1];
            var fromCell = new SfdpPackingCell(
                CellValue((int)(Math.Round(layout.Layout.X[localIndex]) - roundedMinX), step),
                CellValue((int)(Math.Round(layout.Layout.Y[localIndex]) - roundedMinY), step));

            for (var offset = start; offset < end; offset++)
            {
                var neighborGlobal = graph.Neighbors[offset];
                if (!localIndexByGlobal.TryGetValue(neighborGlobal, out var localNeighbor) || localIndex > localNeighbor)
                {
                    continue;
                }

                var toCell = new SfdpPackingCell(
                    CellValue((int)(Math.Round(layout.Layout.X[localNeighbor]) - roundedMinX), step),
                    CellValue((int)(Math.Round(layout.Layout.Y[localNeighbor]) - roundedMinY), step));
                AddLineCells(fromCell, toCell, cells);
            }
        }
    }

    private static void AddLineCells(
        SfdpPackingCell start,
        SfdpPackingCell end,
        HashSet<long> cells)
    {
        var x = start.X;
        var y = start.Y;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var sx = dx >= 0 ? 1 : -1;
        var sy = dy >= 0 ? 1 : -1;
        var ax = Math.Abs(dx) << 1;
        var ay = Math.Abs(dy) << 1;

        if (ax > ay)
        {
            var d = ay - (ax >> 1);
            while (true)
            {
                cells.Add(ToCellKey(x, y));
                if (x == end.X)
                {
                    return;
                }

                if (d >= 0)
                {
                    y += sy;
                    d -= ax;
                }

                x += sx;
                d += ay;
            }
        }

        var error = ax - (ay >> 1);
        while (true)
        {
            cells.Add(ToCellKey(x, y));
            if (y == end.Y)
            {
                return;
            }

            if (error >= 0)
            {
                x += sx;
                error -= ay;
            }

            y += sy;
            error += ax;
        }
    }

    private static int CellValue(int value, int step)
        => value >= 0
            ? value / step
            : ((value + 1) / step) - 1;

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

    private readonly record struct SfdpPackingCell(int X, int Y);

    private sealed record SfdpPackingShape(
        SfdpPackingCell[] Cells,
        int GridWidth,
        int GridHeight,
        int Perimeter,
        double RoundedMinX,
        double RoundedMinY);
}
