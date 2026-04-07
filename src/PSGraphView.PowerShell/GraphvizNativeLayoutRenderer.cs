using PSGraphView.Graphviz;

namespace PSGraphView.PowerShell;

internal static class GraphvizNativeLayoutRenderer
{
    public static string RenderXdotJson(string dot, GraphvizLayoutEngine engine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dot);

        using var session = new GraphvizNativeSession();
        var result = session.LayoutDot(dot, new GraphvizNativeLayoutRequest(ToNativeEngine(engine), IncludeXdot: true));
        return result.XdotJson;
    }

    private static string ToNativeEngine(GraphvizLayoutEngine engine)
    {
        return engine switch
        {
            GraphvizLayoutEngine.Dot => "dot",
            GraphvizLayoutEngine.Neato => "neato",
            GraphvizLayoutEngine.Fdp => "fdp",
            GraphvizLayoutEngine.Sfdp => "sfdp",
            GraphvizLayoutEngine.Twopi => "twopi",
            GraphvizLayoutEngine.Circo => "circo",
            _ => throw new NotSupportedException($"Graphviz engine '{engine}' is not supported.")
        };
    }
}
