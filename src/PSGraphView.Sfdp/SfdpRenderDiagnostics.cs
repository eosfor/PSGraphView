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

    public static void WriteLabels(
        SfdpDiagnosticsWriter diagnostics,
        GraphRenderScene scene)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(scene);

        var labeledNodes = scene.Nodes
            .Where(static node => node.Label is not null)
            .ToArray();

        double? averageWidth = null;
        double? averageHeight = null;
        double? averageOffsetX = null;
        double? averageBaselineOffsetY = null;
        double? averageFontSize = null;
        double? maxWidth = null;
        double? maxHeight = null;
        double? maxAbsOffsetX = null;
        double? maxAbsBaselineOffsetY = null;
        string? fontFamilies = null;

        if (labeledNodes.Length > 0)
        {
            averageWidth = labeledNodes.Average(node => GetApproximateLabelWidth(node.Label!));
            averageHeight = labeledNodes.Average(node => GetApproximateLabelHeight(node.Label!));
            averageOffsetX = labeledNodes.Average(node => node.Label!.X - node.X);
            averageBaselineOffsetY = labeledNodes.Average(node => node.Label!.BaselineY - node.Y);
            averageFontSize = labeledNodes.Average(node => node.Label!.FontSize);
            maxWidth = labeledNodes.Max(node => GetApproximateLabelWidth(node.Label!));
            maxHeight = labeledNodes.Max(node => GetApproximateLabelHeight(node.Label!));
            maxAbsOffsetX = labeledNodes.Max(node => Math.Abs(node.Label!.X - node.X));
            maxAbsBaselineOffsetY = labeledNodes.Max(node => Math.Abs(node.Label!.BaselineY - node.Y));
            fontFamilies = string.Join(
                ",",
                labeledNodes
                    .Select(node => node.Label!.FontFamily)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static family => family, StringComparer.Ordinal));
        }

        diagnostics.Write("render", "labels",
        [
            ("count", labeledNodes.Length),
            ("averageWidth", averageWidth),
            ("averageHeight", averageHeight),
            ("averageOffsetX", averageOffsetX),
            ("averageBaselineOffsetY", averageBaselineOffsetY),
            ("averageFontSize", averageFontSize),
            ("maxWidth", maxWidth),
            ("maxHeight", maxHeight),
            ("maxAbsOffsetX", maxAbsOffsetX),
            ("maxAbsBaselineOffsetY", maxAbsBaselineOffsetY),
            ("fontFamilies", fontFamilies)
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
            ("rasterHeightPoints", scene.Viewport.RasterHeightPoints),
            ("encodeQuality", result.EncodeQuality),
            ("opaqueOutputPolicy", result.OpaqueOutputPolicy),
            ("opaqueFallbackColor", result.OpaqueFallbackColor),
            ("opaqueAlphaThreshold", result.OpaqueAlphaThreshold),
            ("textHintingLevel", result.TextHintingLevel),
            ("subpixelText", result.SubpixelText),
            ("lcdRenderText", result.LcdRenderText),
            ("autohintedText", result.AutohintedText)
        ]);
    }

    private static int CountElements(XElement root, string localName)
        => root.Descendants().Count(element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal));

    private static double GetApproximateLabelWidth(GraphRenderLabel label)
        => Math.Max(6.0, label.Text.Length * label.FontSize * 0.56);

    private static double GetApproximateLabelHeight(GraphRenderLabel label)
        => Math.Max(6.0, label.FontSize + 2.0);

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
