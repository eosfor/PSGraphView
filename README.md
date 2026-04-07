# PSGraphView

Early scaffold for the visualization split from PSGraph.

Scope note:
- `PSGraphView` is intended for visual renderers and layout engines.
- Textual/interchange exports such as Graphviz DOT and GraphML stay in `PSGraph`.
- DSM algorithms and textual DSM exports stay in `PSGraph`, while DSM SVG/Vega-style visualization helpers are migration targets for `PSGraphView`.

Current status:
- Contains a standalone `PSGraphView.Dsm` project in a sibling repository.
- Contains a standalone `PSGraphView.Graphviz` project in a sibling repository.
- Contains a standalone `PSGraphView.Vega` project in a sibling repository.
- Contains a standalone `PSGraphView.Msagl` project in a sibling repository.
- Contains a standalone `PSGraphView.PowerShell` project with initial `Export-GraphView`, `Export-GraphvizView`, and `Export-DSMView` cmdlets.
- Uses `GraphView` from the published `PSGraph.Common` NuGet package.
- Includes working Vega exporters for the force-directed, adjacency-matrix, and tree-layout paths.
- Includes a working DSM Vega matrix exporter built on the extracted DSM node/edge payload builder.
- Includes an extracted DSM Vega data builder for reorderable matrix node/edge payloads.
- Includes an extracted DSM SVG exporter used by the legacy `DsmView` compatibility bridge.
- Includes GraphView-based MSAGL exporters for the fast-incremental and Sugiyama SVG paths, including Sugiyama label placement and SVG post-processing for tree-style layouts.
- Includes a GraphView-based MSAGL MDS exporter, so all MSAGL renderers now live in `PSGraphView`.

Repository layout:
- `src/PSGraphView.Dsm`: DSM-specific visualization library for SVG rendering.
- `src/PSGraphView.Graphviz`: native Graphviz interop library for `DOT -> Graphviz JSON draw payload`.
- `src/PSGraphView.Vega`: visualization library for `GraphView`-based Vega export.
- `src/PSGraphView.Msagl`: visualization library for `GraphView`-based MSAGL export.
- `src/PSGraphView.PowerShell`: PowerShell cmdlet surface over the extracted graph and DSM renderers.
- `tests/PSGraphView.Dsm.Tests`: focused tests for the extracted DSM SVG renderer.
- `tests/PSGraphView.Graphviz.Tests`: focused tests for native `libpsgv` interop.
- `tests/PSGraphView.Vega.Tests`: focused tests for the migrated force-directed path.
- `tests/PSGraphView.Msagl.Tests`: focused tests for the initial MSAGL migration path, including Sugiyama direction and label-placement regressions.
- `tests/PSGraphView.PowerShell.Tests`: cmdlet-level tests for the new PowerShell surface.

Current PowerShell surface:
- `Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <renderer> [-As Html|Json|Svg] [-Path <file>]`
- `Export-GraphvizView -InputObject <dot>|-DotPath <file> -Renderer <Dot|Neato|Fdp|Sfdp|Twopi|Circo> [-As Json|Svg|Png|Jpg] [-OutputPath <file>]`
- `Export-DSMView -Dsm|Result|SequencedDsm <object> -Renderer <renderer> [-As Html|Json|Svg] [-Path <file>]`

Bundled Graphviz runtime:
- `src/PSGraphView.Graphviz` contains the native `libpsgv` interop layer for `DOT -> Graphviz JSON draw payload`.
- `Export-GraphvizView -As Json` already uses this native path and returns the Graphviz JSON layout payload with draw commands.
- `Export-GraphvizView -As Svg` already uses the native path `DOT -> Graphviz JSON -> scene -> Svg`.
- `Export-GraphvizView -As Png|Jpg` still uses the process-based Graphviz fallback until the raster renderer is implemented.
- `dotnet publish src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj` can stage bundled native Graphviz runtimes under `runtimes/<rid>/native` when these MSBuild properties are set:
  - `IncludeGraphvizRuntimeBundle=true`
  - `GraphvizRuntimeVersion=<psgv runtime version>`
- Runtime assets are downloaded from the private GitHub repository `eosfor/graphviz-psgv`, so publish needs a token in `PSGRAPHVIEW_GITHUB_TOKEN` or `GITHUB_TOKEN`.
- The publish workflow expects the secret `GRAPHVIZ_RUNTIME_GITHUB_TOKEN` for this download step.

Demo scripts:
- The scripts under `demos/` load installed `PSQuickGraph` and `PSGraphView` modules from `PSModulePath` by default.
- Install them for demo use with `Install-Module PSQuickGraph -Scope CurrentUser` and `Install-Module PSGraphView -Scope CurrentUser`.
- For local development before publishing, rerun a demo with `-UseLocalModules` to prefer repo build outputs when available.
- You can also point directly at build outputs with `-PSQuickGraphManifestPath <path-to-PSQuickGraph.psd1>` and `-PSGraphViewManifestPath <path-to-PSGraphView.psd1>`.
- `demos/Compare-WikiVote-GraphvizSvg.ps1` builds or reuses one DOT file, writes both `dot -Tsvg` and native `Export-GraphvizView -As Svg` outputs, and saves warm/cold timing results to JSON.
- For local native Graphviz benchmarking without a bundled runtime, pass `-GraphvizNativeLibraryPath <path-to-libpsgv>` or set `PSGRAPHVIEW_PSGV_LIBRARY_PATH`.

Workspace:
- Open `/Users/andrei/repo/psgraph-visualization.code-workspace` to work on `PSGraph` and `PSGraphView` side by side in VS Code.

Dependency model:
- `PSGraphView` consumes `PSGraph.Common` from NuGet instead of a sibling checkout of `PSGraph`.
- This keeps CI and release workflows standalone while the shared contracts stay in the `PSGraph.Common` package.

Next expected steps:
- Continue moving reusable Vega helpers from PSGraph into this repository, with DSM SVG generation as the next DSM-specific target after the extracted data builder.
- Expand the MSAGL project beyond the initial fast-incremental and Sugiyama paths.
- Finish shrinking `/Users/andrei/repo/PSGraph/DSM/DsmView.cs` after the extracted DSM SVG exporter and DSM Vega data builder, while leaving text-oriented DSM export in `PSGraph`.
- Keep the `PSGraph.Common` package version aligned with the `PSGraph` release that publishes shared contracts.
- Add additional exporters one slice at a time, with non-Vega renderers as the next larger migration step.
