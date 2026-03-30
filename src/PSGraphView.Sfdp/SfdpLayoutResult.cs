namespace PSGraphView.Sfdp;

public sealed record SfdpLayoutResult(
    double[] X,
    double[] Y,
    SfdpBoundingBox Bounds,
    int ComponentCount);
