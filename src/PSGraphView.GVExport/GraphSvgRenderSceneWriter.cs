using System.Globalization;
using System.Xml.Linq;

namespace PSGraphView.GVExport;

public static class GraphSvgRenderSceneWriter
{
    public static XDocument CreateDocument(GraphRenderScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        XNamespace ns = "http://www.w3.org/2000/svg";
        var viewport = scene.Viewport;
        var svg = new XElement(ns + "svg",
            new XAttribute("xmlns", ns.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
            new XAttribute("width", $"{Format(viewport.OutputWidth)}pt"),
            new XAttribute("height", $"{Format(viewport.OutputHeight)}pt"),
            new XAttribute("viewBox", CreateViewBox(viewport)));

        if (scene.Style.ArrowStyle is not null)
        {
            svg.Add(BuildArrowDefinitions(ns, scene.Style.ArrowStyle));
        }

        var edgeElements = new List<XElement>(scene.Edges.Count);
        foreach (var edge in scene.Edges)
        {
            var path = new XElement(ns + "path",
                new XAttribute("d", edge.PathData),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", edge.Stroke),
                new XAttribute("stroke-width", Format(edge.StrokeWidth)),
                new XAttribute("stroke-linecap", edge.StrokeLineCap));

            if (!string.IsNullOrWhiteSpace(edge.MarkerEnd))
            {
                path.Add(new XAttribute("marker-end", edge.MarkerEnd));
            }

            edgeElements.Add(new XElement(ns + "g",
                new XAttribute("id", edge.Id),
                new XAttribute("class", "edge"),
                new XElement(ns + "title", edge.Title),
                path));
        }

        var nodeElements = new List<XElement>(scene.Nodes.Count);
        foreach (var node in scene.Nodes)
        {
            var nodeElement = new XElement(ns + "g",
                new XAttribute("id", node.Id),
                new XAttribute("class", "node"),
                new XAttribute("data-node-id", node.DataNodeId),
                new XElement(ns + "title", node.Title),
                new XElement(ns + "ellipse",
                    new XAttribute("cx", Format(node.X)),
                    new XAttribute("cy", Format(node.Y)),
                    new XAttribute("rx", Format(node.RadiusX)),
                    new XAttribute("ry", Format(node.RadiusY)),
                    new XAttribute("fill", ToSvgPaint(node.Fill)),
                    new XAttribute("stroke", ToSvgPaint(node.Stroke)),
                    new XAttribute("stroke-width", Format(node.StrokeWidth))));

            if (node.Label is not null)
            {
                var text = new XElement(ns + "text",
                    new XAttribute("x", Format(node.Label.X)),
                    new XAttribute("y", Format(node.Label.BaselineY)),
                    new XAttribute("font-size", Format(node.Label.FontSize)),
                    new XAttribute("font-family", node.Label.FontFamily),
                    new XAttribute("fill", node.Label.Fill),
                    node.Label.Text);
                if (!string.IsNullOrWhiteSpace(node.Label.TextAnchor))
                {
                    text.Add(new XAttribute("text-anchor", node.Label.TextAnchor));
                }

                nodeElement.Add(text);
            }

            nodeElements.Add(nodeElement);
        }

        var graphGroup = new XElement(ns + "g",
            new XAttribute("id", scene.Canvas.GraphId),
            new XAttribute("class", scene.Canvas.GraphClass),
            new XAttribute("transform", scene.Canvas.Transform),
            new XElement(ns + "title", scene.Canvas.GraphTitle));

        if (scene.Style.ShowBackgroundRect)
        {
            graphGroup.Add(new XElement(ns + "polygon",
                new XAttribute("fill", scene.Style.BackgroundColor),
                new XAttribute("stroke", "none"),
                new XAttribute("points", scene.Canvas.BackgroundPolygonPoints)));
        }

        graphGroup.Add(edgeElements);
        graphGroup.Add(nodeElements);
        svg.Add(graphGroup);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), svg);
    }

    public static string Write(GraphRenderScene scene)
    {
        return CreateDocument(scene).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildArrowDefinitions(XNamespace ns, GraphRenderArrowStyle arrowStyle)
    {
        var scale = Math.Max(0.05, arrowStyle.ArrowSize);
        var markerWidth = 8.0 * scale;
        var markerHeight = 8.0 * scale;
        var refX = 6.0 * scale;
        var refY = 3.0 * scale;
        var arrowLength = 6.0 * scale;
        var arrowHeight = 6.0 * scale;
        var arrowMidY = 3.0 * scale;

        return new XElement(ns + "defs",
            new XElement(ns + "marker",
                new XAttribute("id", arrowStyle.MarkerId),
                new XAttribute("markerWidth", Format(markerWidth)),
                new XAttribute("markerHeight", Format(markerHeight)),
                new XAttribute("refX", Format(refX)),
                new XAttribute("refY", Format(refY)),
                new XAttribute("orient", "auto"),
                new XAttribute("markerUnits", "strokeWidth"),
                new XElement(ns + "path",
                    new XAttribute("d", $"M0,0 L0,{Format(arrowHeight)} L{Format(arrowLength)},{Format(arrowMidY)} z"),
                    new XAttribute("fill", arrowStyle.Color))));
    }

    private static string CreateViewBox(GraphRenderViewport viewport)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatViewBoxValue(viewport.ViewBoxMinX)} {FormatViewBoxValue(viewport.ViewBoxMinY)} {FormatViewBoxValue(viewport.ViewBoxWidth)} {FormatViewBoxValue(viewport.ViewBoxHeight)}");
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatViewBoxValue(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string ToSvgPaint(string value)
    {
        return string.Equals(value, "#00000000", StringComparison.OrdinalIgnoreCase)
            ? "none"
            : value;
    }
}
