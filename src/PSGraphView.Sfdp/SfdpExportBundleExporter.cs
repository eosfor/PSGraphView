using PSGraph.Model;
using PSGraphView.GVExport;

namespace PSGraphView.Sfdp;

public sealed class SfdpExportBundleExporter
{
    public SfdpExportBundle Export(
        GraphView graph,
        SfdpOptions? options = null,
        SfdpExportBundleDiagnosticsOptions? diagnosticsOptions = null,
        SfdpExportBundleFormats formats = SfdpExportBundleFormats.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (formats == SfdpExportBundleFormats.None)
        {
            throw new ArgumentOutOfRangeException(nameof(formats), formats, "At least one export format must be requested.");
        }

        options ??= new SfdpOptions();

        var sharedDiagnosticsContext = SfdpBundleDiagnosticsContext.Create(diagnosticsOptions);
        try
        {
            var effectiveOptions = CloneOptions(options, sharedDiagnosticsContext.SharedDiagnostics);
            using var diagnostics = SfdpDiagnosticsWriter.Create(effectiveOptions.Diagnostics);
            var prepared = SfdpRenderScenePipeline.Prepare(graph, effectiveOptions, diagnostics, cancellationToken);

            string? svg = null;
            if (formats.HasFlag(SfdpExportBundleFormats.Svg))
            {
                SfdpSvgExporter.WriteSvgGeometry(
                    diagnostics,
                    "export_input",
                    prepared.GraphGeometry,
                    effectiveOptions,
                    prepared.LabelPlacements,
                    null,
                    null,
                    null,
                    null,
                    null);
                SfdpSvgExporter.WriteSvgGeometry(
                    diagnostics,
                    "viewbox",
                    prepared.GraphGeometry,
                    effectiveOptions,
                    prepared.LabelPlacements,
                    prepared.ContentBounds,
                    prepared.ViewportMetrics.PaddingX,
                    prepared.Scene.Viewport.OutputWidth,
                    prepared.Scene.Viewport.OutputHeight,
                    $"{SfdpSvgExporter.Format(prepared.Scene.Viewport.ViewBoxMinX)} {SfdpSvgExporter.Format(prepared.Scene.Viewport.ViewBoxMinY)} {SfdpSvgExporter.Format(prepared.Scene.Viewport.ViewBoxWidth)} {SfdpSvgExporter.Format(prepared.Scene.Viewport.ViewBoxHeight)}");

                var document = GraphSvgRenderSceneWriter.CreateDocument(prepared.Scene);
                SfdpRenderDiagnostics.WriteSvgStructure(diagnostics, document.Root!);
                svg = document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            }

            GraphRasterRenderResult? pngResult = null;
            GraphRasterRenderResult? jpgResult = null;
            var includePng = formats.HasFlag(SfdpExportBundleFormats.Png);
            var includeJpg = formats.HasFlag(SfdpExportBundleFormats.Jpg);
            if (includePng && includeJpg)
            {
                var rasterPair = GraphRasterRenderSceneWriter.RenderPngAndJpg(prepared.Scene);
                pngResult = rasterPair.Png;
                jpgResult = rasterPair.Jpg;
            }
            else if (includePng)
            {
                pngResult = GraphRasterRenderSceneWriter.RenderPng(prepared.Scene);
            }
            else if (includeJpg)
            {
                jpgResult = GraphRasterRenderSceneWriter.RenderJpg(prepared.Scene);
            }

            if (pngResult is not null)
            {
                WriteRasterDiagnostics(sharedDiagnosticsContext.PngDiagnostics, sharedDiagnosticsContext.SharedDiagnosticsPath, prepared.Scene, pngResult);
            }

            if (jpgResult is not null)
            {
                WriteRasterDiagnostics(sharedDiagnosticsContext.JpgDiagnostics, sharedDiagnosticsContext.SharedDiagnosticsPath, prepared.Scene, jpgResult);
            }

            return new SfdpExportBundle
            {
                Svg = svg,
                PngBytes = pngResult?.Bytes,
                JpgBytes = jpgResult?.Bytes
            };
        }
        finally
        {
            sharedDiagnosticsContext.Dispose();
        }
    }

    private static void WriteRasterDiagnostics(
        SfdpDiagnosticsOptions? diagnosticsOptions,
        string? sharedDiagnosticsPath,
        GraphRenderScene scene,
        GraphRasterRenderResult result)
    {
        if (diagnosticsOptions is null || string.IsNullOrWhiteSpace(diagnosticsOptions.Path))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sharedDiagnosticsPath) &&
            !string.Equals(sharedDiagnosticsPath, diagnosticsOptions.Path, StringComparison.Ordinal))
        {
            File.Copy(sharedDiagnosticsPath, diagnosticsOptions.Path, overwrite: true);
        }

        using var diagnostics = SfdpDiagnosticsWriter.Create(diagnosticsOptions, append: true);
        SfdpRenderDiagnostics.WriteRasterSurface(diagnostics, scene, result);
    }

    private static SfdpOptions CloneOptions(SfdpOptions options, SfdpDiagnosticsOptions? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new SfdpOptions
        {
            Diagnostics = diagnostics,
            Seed = options.Seed,
            MaxIterations = options.MaxIterations,
            InitialStep = options.InitialStep,
            Tolerance = options.Tolerance,
            AdaptiveCooling = options.AdaptiveCooling,
            UseBarnesHut = options.UseBarnesHut,
            QuadtreeMode = options.QuadtreeMode,
            BarnesHutThreshold = options.BarnesHutThreshold,
            BarnesHutTheta = options.BarnesHutTheta,
            QuadtreeHybridThreshold = options.QuadtreeHybridThreshold,
            QuadtreeMaxDepth = options.QuadtreeMaxDepth,
            EnableMultilevel = options.EnableMultilevel,
            MultilevelThreshold = options.MultilevelThreshold,
            MaxMultilevelDepth = options.MaxMultilevelDepth,
            RefinementIterations = options.RefinementIterations,
            NaturalLength = options.NaturalLength,
            RepulsiveExponent = options.RepulsiveExponent,
            Smoothing = options.Smoothing,
            SmoothingIterations = options.SmoothingIterations,
            ApplyPrincipalComponentRotation = options.ApplyPrincipalComponentRotation,
            RotationDegrees = options.RotationDegrees,
            EnableOverlapRemoval = options.EnableOverlapRemoval,
            OverlapRemovalIterations = options.OverlapRemovalIterations,
            OverlapRemovalPadding = options.OverlapRemovalPadding,
            OverlapRemovalBoxUnits = options.OverlapRemovalBoxUnits,
            OverlapRemovalHalfWidth = options.OverlapRemovalHalfWidth,
            OverlapRemovalHalfHeight = options.OverlapRemovalHalfHeight,
            NodeRadius = options.NodeRadius,
            ComponentGap = options.ComponentGap,
            Width = options.Width,
            Height = options.Height,
            BackgroundColor = options.BackgroundColor,
            ShowLabels = options.ShowLabels,
            ShowArrows = options.ShowArrows,
            LabelFontSize = options.LabelFontSize,
            LabelOffsetX = options.LabelOffsetX,
            LabelOffsetY = options.LabelOffsetY,
            EdgeLineWidth = options.EdgeLineWidth,
            EdgeColor = options.EdgeColor,
            ArrowSize = options.ArrowSize,
            GroupMetadataKey = options.GroupMetadataKey,
            DisableGroupColors = options.DisableGroupColors,
            GraphvizNodeStyle = options.GraphvizNodeStyle
        };
    }

    private sealed class SfdpBundleDiagnosticsContext : IDisposable
    {
        private SfdpBundleDiagnosticsContext(
            SfdpDiagnosticsOptions? sharedDiagnostics,
            SfdpDiagnosticsOptions? pngDiagnostics,
            SfdpDiagnosticsOptions? jpgDiagnostics,
            string? sharedDiagnosticsPath,
            string? temporarySharedDiagnosticsPath)
        {
            SharedDiagnostics = sharedDiagnostics;
            PngDiagnostics = pngDiagnostics;
            JpgDiagnostics = jpgDiagnostics;
            SharedDiagnosticsPath = sharedDiagnosticsPath;
            TemporarySharedDiagnosticsPath = temporarySharedDiagnosticsPath;
        }

        public SfdpDiagnosticsOptions? SharedDiagnostics { get; }

        public SfdpDiagnosticsOptions? PngDiagnostics { get; }

        public SfdpDiagnosticsOptions? JpgDiagnostics { get; }

        public string? SharedDiagnosticsPath { get; }

        private string? TemporarySharedDiagnosticsPath { get; }

        public static SfdpBundleDiagnosticsContext Create(SfdpExportBundleDiagnosticsOptions? diagnosticsOptions)
        {
            if (diagnosticsOptions is null)
            {
                return new SfdpBundleDiagnosticsContext(null, null, null, null, null);
            }

            if (diagnosticsOptions.SvgDiagnostics is not null)
            {
                return new SfdpBundleDiagnosticsContext(
                    diagnosticsOptions.SvgDiagnostics,
                    diagnosticsOptions.PngDiagnostics,
                    diagnosticsOptions.JpgDiagnostics,
                    diagnosticsOptions.SvgDiagnostics.Path,
                    null);
            }

            if (diagnosticsOptions.PngDiagnostics is null && diagnosticsOptions.JpgDiagnostics is null)
            {
                return new SfdpBundleDiagnosticsContext(null, null, null, null, null);
            }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"psgraphview-sfdp-bundle-{Guid.NewGuid():N}.jsonl");

            var sharedDiagnostics = new SfdpDiagnosticsOptions
            {
                Path = tempPath,
                Format = diagnosticsOptions.PngDiagnostics?.Format ?? diagnosticsOptions.JpgDiagnostics?.Format ?? SfdpDiagnosticFormat.JsonLines,
                IncludeIterations = diagnosticsOptions.PngDiagnostics?.IncludeIterations ?? diagnosticsOptions.JpgDiagnostics?.IncludeIterations ?? true,
                IncludeCoordinates = diagnosticsOptions.PngDiagnostics?.IncludeCoordinates ?? diagnosticsOptions.JpgDiagnostics?.IncludeCoordinates ?? false
            };

            return new SfdpBundleDiagnosticsContext(
                sharedDiagnostics,
                diagnosticsOptions.PngDiagnostics,
                diagnosticsOptions.JpgDiagnostics,
                tempPath,
                tempPath);
        }

        public void Dispose()
        {
            if (string.IsNullOrWhiteSpace(TemporarySharedDiagnosticsPath))
            {
                return;
            }

            try
            {
                if (File.Exists(TemporarySharedDiagnosticsPath))
                {
                    File.Delete(TemporarySharedDiagnosticsPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
