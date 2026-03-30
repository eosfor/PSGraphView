using System.Xml.Linq;
using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

internal static class SfdpRenderDiagnostics
{
    public static void WriteScene(
        SfdpDiagnosticsWriter diagnostics,
        GraphView graph,
        GraphRenderScene scene,
        SfdpOptions options)
    {
        diagnostics.Write("render", "scene",
        [
            ("nodeCount", graph.Nodes.Count),
            ("edgeCount", graph.Edges.Count),
            ("routedEdgeCount", scene.Edges.Count),
            ("labelCount", scene.Nodes.Count(static node => node.Label is not null)),
            ("showLabels", options.ShowLabels),
            ("showArrows", options.ShowArrows),
            ("hasBackgroundRect", scene.Style.ShowBackgroundRect),
            ("backgroundColor", scene.Style.BackgroundColor),
            ("nodeRadius", options.NodeRadius),
            ("edgeLineWidth", options.EdgeLineWidth)
        ]);
    }

    public static void WriteViewport(
        SfdpDiagnosticsWriter diagnostics,
        SfdpBoundingBox contentBounds,
        SfdpViewportMetrics viewportMetrics,
        SfdpOptions options)
    {
        var viewport = viewportMetrics.Viewport;
        diagnostics.Write("render", "viewport",
        [
            ("dpiX", viewportMetrics.DpiX),
            ("dpiY", viewportMetrics.DpiY),
            ("rasterDefaultDpiX", viewportMetrics.RasterDefaultDpiX),
            ("rasterDefaultDpiY", viewportMetrics.RasterDefaultDpiY),
            ("zoom", viewportMetrics.Zoom),
            ("rotation", viewportMetrics.Rotation),
            ("padding", viewportMetrics.PaddingX),
            ("padX", viewportMetrics.PaddingX),
            ("padY", viewportMetrics.PaddingY),
            ("translationX", viewportMetrics.TranslationX),
            ("translationY", viewportMetrics.TranslationY),
            ("layoutScale", viewportMetrics.LayoutScale),
            ("contentMinX", contentBounds.MinX),
            ("contentMinY", contentBounds.MinY),
            ("contentMaxX", contentBounds.MaxX),
            ("contentMaxY", contentBounds.MaxY),
            ("contentWidth", contentBounds.Width),
            ("contentHeight", contentBounds.Height),
            ("viewBoxMinX", viewport.ViewBoxMinX),
            ("viewBoxMinY", viewport.ViewBoxMinY),
            ("viewBoxWidth", viewport.ViewBoxWidth),
            ("viewBoxHeight", viewport.ViewBoxHeight),
            ("pageBoundingBoxMinX", viewportMetrics.PageBoundingBox.MinX),
            ("pageBoundingBoxMinY", viewportMetrics.PageBoundingBox.MinY),
            ("pageBoundingBoxMaxX", viewportMetrics.PageBoundingBox.MaxX),
            ("pageBoundingBoxMaxY", viewportMetrics.PageBoundingBox.MaxY),
            ("outputWidth", viewport.OutputWidth),
            ("outputHeight", viewport.OutputHeight),
            ("widthOverride", options.Width.HasValue),
            ("heightOverride", options.Height.HasValue),
            ("backgroundColor", options.BackgroundColor)
        ]);
    }

    public static void WriteSvgStructure(
        SfdpDiagnosticsWriter diagnostics,
        XElement svg)
    {
        var groups = svg.Descendants().Where(static element => element.Name.LocalName == "g").ToArray();
        var nodeGroups = groups.Where(static element => HasClass(element, "node")).ToArray();
        var edgeGroups = groups.Where(static element => HasClass(element, "edge")).ToArray();
        var clusterGroups = groups.Where(static element => HasClass(element, "cluster")).ToArray();
        var graphGroup = groups.FirstOrDefault(static element => HasClass(element, "graph"));

        diagnostics.Write("svg", "structure",
        [
            ("width", (string?)svg.Attribute("width")),
            ("height", (string?)svg.Attribute("height")),
            ("viewBox", (string?)svg.Attribute("viewBox")),
            ("groupCount", groups.Length),
            ("nodeGroupCount", nodeGroups.Length),
            ("edgeGroupCount", edgeGroups.Length),
            ("clusterGroupCount", clusterGroups.Length),
            ("titleCount", CountElements(svg, "title")),
            ("pathCount", CountElements(svg, "path")),
            ("circleCount", CountElements(svg, "circle")),
            ("ellipseCount", CountElements(svg, "ellipse")),
            ("textCount", CountElements(svg, "text")),
            ("anchorCount", CountElements(svg, "a")),
            ("rectCount", CountElements(svg, "rect")),
            ("graphGroupPresent", graphGroup is not null),
            ("graphGroupId", (string?)graphGroup?.Attribute("id")),
            ("graphGroupTransform", (string?)graphGroup?.Attribute("transform"))
        ]);
    }

    public static void WriteRasterSurface(
        SfdpDiagnosticsWriter diagnostics,
        GraphRenderScene scene,
        GraphRasterRenderResult result)
    {
        diagnostics.Write("render", "raster",
        [
            ("format", result.Format),
            ("backend", result.Backend),
            ("pixelWidth", result.PixelWidth),
            ("pixelHeight", result.PixelHeight),
            ("scaleX", result.ScaleX),
            ("scaleY", result.ScaleY),
            ("flattenedForOpaqueOutput", result.FlattenedForOpaqueOutput),
            ("byteCount", result.Bytes.Length),
            ("viewBoxWidth", scene.Viewport.ViewBoxWidth),
            ("viewBoxHeight", scene.Viewport.ViewBoxHeight),
            ("outputWidth", scene.Viewport.OutputWidth),
            ("outputHeight", scene.Viewport.OutputHeight),
            ("rasterWidthPoints", scene.Viewport.RasterWidthPoints),
            ("rasterHeightPoints", scene.Viewport.RasterHeightPoints)
        ]);
    }

    private static int CountElements(XElement root, string localName)
        => root.Descendants().Count(element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal));

    private static bool HasClass(XElement element, string className)
    {
        var classAttribute = (string?)element.Attribute("class");
        if (string.IsNullOrWhiteSpace(classAttribute))
        {
            return false;
        }

        return classAttribute
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, className, StringComparison.Ordinal));
    }
}
