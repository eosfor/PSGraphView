namespace PSGraphView.Sfdp;

internal readonly record struct SfdpPrismEdge(
    int Left,
    int Right,
    double IdealDistance = 0.0,
    double Weight = 0.0,
    bool IsOverlapConstraint = false)
{
    public SfdpPrismEdge Normalize()
        => Left <= Right ? this : this with { Left = Right, Right = Left };
}
