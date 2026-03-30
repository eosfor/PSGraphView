namespace PSGraphView.Sfdp;

public sealed class SfdpDiagnosticsOptions
{
    public string? Path { get; init; }

    public SfdpDiagnosticFormat Format { get; init; } = SfdpDiagnosticFormat.JsonLines;

    public bool IncludeIterations { get; init; } = true;

    public bool IncludeCoordinates { get; init; }
}
