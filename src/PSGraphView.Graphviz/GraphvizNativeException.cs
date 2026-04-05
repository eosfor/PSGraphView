namespace PSGraphView.Graphviz;

public sealed class GraphvizNativeException : InvalidOperationException
{
    public GraphvizNativeException(
        GraphvizNativeStatus status,
        string message,
        string? diagnostics)
        : base(string.IsNullOrWhiteSpace(diagnostics) ? message : $"{message} {diagnostics}")
    {
        Status = status;
        Diagnostics = diagnostics;
    }

    public GraphvizNativeStatus Status { get; }

    public string? Diagnostics { get; }
}
