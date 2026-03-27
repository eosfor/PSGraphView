namespace PSGraphView.Msagl;

public sealed class MsaglFastIncrementalOptions
{
    public string BackgroundColor { get; init; } = "#ffffff";
    public bool ShowLabels { get; init; }
    public bool ShowArrows { get; init; }
    public double NodeRadius { get; init; } = 4.0;
    public double EdgeLineWidth { get; init; } = 0.8;
    public double LabelFontSize { get; init; } = 8.0;
    public string GroupMetadataKey { get; init; } = "group";
    public bool DisableGroupColors { get; init; }
    public double RepulsiveForceConstant { get; init; } = 5.5;
    public double AttractiveForceConstant { get; init; } = 0.08;
    public double AttractiveInterClusterForceConstant { get; init; } = 0.12;
    public double GravityConstant { get; init; } = 0.0;
    public double NodeSeparation { get; init; } = 4.0;
    public int MaxIterations { get; init; } = 250;
    public int MinorIterations { get; init; } = 6;
    public int ProjectionIterations { get; init; } = 12;
    public double? Width { get; init; }
    public double? Height { get; init; }
}
