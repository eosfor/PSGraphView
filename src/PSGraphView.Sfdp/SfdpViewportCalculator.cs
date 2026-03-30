namespace PSGraphView.Sfdp;

internal static class SfdpViewportCalculator
{
    private const double SvgDpi = 72.0;
    private const double RasterDpi = 96.0;
    private const double GraphvizPadInPoints = 4.0;
    private const double DefaultZoom = 1.0;
    private const double DefaultRotation = 0.0;
    private const double GraphvizPointsScale = 72.0;

    public static double GetLayoutScale(SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.OverlapRemovalBoxUnits == SfdpOverlapRemovalBoxUnits.GraphvizPoints
            ? GraphvizPointsScale
            : 1.0;
    }

    public static double[] ScaleCoordinates(double[] coordinates, double scale)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        if (Math.Abs(scale - 1.0) <= double.Epsilon)
        {
            return (double[])coordinates.Clone();
        }

        var scaled = new double[coordinates.Length];
        for (var index = 0; index < coordinates.Length; index++)
        {
            scaled[index] = coordinates[index] * scale;
        }

        return scaled;
    }

    public static SfdpBoundingBox ScaleBounds(SfdpBoundingBox bounds, double scale)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        if (Math.Abs(scale - 1.0) <= double.Epsilon)
        {
            return bounds;
        }

        return new SfdpBoundingBox(
            bounds.MinX * scale,
            bounds.MinY * scale,
            bounds.MaxX * scale,
            bounds.MaxY * scale);
    }

    public static SfdpViewportMetrics CalculateSvgViewport(
        SfdpBoundingBox contentBounds,
        SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(contentBounds);
        ArgumentNullException.ThrowIfNull(options);

        var padding = options.OverlapRemovalBoxUnits == SfdpOverlapRemovalBoxUnits.GraphvizPoints
            ? GraphvizPadInPoints
            : Math.Max(options.NodeRadius * 2.0, 12.0);
        var pageWidth = Math.Max(1.0, contentBounds.Width + (padding * 2.0));
        var pageHeight = Math.Max(1.0, contentBounds.Height + (padding * 2.0));
        var outputWidth = options.Width ?? Math.Ceiling(pageWidth);
        var outputHeight = options.Height ?? Math.Ceiling(pageHeight);
        var translationX = padding - contentBounds.MinX;
        var translationY = padding - contentBounds.MinY;

        return new SfdpViewportMetrics(
            DpiX: SvgDpi,
            DpiY: SvgDpi,
            RasterDefaultDpiX: RasterDpi,
            RasterDefaultDpiY: RasterDpi,
            Zoom: DefaultZoom,
            Rotation: DefaultRotation,
            PaddingX: padding,
            PaddingY: padding,
            TranslationX: translationX,
            TranslationY: translationY,
            LayoutScale: GetLayoutScale(options),
            PageBoundingBox: new SfdpBoundingBox(0.0, 0.0, Math.Ceiling(pageWidth), Math.Ceiling(pageHeight)),
            Viewport: new PSGraphView.GVExport.GraphRenderViewport(
                ViewBoxMinX: 0.0,
                ViewBoxMinY: 0.0,
                ViewBoxWidth: Math.Ceiling(pageWidth),
                ViewBoxHeight: Math.Ceiling(pageHeight),
                OutputWidth: outputWidth,
                OutputHeight: outputHeight));
    }
}

internal sealed record SfdpViewportMetrics(
    double DpiX,
    double DpiY,
    double RasterDefaultDpiX,
    double RasterDefaultDpiY,
    double Zoom,
    double Rotation,
    double PaddingX,
    double PaddingY,
    double TranslationX,
    double TranslationY,
    double LayoutScale,
    SfdpBoundingBox PageBoundingBox,
    PSGraphView.GVExport.GraphRenderViewport Viewport);
