using SkiaSharp;

namespace PSGraphView.Graphviz;

public sealed record RasterComparisonOptions(
    int MaxComparisonDimension = 0,
    byte DifferentPixelThreshold = 8);

public sealed record RasterImageSize(
    int Width,
    int Height);

public sealed record RasterComparisonMetrics(
    double MeanAbsoluteDifference,
    double RootMeanSquareDifference,
    double MaxAbsoluteDifference,
    double DifferentPixelRatio,
    double GlobalStructuralSimilarity);

public sealed record RasterComparisonResult(
    RasterImageSize BaselineSize,
    RasterImageSize CandidateSize,
    RasterImageSize ComparisonSize,
    byte DifferentPixelThreshold,
    RasterComparisonMetrics Metrics);

public static class RasterImageComparer
{
    public static RasterComparisonResult Compare(
        byte[] baselineImageData,
        byte[] candidateImageData,
        RasterComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baselineImageData);
        ArgumentNullException.ThrowIfNull(candidateImageData);

        var comparisonOptions = ValidateOptions(options);
        using var prepared = PrepareComparison(baselineImageData, candidateImageData, comparisonOptions);

        var metrics = CalculateMetrics(
            prepared.BaselineBitmap,
            prepared.CandidateBitmap,
            comparisonOptions.DifferentPixelThreshold);

        return new RasterComparisonResult(
            prepared.BaselineSize,
            prepared.CandidateSize,
            prepared.ComparisonSize,
            comparisonOptions.DifferentPixelThreshold,
            metrics);
    }

    public static byte[] RenderDiffPng(
        byte[] baselineImageData,
        byte[] candidateImageData,
        RasterComparisonOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baselineImageData);
        ArgumentNullException.ThrowIfNull(candidateImageData);

        var comparisonOptions = ValidateOptions(options);
        using var prepared = PrepareComparison(baselineImageData, candidateImageData, comparisonOptions);
        using var diffBitmap = new SKBitmap(
            prepared.ComparisonSize.Width,
            prepared.ComparisonSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        for (var y = 0; y < diffBitmap.Height; y++)
        {
            for (var x = 0; x < diffBitmap.Width; x++)
            {
                var baselineGray = ToGrayscale(prepared.BaselineBitmap.GetPixel(x, y));
                var candidateGray = ToGrayscale(prepared.CandidateBitmap.GetPixel(x, y));
                var delta = (byte)Math.Clamp(Math.Round(Math.Abs(baselineGray - candidateGray)), 0, 255);
                diffBitmap.SetPixel(x, y, new SKColor(delta, 0, 0, 255));
            }
        }

        using var image = SKImage.FromBitmap(diffBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static RasterComparisonOptions ValidateOptions(RasterComparisonOptions? options)
    {
        var resolvedOptions = options ?? new RasterComparisonOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(resolvedOptions.MaxComparisonDimension);
        return resolvedOptions;
    }

    private static PreparedComparison PrepareComparison(
        byte[] baselineImageData,
        byte[] candidateImageData,
        RasterComparisonOptions options)
    {
        SkiaSharpNativeLoader.EnsureLoaded();

        using var baselineSource = SKBitmap.Decode(baselineImageData)
            ?? throw new InvalidDataException("Failed to decode the baseline image.");
        using var candidateSource = SKBitmap.Decode(candidateImageData)
            ?? throw new InvalidDataException("Failed to decode the candidate image.");

        var baselineSize = new RasterImageSize(baselineSource.Width, baselineSource.Height);
        var candidateSize = new RasterImageSize(candidateSource.Width, candidateSource.Height);
        var comparisonSize = GetComparisonSize(baselineSize, candidateSize, options.MaxComparisonDimension);

        return new PreparedComparison(
            NormalizeBitmap(baselineSource, comparisonSize),
            NormalizeBitmap(candidateSource, comparisonSize),
            baselineSize,
            candidateSize,
            comparisonSize);
    }

    private static RasterImageSize GetComparisonSize(
        RasterImageSize baselineSize,
        RasterImageSize candidateSize,
        int maxComparisonDimension)
    {
        var width = Math.Max(baselineSize.Width, candidateSize.Width);
        var height = Math.Max(baselineSize.Height, candidateSize.Height);

        if (maxComparisonDimension > 0)
        {
            var longestSide = Math.Max(width, height);
            if (longestSide > maxComparisonDimension)
            {
                var scale = maxComparisonDimension / (double)longestSide;
                width = Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
                height = Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));
            }
        }

        return new RasterImageSize(width, height);
    }

    private static SKBitmap NormalizeBitmap(SKBitmap source, RasterImageSize comparisonSize)
    {
        var target = new SKBitmap(comparisonSize.Width, comparisonSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        canvas.Clear(SKColors.White);

        var scale = Math.Min(
            comparisonSize.Width / (float)source.Width,
            comparisonSize.Height / (float)source.Height);
        var scaledWidth = source.Width * scale;
        var scaledHeight = source.Height * scale;
        var offsetX = (comparisonSize.Width - scaledWidth) / 2f;
        var offsetY = (comparisonSize.Height - scaledHeight) / 2f;
        var destinationRect = SKRect.Create(offsetX, offsetY, scaledWidth, scaledHeight);

        canvas.DrawBitmap(source, destinationRect, paint);
        canvas.Flush();

        return target;
    }

    private static RasterComparisonMetrics CalculateMetrics(
        SKBitmap baselineBitmap,
        SKBitmap candidateBitmap,
        byte differentPixelThreshold)
    {
        var pixelCount = baselineBitmap.Width * baselineBitmap.Height;
        if (pixelCount <= 0)
        {
            throw new InvalidDataException("Comparison images must contain at least one pixel.");
        }

        double sumAbsoluteDifference = 0;
        double sumSquaredDifference = 0;
        double maxAbsoluteDifference = 0;
        double baselineSum = 0;
        double candidateSum = 0;
        double baselineSquaredSum = 0;
        double candidateSquaredSum = 0;
        double crossSum = 0;
        var differentPixelCount = 0;

        for (var y = 0; y < baselineBitmap.Height; y++)
        {
            for (var x = 0; x < baselineBitmap.Width; x++)
            {
                var baselineGray = ToGrayscale(baselineBitmap.GetPixel(x, y));
                var candidateGray = ToGrayscale(candidateBitmap.GetPixel(x, y));
                var absoluteDifference = Math.Abs(baselineGray - candidateGray);

                sumAbsoluteDifference += absoluteDifference;
                sumSquaredDifference += absoluteDifference * absoluteDifference;
                maxAbsoluteDifference = Math.Max(maxAbsoluteDifference, absoluteDifference);

                baselineSum += baselineGray;
                candidateSum += candidateGray;
                baselineSquaredSum += baselineGray * baselineGray;
                candidateSquaredSum += candidateGray * candidateGray;
                crossSum += baselineGray * candidateGray;

                if (absoluteDifference > differentPixelThreshold)
                {
                    differentPixelCount++;
                }
            }
        }

        var meanAbsoluteDifference = sumAbsoluteDifference / pixelCount;
        var rootMeanSquareDifference = Math.Sqrt(sumSquaredDifference / pixelCount);
        var differentPixelRatio = differentPixelCount / (double)pixelCount;
        var baselineMean = baselineSum / pixelCount;
        var candidateMean = candidateSum / pixelCount;
        var baselineVariance = Math.Max(0, baselineSquaredSum / pixelCount - baselineMean * baselineMean);
        var candidateVariance = Math.Max(0, candidateSquaredSum / pixelCount - candidateMean * candidateMean);
        var covariance = crossSum / pixelCount - baselineMean * candidateMean;

        const double c1 = 6.5025;
        const double c2 = 58.5225;
        var ssimNumerator = (2 * baselineMean * candidateMean + c1) * (2 * covariance + c2);
        var ssimDenominator = (baselineMean * baselineMean + candidateMean * candidateMean + c1) *
                              (baselineVariance + candidateVariance + c2);
        var structuralSimilarity = ssimDenominator == 0
            ? 1
            : Math.Clamp(ssimNumerator / ssimDenominator, -1, 1);

        return new RasterComparisonMetrics(
            meanAbsoluteDifference,
            rootMeanSquareDifference,
            maxAbsoluteDifference,
            differentPixelRatio,
            structuralSimilarity);
    }

    private static double ToGrayscale(SKColor color)
    {
        return color.Red * 0.2126 + color.Green * 0.7152 + color.Blue * 0.0722;
    }

    private sealed class PreparedComparison : IDisposable
    {
        public PreparedComparison(
            SKBitmap baselineBitmap,
            SKBitmap candidateBitmap,
            RasterImageSize baselineSize,
            RasterImageSize candidateSize,
            RasterImageSize comparisonSize)
        {
            BaselineBitmap = baselineBitmap;
            CandidateBitmap = candidateBitmap;
            BaselineSize = baselineSize;
            CandidateSize = candidateSize;
            ComparisonSize = comparisonSize;
        }

        public SKBitmap BaselineBitmap { get; }

        public SKBitmap CandidateBitmap { get; }

        public RasterImageSize BaselineSize { get; }

        public RasterImageSize CandidateSize { get; }

        public RasterImageSize ComparisonSize { get; }

        public void Dispose()
        {
            BaselineBitmap.Dispose();
            CandidateBitmap.Dispose();
        }
    }
}
