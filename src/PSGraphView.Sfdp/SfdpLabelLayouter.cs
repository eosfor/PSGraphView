namespace PSGraphView.Sfdp;

internal static class SfdpLabelLayouter
{
    internal sealed record LabelPlacement(
        int NodeIndex,
        double X,
        double Y,
        double Width,
        double Height);

    public static IReadOnlyList<LabelPlacement> PlaceLabels(
        IReadOnlyList<string> labels,
        double[] x,
        double[] y,
        SfdpOptions options)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(options);

        var placements = new LabelPlacement[labels.Count];
        for (var index = 0; index < labels.Count; index++)
        {
            var width = Math.Max(6.0, labels[index].Length * options.LabelFontSize * 0.56);
            var height = Math.Max(6.0, options.LabelFontSize + 2.0);
            placements[index] = new LabelPlacement(
                index,
                x[index] - (width * 0.5) + options.LabelOffsetX,
                y[index] - (height * 0.5) + options.LabelOffsetY,
                width,
                height);
        }

        return placements;
    }
}
