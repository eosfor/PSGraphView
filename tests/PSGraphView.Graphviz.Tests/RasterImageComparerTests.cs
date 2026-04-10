using SkiaSharp;

namespace PSGraphView.Graphviz.Tests;

public sealed class RasterImageComparerTests
{
    [Fact]
    public void Compare_IdenticalImages_ReturnsPerfectMetrics()
    {
        var imageBytes = CreateSolidPng(16, 10, SKColors.White);

        var result = RasterImageComparer.Compare(imageBytes, imageBytes);

        Assert.Equal(16, result.BaselineSize.Width);
        Assert.Equal(10, result.BaselineSize.Height);
        Assert.Equal(16, result.ComparisonSize.Width);
        Assert.Equal(10, result.ComparisonSize.Height);
        Assert.Equal(0, result.Metrics.MeanAbsoluteDifference, 6);
        Assert.Equal(0, result.Metrics.RootMeanSquareDifference, 6);
        Assert.Equal(0, result.Metrics.MaxAbsoluteDifference, 6);
        Assert.Equal(0, result.Metrics.DifferentPixelRatio, 6);
        Assert.Equal(1, result.Metrics.GlobalStructuralSimilarity, 6);
    }

    [Fact]
    public void Compare_BlackAndWhiteImages_ReturnsHighDifferenceMetrics()
    {
        var baselineBytes = CreateSolidPng(12, 8, SKColors.Black);
        var candidateBytes = CreateSolidPng(12, 8, SKColors.White);

        var result = RasterImageComparer.Compare(baselineBytes, candidateBytes);

        Assert.Equal(255, result.Metrics.MeanAbsoluteDifference, 3);
        Assert.Equal(255, result.Metrics.RootMeanSquareDifference, 3);
        Assert.Equal(255, result.Metrics.MaxAbsoluteDifference, 3);
        Assert.Equal(1, result.Metrics.DifferentPixelRatio, 6);
        Assert.True(result.Metrics.GlobalStructuralSimilarity < 0.01);
    }

    [Fact]
    public void Compare_RespectsMaximumComparisonDimension()
    {
        var baselineBytes = CreateSolidPng(120, 60, SKColors.Black);
        var candidateBytes = CreateSolidPng(80, 40, SKColors.Black);
        var options = new RasterComparisonOptions(MaxComparisonDimension: 30, DifferentPixelThreshold: 8);

        var result = RasterImageComparer.Compare(baselineBytes, candidateBytes, options);

        Assert.Equal(30, result.ComparisonSize.Width);
        Assert.Equal(15, result.ComparisonSize.Height);
    }

    [Fact]
    public void RenderDiffPng_ProducesDecodableImageWithExpectedSize()
    {
        var baselineBytes = CreateSolidPng(14, 9, SKColors.Black);
        var candidateBytes = CreateSolidPng(14, 9, SKColors.White);

        var diffBytes = RasterImageComparer.RenderDiffPng(baselineBytes, candidateBytes);

        Assert.Equal(0x89, diffBytes[0]);
        Assert.Equal((byte)'P', diffBytes[1]);
        Assert.Equal((byte)'N', diffBytes[2]);
        Assert.Equal((byte)'G', diffBytes[3]);

        using var bitmap = SKBitmap.Decode(diffBytes);
        Assert.NotNull(bitmap);
        Assert.Equal(14, bitmap.Width);
        Assert.Equal(9, bitmap.Height);
    }

    private static byte[] CreateSolidPng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(color);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
