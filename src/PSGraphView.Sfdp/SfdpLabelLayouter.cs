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
        if (labels.Count == 0)
        {
            return placements;
        }

        var centerX = x.Average();
        var centerY = y.Average();
        var order = Enumerable.Range(0, labels.Count)
            .OrderByDescending(index =>
            {
                var dx = x[index] - centerX;
                var dy = y[index] - centerY;
                return dx * dx + dy * dy;
            })
            .ToArray();

        var placed = new List<LabelPlacement>(labels.Count);
        foreach (var index in order)
        {
            var width = Math.Max(6.0, labels[index].Length * options.LabelFontSize * 0.56);
            var height = Math.Max(6.0, options.LabelFontSize + 2.0);
            var candidates = BuildCandidates(index, x[index], y[index], width, height, centerX, centerY, options);
            LabelPlacement? best = null;
            var bestScore = double.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                var score = ScoreCandidate(candidate, x, y, options, placed);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            var placement = best ?? candidates[0];
            placements[index] = placement;
            placed.Add(placement);
        }

        return placements;
    }

    private static LabelPlacement[] BuildCandidates(
        int nodeIndex,
        double nodeX,
        double nodeY,
        double width,
        double height,
        double centerX,
        double centerY,
        SfdpOptions options)
    {
        var gapX = options.NodeRadius + options.LabelOffsetX + 4.0;
        var gapY = options.NodeRadius + Math.Abs(options.LabelOffsetY) + 4.0;

        LabelPlacement Right() => new(nodeIndex, nodeX + gapX, nodeY - height * 0.5 + options.LabelOffsetY, width, height);
        LabelPlacement Left() => new(nodeIndex, nodeX - gapX - width, nodeY - height * 0.5 + options.LabelOffsetY, width, height);
        LabelPlacement Top() => new(nodeIndex, nodeX - width * 0.5, nodeY - gapY - height + options.LabelOffsetY, width, height);
        LabelPlacement Bottom() => new(nodeIndex, nodeX - width * 0.5, nodeY + gapY + options.LabelOffsetY, width, height);
        LabelPlacement TopRight() => new(nodeIndex, nodeX + gapX * 0.75, nodeY - gapY - height * 0.5 + options.LabelOffsetY, width, height);
        LabelPlacement TopLeft() => new(nodeIndex, nodeX - gapX * 0.75 - width, nodeY - gapY - height * 0.5 + options.LabelOffsetY, width, height);
        LabelPlacement BottomRight() => new(nodeIndex, nodeX + gapX * 0.75, nodeY + gapY - height * 0.5 + options.LabelOffsetY, width, height);
        LabelPlacement BottomLeft() => new(nodeIndex, nodeX - gapX * 0.75 - width, nodeY + gapY - height * 0.5 + options.LabelOffsetY, width, height);

        var outwardRight = nodeX >= centerX;
        var outwardBottom = nodeY >= centerY;

        return
        [
            outwardRight ? Right() : Left(),
            outwardBottom ? Bottom() : Top(),
            outwardRight
                ? (outwardBottom ? BottomRight() : TopRight())
                : (outwardBottom ? BottomLeft() : TopLeft()),
            outwardRight ? (outwardBottom ? TopRight() : BottomRight()) : (outwardBottom ? TopLeft() : BottomLeft()),
            outwardRight ? Left() : Right(),
            outwardBottom ? Top() : Bottom(),
            outwardRight ? (outwardBottom ? BottomLeft() : TopLeft()) : (outwardBottom ? BottomRight() : TopRight()),
            outwardRight ? (outwardBottom ? TopLeft() : BottomLeft()) : (outwardBottom ? TopRight() : BottomRight())
        ];
    }

    private static double ScoreCandidate(
        LabelPlacement candidate,
        double[] nodeX,
        double[] nodeY,
        SfdpOptions options,
        IReadOnlyList<LabelPlacement> placed)
    {
        var score = 0.0;

        for (var i = 0; i < nodeX.Length; i++)
        {
            if (i == candidate.NodeIndex)
            {
                continue;
            }

            if (IntersectsCircle(candidate, nodeX[i], nodeY[i], options.NodeRadius + 2.0))
            {
                score += 1000.0;
            }
        }

        if (IntersectsCircle(candidate, nodeX[candidate.NodeIndex], nodeY[candidate.NodeIndex], options.NodeRadius + 1.0))
        {
            score += 5000.0;
        }

        foreach (var existing in placed)
        {
            if (Intersects(candidate, existing))
            {
                score += 10000.0 + IntersectionArea(candidate, existing);
            }
        }

        score += Math.Abs(candidate.X - nodeX[candidate.NodeIndex]) * 0.02;
        score += Math.Abs(candidate.Y - nodeY[candidate.NodeIndex]) * 0.02;
        return score;
    }

    private static bool IntersectsCircle(LabelPlacement rect, double cx, double cy, double radius)
    {
        var closestX = Math.Clamp(cx, rect.X, rect.X + rect.Width);
        var closestY = Math.Clamp(cy, rect.Y, rect.Y + rect.Height);
        var dx = cx - closestX;
        var dy = cy - closestY;
        return dx * dx + dy * dy < radius * radius;
    }

    private static bool Intersects(LabelPlacement left, LabelPlacement right)
    {
        return left.X < right.X + right.Width &&
               left.X + left.Width > right.X &&
               left.Y < right.Y + right.Height &&
               left.Y + left.Height > right.Y;
    }

    private static double IntersectionArea(LabelPlacement left, LabelPlacement right)
    {
        var width = Math.Max(0.0, Math.Min(left.X + left.Width, right.X + right.Width) - Math.Max(left.X, right.X));
        var height = Math.Max(0.0, Math.Min(left.Y + left.Height, right.Y + right.Height) - Math.Max(left.Y, right.Y));
        return width * height;
    }
}
