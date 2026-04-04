namespace PSGraphView.Sfdp;

public sealed class SfdpExportBundle
{
    public string? Svg { get; init; }

    public byte[]? PngBytes { get; init; }

    public byte[]? JpgBytes { get; init; }
}
