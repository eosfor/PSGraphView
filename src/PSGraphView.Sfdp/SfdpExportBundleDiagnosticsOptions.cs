namespace PSGraphView.Sfdp;

public sealed class SfdpExportBundleDiagnosticsOptions
{
    public SfdpDiagnosticsOptions? SvgDiagnostics { get; init; }

    public SfdpDiagnosticsOptions? PngDiagnostics { get; init; }

    public SfdpDiagnosticsOptions? JpgDiagnostics { get; init; }
}
