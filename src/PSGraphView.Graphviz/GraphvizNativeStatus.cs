namespace PSGraphView.Graphviz;

public enum GraphvizNativeStatus
{
    Ok = 0,
    InvalidArgument = 1,
    OutOfMemory = 2,
    ContextError = 3,
    GraphError = 4,
    LayoutError = 5,
    RenderError = 6,
    InternalError = 7
}
