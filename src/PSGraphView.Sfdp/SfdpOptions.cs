namespace PSGraphView.Sfdp;

public sealed class SfdpOptions
{
    public SfdpDiagnosticsOptions? Diagnostics { get; init; }
    public int? Seed { get; init; }
    public int MaxIterations { get; init; } = 500;
    public double InitialStep { get; init; } = 0.1;
    public double Tolerance { get; init; } = 0.001;
    public bool AdaptiveCooling { get; init; } = true;
    public bool UseBarnesHut { get; init; } = true;
    public SfdpQuadtreeMode QuadtreeMode { get; init; } = SfdpQuadtreeMode.Normal;
    public int BarnesHutThreshold { get; init; } = 45;
    public double BarnesHutTheta { get; init; } = 0.6;
    public int QuadtreeHybridThreshold { get; init; } = 10000;
    public int QuadtreeMaxDepth { get; init; } = 10;
    public bool EnableMultilevel { get; init; } = true;
    public int MultilevelThreshold { get; init; } = 4;
    public int MaxMultilevelDepth { get; init; } = int.MaxValue;
    public int RefinementIterations { get; init; } = 100;
    public double? NaturalLength { get; init; }
    public double? RepulsiveExponent { get; init; }
    public SfdpSmoothingMode Smoothing { get; init; } = SfdpSmoothingMode.None;
    public int SmoothingIterations { get; init; } = 50;
    public bool ApplyPrincipalComponentRotation { get; init; } = true;
    public double RotationDegrees { get; init; } = 0.0;
    public bool EnableOverlapRemoval { get; init; } = true;
    public int OverlapRemovalIterations { get; init; } = 32;
    public double OverlapRemovalPadding { get; init; } = 1.0;
    public SfdpOverlapRemovalBoxUnits OverlapRemovalBoxUnits { get; init; } = SfdpOverlapRemovalBoxUnits.OutputUnits;
    public double? OverlapRemovalHalfWidth { get; init; }
    public double? OverlapRemovalHalfHeight { get; init; }
    public double NodeRadius { get; init; } = 4.0;
    public double ComponentGap { get; init; } = 16.0 / 72.0;
    public double? Width { get; init; }
    public double? Height { get; init; }
    public string BackgroundColor { get; init; } = "#ffffff";
    public bool ShowLabels { get; init; }
    public bool ShowArrows { get; init; }
    public double LabelFontSize { get; init; } = 14.0;
    public double LabelOffsetX { get; init; } = 0.0;
    public double LabelOffsetY { get; init; } = 0.0;
    public double EdgeLineWidth { get; init; } = 0.8;
    public string EdgeColor { get; init; } = "#c0c0c0";
    public double ArrowSize { get; init; } = 1.0;
    public string GroupMetadataKey { get; init; } = "group";
    public bool DisableGroupColors { get; init; }
    public bool GraphvizNodeStyle { get; init; }
}
