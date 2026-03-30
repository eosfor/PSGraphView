using System.Globalization;
using System.Xml.Linq;
using PSGraph.Model;

namespace PSGraphView.Sfdp;

public sealed class SfdpSvgExporter
{
    private static readonly string[] GroupPalette =
    [
        "#4682b4",
        "#ffa500",
        "#008000",
        "#ff0000",
        "#ee82ee",
        "#8b4513",
        "#ffc0cb",
        "#808080",
        "#808000",
        "#00ffff"
    ];

    private readonly SfdpLayoutEngine _layoutEngine = new();

    public string Export(
        GraphView graph,
        SfdpOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        options ??= new SfdpOptions();

        using var diagnostics = SfdpDiagnosticsWriter.Create(options.Diagnostics);
        var layout = _layoutEngine.Layout(graph, options, diagnostics, cancellationToken);
        var labelPlacements = options.ShowLabels
            ? SfdpLabelLayouter.PlaceLabels(graph.Nodes.Select(node => node.Label).ToArray(), layout.X, layout.Y, options)
            : Array.Empty<SfdpLabelLayouter.LabelPlacement>();
        var routedEdges = SfdpEdgeRouter.RouteEdges(graph, layout.X, layout.Y, options);
        SfdpRenderDiagnostics.WriteScene(diagnostics, graph, routedEdges, labelPlacements, options);
        var graphGeometry = SfdpGeometrySummary.Create(SfdpGraphBuilder.BuildUndirectedCsr(SfdpGraphBuilder.BuildIndexed(graph)), layout.X, layout.Y);
        WriteSvgGeometry(diagnostics, "export_input", graphGeometry, options, labelPlacements, null, null, null, null, null);
        var padding = Math.Max(options.NodeRadius * 2.0, 12.0);
        var contentBounds = ExpandBoundsForLabels(layout.Bounds, labelPlacements);
        var minX = contentBounds.MinX - padding;
        var minY = contentBounds.MinY - padding;
        var naturalWidth = Math.Max(1.0, contentBounds.Width + padding * 2.0);
        var naturalHeight = Math.Max(1.0, contentBounds.Height + padding * 2.0);
        var outputWidth = options.Width ?? Math.Ceiling(naturalWidth);
        var outputHeight = options.Height ?? Math.Ceiling(naturalHeight);
        WriteSvgGeometry(
            diagnostics,
            "viewbox",
            graphGeometry,
            options,
            labelPlacements,
            contentBounds,
            padding,
            outputWidth,
            outputHeight,
            $"{Format(minX)} {Format(minY)} {Format(naturalWidth)} {Format(naturalHeight)}");
        SfdpRenderDiagnostics.WriteViewport(
            diagnostics,
            contentBounds,
            padding,
            minX,
            minY,
            naturalWidth,
            naturalHeight,
            outputWidth,
            outputHeight,
            options);

        XNamespace ns = "http://www.w3.org/2000/svg";
        var svg = new XElement(ns + "svg",
            new XAttribute("xmlns", ns.NamespaceName),
            new XAttribute("width", Format(outputWidth)),
            new XAttribute("height", Format(outputHeight)),
            new XAttribute("viewBox", string.Create(CultureInfo.InvariantCulture, $"{Format(minX)} {Format(minY)} {Format(naturalWidth)} {Format(naturalHeight)}")));

        if (options.ShowArrows)
        {
            svg.Add(BuildArrowDefinitions(ns, options.EdgeColor, options.ArrowSize));
        }

        svg.Add(new XElement(ns + "rect",
            new XAttribute("x", Format(minX)),
            new XAttribute("y", Format(minY)),
            new XAttribute("width", Format(naturalWidth)),
            new XAttribute("height", Format(naturalHeight)),
            new XAttribute("fill", options.BackgroundColor)));

        var edgeContainer = new XElement(ns + "g", new XAttribute("id", "edges"));
        for (var edgeIndex = 0; edgeIndex < routedEdges.Count; edgeIndex++)
        {
            var edge = routedEdges[edgeIndex];
            var sourceNode = graph.Nodes[edge.SourceIndex];
            var targetNode = graph.Nodes[edge.TargetIndex];
            var path = new XElement(ns + "path",
                new XAttribute("d", edge.PathData),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", options.EdgeColor),
                new XAttribute("stroke-width", Format(options.EdgeLineWidth)),
                new XAttribute("stroke-linecap", "round"));

            if (options.ShowArrows)
            {
                path.Add(new XAttribute("marker-end", "url(#arrowhead)"));
            }

            edgeContainer.Add(new XElement(ns + "g",
                new XAttribute("id", $"edge{edgeIndex + 1}"),
                new XAttribute("class", "edge"),
                new XElement(ns + "title", $"{sourceNode.Id}->{targetNode.Id}"),
                path));
        }

        var nodeGroup = new XElement(ns + "g", new XAttribute("id", "nodes"));
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            var node = graph.Nodes[i];
            var nodeContainer = new XElement(ns + "g",
                new XAttribute("id", $"node{i + 1}"),
                new XAttribute("class", "node"),
                new XAttribute("data-node-id", node.Id));
            nodeContainer.Add(new XElement(ns + "title", node.Id));
            nodeContainer.Add(new XElement(ns + "circle",
                new XAttribute("cx", Format(layout.X[i])),
                new XAttribute("cy", Format(layout.Y[i])),
                new XAttribute("r", Format(options.NodeRadius)),
                new XAttribute("fill", ResolveFill(node, options)),
                new XAttribute("stroke", "#555555"),
                new XAttribute("stroke-width", "1")));

            if (options.ShowLabels)
            {
                var label = labelPlacements[i];
                nodeContainer.Add(new XElement(ns + "text",
                    new XAttribute("x", Format(label.X)),
                    new XAttribute("y", Format(label.Y + (options.LabelFontSize * 0.8))),
                    new XAttribute("font-size", Format(options.LabelFontSize)),
                    new XAttribute("font-family", "sans-serif"),
                    new XAttribute("fill", "#222222"),
                    node.Label));
            }

            nodeGroup.Add(nodeContainer);
        }

        svg.Add(edgeContainer);
        svg.Add(nodeGroup);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), svg);
        SfdpRenderDiagnostics.WriteSvgStructure(diagnostics, svg);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static void WriteSvgGeometry(
        SfdpDiagnosticsWriter diagnostics,
        string stage,
        SfdpGeometrySummary graphGeometry,
        SfdpOptions options,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labels,
        SfdpBoundingBox? contentBounds,
        double? padding,
        double? outputWidth,
        double? outputHeight,
        string? viewBox)
    {
        var averageLabelSize = default(double?);
        if (labels.Count > 0)
        {
            averageLabelSize = 0.0;
            foreach (var label in labels)
            {
                averageLabelSize += label.Width + label.Height;
            }

            averageLabelSize /= labels.Count;
        }

        diagnostics.Write("svg", "geometry",
        [
            ("stage", stage),
            ("minX", graphGeometry.MinX),
            ("minY", graphGeometry.MinY),
            ("maxX", graphGeometry.MaxX),
            ("maxY", graphGeometry.MaxY),
            ("width", graphGeometry.Width),
            ("height", graphGeometry.Height),
            ("diagonal", graphGeometry.Diagonal),
            ("averageEdgeLength", graphGeometry.AverageEdgeLength),
            ("averageLabelSize", averageLabelSize),
            ("padding", padding),
            ("contentMinX", contentBounds?.MinX),
            ("contentMinY", contentBounds?.MinY),
            ("contentMaxX", contentBounds?.MaxX),
            ("contentMaxY", contentBounds?.MaxY),
            ("contentWidth", contentBounds?.Width),
            ("contentHeight", contentBounds?.Height),
            ("outputWidth", outputWidth),
            ("outputHeight", outputHeight),
            ("nodeRadius", options.NodeRadius),
            ("viewBox", viewBox)
        ]);
    }

    private static SfdpBoundingBox ExpandBoundsForLabels(
        SfdpBoundingBox bounds,
        IReadOnlyList<SfdpLabelLayouter.LabelPlacement> labels)
    {
        if (labels.Count == 0)
        {
            return bounds;
        }

        var minX = bounds.MinX;
        var minY = bounds.MinY;
        var maxX = bounds.MaxX;
        var maxY = bounds.MaxY;

        foreach (var label in labels)
        {
            minX = Math.Min(minX, label.X);
            minY = Math.Min(minY, label.Y);
            maxX = Math.Max(maxX, label.X + label.Width);
            maxY = Math.Max(maxY, label.Y + label.Height);
        }

        return new SfdpBoundingBox(minX, minY, maxX, maxY);
    }

    private static XElement BuildArrowDefinitions(XNamespace ns, string edgeColor, double arrowSize)
    {
        var scale = Math.Max(0.05, arrowSize);
        var markerWidth = 8.0 * scale;
        var markerHeight = 8.0 * scale;
        var refX = 6.0 * scale;
        var refY = 3.0 * scale;
        var arrowLength = 6.0 * scale;
        var arrowHeight = 6.0 * scale;
        var arrowMidY = 3.0 * scale;

        return new XElement(ns + "defs",
            new XElement(ns + "marker",
                new XAttribute("id", "arrowhead"),
                new XAttribute("markerWidth", Format(markerWidth)),
                new XAttribute("markerHeight", Format(markerHeight)),
                new XAttribute("refX", Format(refX)),
                new XAttribute("refY", Format(refY)),
                new XAttribute("orient", "auto"),
                new XAttribute("markerUnits", "strokeWidth"),
                new XElement(ns + "path",
                    new XAttribute("d", $"M0,0 L0,{Format(arrowHeight)} L{Format(arrowLength)},{Format(arrowMidY)} z"),
                    new XAttribute("fill", edgeColor))));
    }

    private static string ResolveFill(GraphViewNode node, SfdpOptions options)
    {
        if (options.DisableGroupColors || string.IsNullOrWhiteSpace(options.GroupMetadataKey))
        {
            return "#696969";
        }

        if (node.Metadata.TryGetValue(options.GroupMetadataKey, out var value) && TryGetGroup(value, out var group))
        {
            return GroupPalette[Math.Abs(group) % GroupPalette.Length];
        }

        return "#696969";
    }

    private static bool TryGetGroup(object? value, out int group)
    {
        group = 0;
        return value switch
        {
            int i => (group = i) == i,
            long l when l >= int.MinValue && l <= int.MaxValue => (group = (int)l) == l,
            double d => (group = (int)d) == (int)d,
            float f => (group = (int)f) == (int)f,
            _ => value is not null && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out group)
        };
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
