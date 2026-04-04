namespace PSGraphView.Sfdp;

[Flags]
public enum SfdpExportBundleFormats
{
    None = 0,
    Svg = 1,
    Png = 2,
    Jpg = 4,
    All = Svg | Png | Jpg
}
