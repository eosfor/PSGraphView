# PSGraphView

Early scaffold for the visualization split from PSGraph.

Scope note:
- `PSGraphView` is intended for visual renderers and layout engines.
- Textual/interchange exports such as Graphviz DOT and GraphML stay in `PSGraph`.
- DSM algorithms and textual DSM exports stay in `PSGraph`, while DSM SVG/Vega-style visualization helpers are migration targets for `PSGraphView`.

Current status:
- Contains a standalone `PSGraphView.Dsm` project in a sibling repository.
- Contains a standalone `PSGraphView.Vega` project in a sibling repository.
- Contains a standalone `PSGraphView.Msagl` project in a sibling repository.
- Contains a standalone `PSGraphView.PowerShell` project with initial `Export-GraphView` and `Export-DSMView` cmdlets.
- Uses `GraphView` from `PSGraph.Common` via a temporary project reference to the PSGraph repository.
- Includes working Vega exporters for the force-directed, adjacency-matrix, and tree-layout paths.
- Includes a working DSM Vega matrix exporter built on the extracted DSM node/edge payload builder.
- Includes an extracted DSM Vega data builder for reorderable matrix node/edge payloads.
- Includes an extracted DSM SVG exporter used by the legacy `DsmView` compatibility bridge.
- Includes GraphView-based MSAGL exporters for the fast-incremental and Sugiyama SVG paths, including Sugiyama label placement and SVG post-processing for tree-style layouts.
- Includes a GraphView-based MSAGL MDS exporter, so all MSAGL renderers now live in `PSGraphView`.

Repository layout:
- `src/PSGraphView.Dsm`: DSM-specific visualization library for SVG rendering.
- `src/PSGraphView.Vega`: visualization library for `GraphView`-based Vega export.
- `src/PSGraphView.Msagl`: visualization library for `GraphView`-based MSAGL export.
- `src/PSGraphView.PowerShell`: PowerShell cmdlet surface over the extracted graph and DSM renderers.
- `tests/PSGraphView.Dsm.Tests`: focused tests for the extracted DSM SVG renderer.
- `tests/PSGraphView.Vega.Tests`: focused tests for the migrated force-directed path.
- `tests/PSGraphView.Msagl.Tests`: focused tests for the initial MSAGL migration path, including Sugiyama direction and label-placement regressions.
- `tests/PSGraphView.PowerShell.Tests`: cmdlet-level tests for the new PowerShell surface.

Current PowerShell surface:
- `Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <renderer> [-As Html|Json|Svg] [-Path <file>]`
- `Export-DSMView -Dsm|Result|SequencedDsm <object> -Renderer <renderer> [-As Html|Json|Svg] [-Path <file>]`

Workspace:
- Open `/Users/andrei/repo/psgraph-visualization.code-workspace` to work on `PSGraph` and `PSGraphView` side by side in VS Code.

Temporary bridge:
- `src/PSGraphView.Vega/PSGraphView.Vega.csproj` currently references `../../PSGraph/PSGraph.Common/PSGraph.Common.csproj`.
- This keeps the new repo usable before contracts are fully extracted or packaged independently.

Next expected steps:
- Continue moving reusable Vega helpers from PSGraph into this repository, with DSM SVG generation as the next DSM-specific target after the extracted data builder.
- Expand the MSAGL project beyond the initial fast-incremental and Sugiyama paths.
- Finish shrinking `/Users/andrei/repo/PSGraph/DSM/DsmView.cs` after the extracted DSM SVG exporter and DSM Vega data builder, while leaving text-oriented DSM export in `PSGraph`.
- Replace the temporary sibling project reference with a stable contracts package or shared contracts repo.
- Add additional exporters one slice at a time, with non-Vega renderers as the next larger migration step.