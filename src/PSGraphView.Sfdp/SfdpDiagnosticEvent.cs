namespace PSGraphView.Sfdp;

public sealed record SfdpDiagnosticEvent(
    string Phase,
    string Name,
    int? ComponentId,
    int? Level,
    int? Iteration,
    IReadOnlyDictionary<string, object?> Data);
