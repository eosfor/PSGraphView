namespace PSGraphView.GVExport;

internal enum GraphRasterBackendKind
{
    SkiaSharp = 1,
}

internal static class GraphRasterBackendSelection
{
    // Patch 3a fixes the backend choice before the production raster renderer lands.
    public static GraphRasterBackendKind Selected => GraphRasterBackendKind.SkiaSharp;
}
