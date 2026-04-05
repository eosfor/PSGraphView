namespace PSGraphView.Graphviz;

public sealed record GraphvizNativeLayoutRequest(
    string Engine = "dot",
    bool IncludeXdot = false);
