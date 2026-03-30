namespace PSGraphView.Sfdp;

internal sealed class SfdpQuadTreeSupernodeBuffer
{
    public double[] CenterX = new double[10];
    public double[] CenterY = new double[10];
    public double[] Weights = new double[10];
    public double[] Distances = new double[10];
    public int Count { get; private set; }
    public double TraversalCount { get; private set; }

    public void Reset()
    {
        Count = 0;
        TraversalCount = 0.0;
    }

    public void IncrementTraversal()
    {
        TraversalCount += 1.0;
    }

    public void Add(double centerX, double centerY, double weight, double distance)
    {
        EnsureCapacity(Count + 1);
        CenterX[Count] = centerX;
        CenterY[Count] = centerY;
        Weights[Count] = weight;
        Distances[Count] = distance;
        Count++;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= CenterX.Length)
        {
            return;
        }

        var capacity = CenterX.Length;
        while (capacity < requiredCapacity)
        {
            capacity *= 2;
        }

        Array.Resize(ref CenterX, capacity);
        Array.Resize(ref CenterY, capacity);
        Array.Resize(ref Weights, capacity);
        Array.Resize(ref Distances, capacity);
    }
}

internal sealed class SfdpQuadTree
{
    private const int BucketCapacity = 1;
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly Node _root;

    private SfdpQuadTree(double[] x, double[] y, Node root, int maxDepth)
    {
        _x = x;
        _y = y;
        _root = root;
        MaxDepth = maxDepth;
    }

    public int MaxDepth { get; }
    public int MaxDepthUsed { get; private set; }

    public static SfdpQuadTree Build(double[] x, double[] y, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var minX = x.Min();
        var maxX = x.Max();
        var minY = y.Min();
        var maxY = y.Max();
        var size = Math.Max(maxX - minX, maxY - minY);
        if (size <= 0)
        {
            size = 1.0;
        }

        var root = new Node(minX - 0.5, minY - 0.5, size + 1.0);
        var tree = new SfdpQuadTree(x, y, root, maxDepth);
        for (var i = 0; i < x.Length; i++)
        {
            tree.Insert(root, i, 0);
        }

        return tree;
    }

    public void AccumulateRepulsion(
        int targetIndex,
        double theta,
        double exponent,
        double repulsiveScale,
        ref double fx,
        ref double fy)
    {
        AccumulateRepulsion(_root, targetIndex, theta, exponent, repulsiveScale, ref fx, ref fy);
    }

    public void GetSupernodes(
        int targetIndex,
        double theta,
        SfdpQuadTreeSupernodeBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Reset();
        PopulateSupernodes(_root, targetIndex, theta, buffer);
    }

    private void Insert(Node node, int index, int depth)
    {
        MaxDepthUsed = Math.Max(MaxDepthUsed, depth);
        node.AddMass(_x[index], _y[index]);

        if (node.IsLeaf && (node.PointIndices.Count < BucketCapacity || depth >= MaxDepth))
        {
            node.PointIndices.Add(index);
            return;
        }

        if (node.IsLeaf)
        {
            node.Subdivide();
            foreach (var existingIndex in node.PointIndices)
            {
                Insert(node.GetChildForPoint(_x[existingIndex], _y[existingIndex]), existingIndex, depth + 1);
            }

            node.PointIndices.Clear();
        }

        Insert(node.GetChildForPoint(_x[index], _y[index]), index, depth + 1);
    }

    private void AccumulateRepulsion(
        Node node,
        int targetIndex,
        double theta,
        double exponent,
        double repulsiveScale,
        ref double fx,
        ref double fy)
    {
        if (node.Mass <= 0)
        {
            return;
        }

        if (node.IsLeaf)
        {
            foreach (var pointIndex in node.PointIndices)
            {
                if (pointIndex == targetIndex)
                {
                    continue;
                }

                ApplyRepulsion(_x[targetIndex], _y[targetIndex], _x[pointIndex], _y[pointIndex], 1.0, exponent, repulsiveScale, ref fx, ref fy);
            }

            return;
        }

        var dx = _x[targetIndex] - node.CenterOfMassX;
        var dy = _y[targetIndex] - node.CenterOfMassY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 0 && node.Size / distance < theta)
        {
            ApplyRepulsion(_x[targetIndex], _y[targetIndex], node.CenterOfMassX, node.CenterOfMassY, node.Mass, exponent, repulsiveScale, ref fx, ref fy);
            return;
        }

        foreach (var child in node.Children!)
        {
            if (child is not null)
            {
                AccumulateRepulsion(child, targetIndex, theta, exponent, repulsiveScale, ref fx, ref fy);
            }
        }
    }

    private void PopulateSupernodes(
        Node node,
        int targetIndex,
        double theta,
        SfdpQuadTreeSupernodeBuffer buffer)
    {
        buffer.IncrementTraversal();

        if (node.Mass <= 0)
        {
            return;
        }

        foreach (var pointIndex in node.PointIndices)
        {
            if (pointIndex == targetIndex)
            {
                continue;
            }

            buffer.Add(
                _x[pointIndex],
                _y[pointIndex],
                1.0,
                Distance(_x[targetIndex], _y[targetIndex], _x[pointIndex], _y[pointIndex]));
        }

        if (node.IsLeaf)
        {
            return;
        }

        var centerDistance = Distance(_x[targetIndex], _y[targetIndex], node.CenterX, node.CenterY);
        if (node.HalfSize < theta * centerDistance)
        {
            buffer.Add(
                node.CenterOfMassX,
                node.CenterOfMassY,
                node.Mass,
                Distance(_x[targetIndex], _y[targetIndex], node.CenterOfMassX, node.CenterOfMassY));
            return;
        }

        foreach (var child in node.Children!)
        {
            if (child is not null)
            {
                PopulateSupernodes(child, targetIndex, theta, buffer);
            }
        }
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void ApplyRepulsion(
        double sourceX,
        double sourceY,
        double targetX,
        double targetY,
        double mass,
        double exponent,
        double repulsiveScale,
        ref double fx,
        ref double fy)
    {
        const double minDistance = 0.0001;
        var dx = sourceX - targetX;
        var dy = sourceY - targetY;
        var distance = Math.Max(Math.Sqrt(dx * dx + dy * dy), minDistance);
        var repulsive = mass * repulsiveScale / Math.Pow(distance, 1.0 - exponent);
        fx += repulsive * dx;
        fy += repulsive * dy;
    }

    private sealed class Node
    {
        public Node(double minX, double minY, double size)
        {
            MinX = minX;
            MinY = minY;
            Size = size;
            PointIndices = [];
        }

        public double MinX { get; }
        public double MinY { get; }
        public double Size { get; }
        public double CenterX => MinX + Size / 2.0;
        public double CenterY => MinY + Size / 2.0;
        public double HalfSize => Size / 2.0;
        public double Mass { get; private set; }
        public double CenterOfMassX { get; private set; }
        public double CenterOfMassY { get; private set; }
        public List<int> PointIndices { get; }
        public Node?[]? Children { get; private set; }
        public bool IsLeaf => Children is null;

        public void AddMass(double x, double y)
        {
            var newMass = Mass + 1.0;
            CenterOfMassX = ((CenterOfMassX * Mass) + x) / newMass;
            CenterOfMassY = ((CenterOfMassY * Mass) + y) / newMass;
            Mass = newMass;
        }

        public void Subdivide()
        {
            var half = Size / 2.0;
            Children =
            [
                new Node(MinX, MinY, half),
                new Node(MinX + half, MinY, half),
                new Node(MinX, MinY + half, half),
                new Node(MinX + half, MinY + half, half)
            ];
        }

        public Node GetChildForPoint(double x, double y)
        {
            var half = Size / 2.0;
            var right = x >= MinX + half;
            var bottom = y >= MinY + half;
            var index = (bottom ? 2 : 0) + (right ? 1 : 0);
            return Children![index]!;
        }
    }
}
