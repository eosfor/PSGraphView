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
            new XAttribute("width", Format(viewport.OutputWidth)),
            new XAttribute("height", Format(viewport.OutputHeight)),
            new XAttribute("viewBox", CreateViewBox(viewport)));

        if (scene.Style.ArrowStyle is not null)
        {
            svg.Add(BuildArrowDefinitions(ns, scene.Style.ArrowStyle));
        }

        if (scene.Style.ShowBackgroundRect)
        {
            svg.Add(new XElement(ns + "rect",
                new XAttribute("x", Format(viewport.ViewBoxMinX)),
                new XAttribute("y", Format(viewport.ViewBoxMinY)),
                new XAttribute("width", Format(viewport.ViewBoxWidth)),
                new XAttribute("height", Format(viewport.ViewBoxHeight)),
                new XAttribute("fill", scene.Style.BackgroundColor)));
        }

        var edgeContainer = new XElement(ns + "g", new XAttribute("id", "edges"));
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

            edgeContainer.Add(new XElement(ns + "g",
                new XAttribute("id", edge.Id),
                new XAttribute("class", "edge"),
                new XElement(ns + "title", edge.Title),
                path));
        }

        var nodeContainer = new XElement(ns + "g", new XAttribute("id", "nodes"));
        foreach (var node in scene.Nodes)
        {
            var nodeElement = new XElement(ns + "g",
                new XAttribute("id", node.Id),
                new XAttribute("class", "node"),
                new XAttribute("data-node-id", node.DataNodeId),
                new XElement(ns + "title", node.Title),
                new XElement(ns + "circle",
                    new XAttribute("cx", Format(node.X)),
                    new XAttribute("cy", Format(node.Y)),
                    new XAttribute("r", Format(node.Radius)),
                    new XAttribute("fill", node.Fill),
                    new XAttribute("stroke", node.Stroke),
                    new XAttribute("stroke-width", Format(node.StrokeWidth))));

            if (node.Label is not null)
            {
                nodeElement.Add(new XElement(ns + "text",
                    new XAttribute("x", Format(node.Label.X)),
                    new XAttribute("y", Format(node.Label.BaselineY)),
                    new XAttribute("font-size", Format(node.Label.FontSize)),
                    new XAttribute("font-family", node.Label.FontFamily),
                    new XAttribute("fill", node.Label.Fill),
                    node.Label.Text));
            }

            nodeContainer.Add(nodeElement);
        }

        svg.Add(edgeContainer);
        svg.Add(nodeContainer);
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
            $"{Format(viewport.ViewBoxMinX)} {Format(viewport.ViewBoxMinY)} {Format(viewport.ViewBoxWidth)} {Format(viewport.ViewBoxHeight)}");
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
