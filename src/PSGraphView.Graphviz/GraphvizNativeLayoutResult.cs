namespace PSGraphView.Graphviz;

public sealed record GraphvizNativeLayoutResult(
    string XdotJson,
    string? Xdot,
    string? Diagnostics);
