namespace PSGraphView.Graphviz;

public sealed record GraphScene
{
    public GraphScene(
        string name,
        SceneRect? bounds = null,
        IReadOnlyList<SceneCommand>? commands = null,
        IReadOnlyList<SceneObject>? objects = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Bounds = bounds;
        Commands = commands ?? Array.Empty<SceneCommand>();
        Objects = objects ?? Array.Empty<SceneObject>();
    }

    public string Name { get; }

    public SceneRect? Bounds { get; }

    public IReadOnlyList<SceneCommand> Commands { get; }

    public IReadOnlyList<SceneObject> Objects { get; }
}

public sealed record SceneObject
{
    public SceneObject(
        SceneObjectKind kind,
        string? name = null,
        SceneRect? bounds = null,
        IReadOnlyList<SceneCommand>? commands = null)
    {
        Kind = kind;
        Name = name;
        Bounds = bounds;
        Commands = commands ?? Array.Empty<SceneCommand>();
    }

    public SceneObjectKind Kind { get; }

    public string? Name { get; }

    public SceneRect? Bounds { get; }

    public IReadOnlyList<SceneCommand> Commands { get; }
}

public enum SceneObjectKind
{
    Graph = 0,
    Node = 1,
    Edge = 2,
    Subgraph = 3
}
