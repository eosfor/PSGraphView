namespace PSGraphView.GVExport;

public sealed class GraphRasterRenderPair
{
    public required GraphRasterRenderResult Png { get; init; }

    public required GraphRasterRenderResult Jpg { get; init; }
}
