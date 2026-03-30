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
        var dominantX = new double[100];
        var dominantY = new double[100];
        for (var i = 0; i < 50; i++)
        {
            dominantX[i] = 0.0;
            dominantY[i] = 0.0;
        }

        for (var i = 50; i < 100; i++)
        {
            dominantX[i] = 100.0;
            dominantY[i] = 100.0;
        }

        var layouts = new List<SfdpComponentLayout>
        {
            new(0, Enumerable.Range(0, 100).ToArray(), dominantX, dominantY)
        };

        for (var i = 1; i <= 20; i++)
        {
            layouts.Add(new SfdpComponentLayout(i, [0], [0.0], [0.0]));
        }

        var packed = SfdpComponentPacker.Pack(layouts, 5.0, new SfdpPackingOptions
        {
            ComponentGap = 20.0,
            MaxRowWidth = 500.0
        });

        var dominant = Assert.Single(packed, static component => component.ComponentId == 0);
        Assert.Contains(packed, component => component.ComponentId != 0 && component.Bounds.MaxX <= dominant.Bounds.MinX);
        Assert.Contains(packed, component => component.ComponentId != 0 && component.Bounds.MinX >= dominant.Bounds.MaxX);

        var overallMinX = packed.Min(static component => component.Bounds.MinX);
        var overallMaxX = packed.Max(static component => component.Bounds.MaxX);
        Assert.True(overallMaxX - overallMinX < 220.0);
    }

    private static bool AreSeparatedByGap(SfdpBoundingBox first, SfdpBoundingBox second, double gap)
    {
        var horizontalGap = Math.Max(second.MinX - first.MaxX, first.MinX - second.MaxX);
        var verticalGap = Math.Max(second.MinY - first.MaxY, first.MinY - second.MaxY);
        return horizontalGap >= gap || verticalGap >= gap;
    }
}
