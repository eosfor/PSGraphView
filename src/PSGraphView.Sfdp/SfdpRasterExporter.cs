using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

public sealed class SfdpRasterExporter
{
    public byte[] ExportPng(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Export(graph, options, cancellationToken, GraphRasterRenderSceneWriter.RenderPng);
    }

    public byte[] ExportJpg(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Export(graph, options, cancellationToken, GraphRasterRenderSceneWriter.RenderJpg);
    }

    private static byte[] Export(
        GraphView graph,
        SfdpOptions? options,
        CancellationToken cancellationToken,
        Func<GraphRenderScene, GraphRasterRenderResult> render)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(render);

        options ??= new SfdpOptions();

        using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
        var prepared = SfdpRenderScenePipeline.Prepare(graph, options, diagnostics, cancellationToken);
        var result = render(prepared.Scene);
        SfdpRenderDiagnostics.WriteRasterSurface(diagnostics, prepared.Scene, result);
        return result.Bytes;
    }
}
