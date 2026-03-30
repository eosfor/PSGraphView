namespace PSGraphView.Sfdp;

internal readonly record struct SfdpPrismNodeBox(
    int Index,
    double CenterX,
    double CenterY,
    double HalfWidth,
    double HalfHeight);
