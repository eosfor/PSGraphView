using PSGraphView.Sfdp;

namespace PSGraphView.Sfdp.Tests;

public sealed class SfdpComponentPackerTests
{
    [Fact]
    public void Pack_SeparatesComponentBoundsByGap()
    {
        var layouts =
            new[]
            {
                new SfdpComponentLayout(0, [0, 1], [0.0, 20.0], [0.0, 0.0]),
                new SfdpComponentLayout(1, [2, 3], [0.0, 20.0], [0.0, 0.0])
            };

        var packed = SfdpComponentPacker.Pack(layouts, 5.0, new SfdpPackingOptions
        {
            ComponentGap = 30.0,
            MaxRowWidth = 500.0
        });

        Assert.Equal(2, packed.Count);
        Assert.True(AreSeparatedByGap(packed[0].Bounds, packed[1].Bounds, 30.0));
    }

    [Fact]
    public void Pack_DominantComponent_DistributesSmallComponentsAroundIt()
    {
        var layouts = new List<SfdpComponentLayout>
        {
            new(0, [0, 1, 2, 3], [0.0, 0.0, 120.0, 120.0], [0.0, 120.0, 0.0, 120.0]),
            new(1, [4, 5], [0.0, 8.0], [0.0, 0.0]),
            new(2, [6, 7], [0.0, 8.0], [0.0, 0.0]),
            new(3, [8, 9], [0.0, 8.0], [0.0, 0.0]),
            new(4, [10, 11], [0.0, 8.0], [0.0, 0.0])
        };

        var graph = new SfdpCsrGraph(
            nodeCount: 12,
            offsets:
            [
                0, 2, 4, 6, 8,
                9, 10, 11, 12,
                13, 14, 15, 16
            ],
            neighbors:
            [
                1, 2,
                0, 3,
                0, 3,
                1, 2,
                5,
                4,
                7,
                6,
                9,
                8,
                11,
                10
            ]);

        var packedResult = SfdpComponentPacker.PackDetailed(layouts, graph, 5.0, new SfdpPackingOptions
        {
            ComponentGap = 16.0,
            MaxRowWidth = 500.0
        });
        var packed = packedResult.Components.Select(static component => component.PackedComponent).ToArray();

        var dominant = Assert.Single(packed, static component => component.ComponentId == 0);
        Assert.All(
            packed.Where(static component => component.ComponentId != 0),
            component =>
            {
                Assert.True(component.Bounds.MinX >= dominant.Bounds.MinX);
                Assert.True(component.Bounds.MaxX <= dominant.Bounds.MaxX);
                Assert.True(component.Bounds.MinY >= dominant.Bounds.MinY);
                Assert.True(component.Bounds.MaxY <= dominant.Bounds.MaxY);
            });

        var overallMinX = packed.Min(static component => component.Bounds.MinX);
        var overallMaxX = packed.Max(static component => component.Bounds.MaxX);
        Assert.True(overallMaxX - overallMinX <= dominant.Bounds.Width);
    }

    private static bool AreSeparatedByGap(SfdpBoundingBox first, SfdpBoundingBox second, double gap)
    {
        var horizontalGap = Math.Max(second.MinX - first.MaxX, first.MinX - second.MaxX);
        var verticalGap = Math.Max(second.MinY - first.MaxY, first.MinY - second.MaxY);
        return horizontalGap >= gap || verticalGap >= gap;
    }
}
