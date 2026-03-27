namespace PSGraphView.Msagl;

public sealed class MsaglSugiyamaOptions
{
    public string BackgroundColor { get; init; } = "#ffffff";
    public bool ShowLabels { get; init; }
    public bool ShowArrows { get; init; }
    public double NodeRadius { get; init; } = 4.0;
    public double EdgeLineWidth { get; init; } = 0.8;
    public double LabelFontSize { get; init; } = 8.0;
    public string GroupMetadataKey { get; init; } = "group";
    public bool DisableGroupColors { get; init; }
    public string Direction { get; init; } = "Horizontal";
    public double LayerSeparation { get; init; } = 90.0;
    public double NodeSeparation { get; init; } = 24.0;
    public string EdgeRouting { get; init; } = "SugiyamaSplines";
    public double LabelOffsetX { get; init; } = 6.0;
    public double LabelOffsetY { get; init; } = 0.0;
    public double? Width { get; init; }
    public double? Height { get; init; }
}
