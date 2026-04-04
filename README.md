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
- Uses `GraphView` from the published `PSGraph.Common` NuGet package.
- Includes working Vega exporters for the force-directed, adjacency-matrix, and tree-layout paths.
- Includes a working DSM Vega matrix exporter built on the extracted DSM node/edge payload builder.
- Includes an extracted DSM Vega data builder for reorderable matrix node/edge payloads.
- Includes an extracted DSM SVG exporter used by the legacy `DsmView` compatibility bridge.
- Includes GraphView-based MSAGL exporters for the fast-incremental and Sugiyama SVG paths, including Sugiyama label placement and SVG post-processing for tree-style layouts.
- Includes a GraphView-based MSAGL MDS exporter, so all MSAGL renderers now live in `PSGraphView`.
- Includes a managed `Sfdp` export pipeline with its own indexed graph, CSR, connected-components, packing, and `svg/png/jpg` output.
- Includes a shared `PSGraphView.GVExport` layer for scene, SVG, and raster export code.

Repository layout:
- `src/PSGraphView.Dsm`: DSM-specific visualization library for SVG rendering.
- `src/PSGraphView.Vega`: visualization library for `GraphView`-based Vega export.
- `src/PSGraphView.Msagl`: visualization library for `GraphView`-based MSAGL export.
- `src/PSGraphView.GVExport`: shared scene, SVG, and raster export primitives used by managed renderers.
- `src/PSGraphView.Sfdp`: managed `Sfdp`-style layout and SVG export pipeline.
- `src/PSGraphView.PowerShell`: PowerShell cmdlet surface over the extracted graph and DSM renderers.
- `tests/PSGraphView.Dsm.Tests`: focused tests for the extracted DSM SVG renderer.
- `tests/PSGraphView.Vega.Tests`: focused tests for the migrated force-directed path.
- `tests/PSGraphView.Msagl.Tests`: focused tests for the initial MSAGL migration path, including Sugiyama direction and label-placement regressions.
- `tests/PSGraphView.Sfdp.Tests`: focused tests for the managed `Sfdp` layout, overlap removal, routing, and managed export output.
- `tests/PSGraphView.PowerShell.Tests`: cmdlet-level tests for the new PowerShell surface.

Current PowerShell surface:
- `Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <renderer> [-As Html|Json|Svg|Png|Jpg] [-Path <file>]`
- `Export-DSMView -Dsm|Result|SequencedDsm <object> -Renderer <renderer> [-As Html|Json|Svg] [-Path <file>]`

PowerShell help:
- Command help markdown and external help XML are generated with `platyPS`.
- Regenerate them with `pwsh ./eng/Generate-PowerShellHelp.ps1`.

## Export-GraphView Sfdp

`Sfdp` is the managed node-link layout in `PSGraphView`. It now exports `Svg`, `Png`, and `Jpg`.

Basic example:

```powershell
Export-GraphView -Graph $graph `
  -Renderer Sfdp `
  -As Svg `
  -Path ./graph.svg `
  -ShowArrows `
  -NodeRadius 3 `
  -EdgeLineWidth 0.4
```

Raster example:

```powershell
Export-GraphView -Graph $graph `
  -Renderer Sfdp `
  -As Png `
  -Path ./graph.png `
  -ShowArrows `
  -NodeRadius 3 `
  -EdgeLineWidth 0.4
```

Common visual parameters for `Sfdp`:
- `-BackgroundColor <hex>`: background color for `Sfdp` `svg/png/jpg` output.
- `-ShowLabels`: render node labels. Omit it for unlabeled nodes.
- `-ShowArrows`: render arrowheads for directed edges.
- `-NodeRadius <double>`: node circle radius in output units.
- `-EdgeLineWidth <double>`: edge stroke width.
- `-EdgeColor <hex>`: edge and arrow color. Default is `#c0c0c0`.
- `-ArrowSize <double>`: arrowhead scale multiplier. Default is `1.0`.
- `-LabelFontSize <double>`: node label font size.
- `-LabelOffsetX <double>` / `-LabelOffsetY <double>`: base label offset before label-overlap adjustment.
- `-Width <double>` / `-Height <double>`: force output SVG size. If omitted, natural bounds are used.
- `-GroupMetadataKey <string>` and `-DisableGroupColors`: group-based node coloring.

`Sfdp` algorithm parameters:
- `-SfdpSeed <int>`: deterministic seed.
- `-SfdpMaxIterations <int>`: iteration cap for single-level solving.
- `-SfdpInitialStep <double>`: initial movement step.
- `-SfdpTolerance <double>`: convergence threshold.
- `-DisableSfdpAdaptiveCooling`: disable adaptive cooling.
- `-DisableSfdpBarnesHut`: disable quadtree acceleration entirely.
- `-SfdpQuadtreeMode <None|Normal|Fast|Hybrid>`: choose the quadtree path. Default is `Normal`, matching Graphviz more closely than the old single on/off switch.
- `-SfdpBarnesHutThreshold <int>`: minimum component size for quadtree acceleration. Default is `45`, matching Graphviz `quadtree_size` more closely.
- `-SfdpBarnesHutTheta <double>`: Barnes-Hut theta.
- `-SfdpQuadtreeHybridThreshold <int>`: for `Hybrid`, switch to `Fast` above this component size. Default is `10000`.
- `-SfdpQuadtreeMaxDepth <int>`: quadtree depth cap. Default is `10`, closer to Graphviz `max_qtree_level`.
- `-DisableSfdpMultilevel`: disable coarse-to-fine solving.
- `-SfdpMultilevelThreshold <int>`: optional minimum component size for multilevel recursion. Default is `4`, closer to Graphviz coarsening semantics than the old early stop at `64`.
- `-SfdpMaxMultilevelDepth <int>`: optional coarse-to-fine depth cap. By default, the managed renderer does not impose a small fixed depth limit.
- `-SfdpRefinementIterations <int>`: extra refinement iterations after prolongation.
- `-SfdpNaturalLength <double>`: override natural spring length.
- `-SfdpRepulsiveExponent <double>`: override repulsive exponent.
- `-DisableSfdpOverlapRemoval`: disable node overlap post-process.
- `-SfdpOverlapRemovalIterations <int>`: overlap removal iteration cap.
- `-SfdpOverlapRemovalPadding <double>`: extra gap between node circles during overlap removal.
- `-SfdpOverlapRemovalBoxUnits <OutputUnits|GraphvizPoints>`: choose how default overlap boxes are derived when half-size overrides are not set. `GraphvizPoints` is useful in Graphviz-compare demos.

Graphviz `sfdp`-like preset:

```powershell
Export-GraphView -Graph $subgraph `
  -Renderer Sfdp `
  -As Svg `
  -Path ./x-filtered-200.svg `
  -NodeRadius 1 `
  -EdgeLineWidth 0.2 `
  -EdgeColor '#00000018' `
  -ArrowSize 0.08 `
  -ShowArrows `
  -SfdpOverlapRemovalPadding 4 `
  -DisableGroupColors
```

This is only an approximation of the Graphviz CLI style. The managed `Sfdp` path is not a strict `1:1` port of Graphviz output, but it now exports `svg`, `png`, and `jpg` directly through the managed export pipeline.

Current mapping to a typical Graphviz command:

| Graphviz | Current PSGraphView analog |
| --- | --- |
| `-Nshape=circle` | Default `Sfdp` node shape is already a circle. |
| `-Nfixedsize=true` | Current `Sfdp` nodes are fixed-size circles driven by `-NodeRadius`. |
| `-Nwidth`, `-Nheight` | Approximate with `-NodeRadius`. Units do not match Graphviz inches exactly. |
| `-Nlabel=\"\"` | Omit `-ShowLabels`. |
| `-Earrowsize` | `-ArrowSize`. |
| `-Epenwidth` | `-EdgeLineWidth`. |
| `-Ecolor` | `-EdgeColor`. |
| `-Goverlap=prism` | Roughly similar intent: default overlap removal plus `-SfdpOverlapRemovalPadding`. Not the same algorithm. |
| `-Gsep=\"+4\"` | Roughly `-SfdpOverlapRemovalPadding 4`. |
| `-Goutputorder=edgesfirst` | Already how `SfdpSvgExporter` writes SVG groups. |
| `-Gdpi=220` | No direct `Sfdp` DPI parameter right now. `svg` stays vector; `png/jpg` use the managed raster pipeline defaults. |
| `-Tpng` | `-As Png -Path ./x.png` |
| `-Tjpg` | `-As Jpg -Path ./x.jpg` |
| `-o ./x.png` | `-Path ./x.png` |

Demo scripts:
- The scripts under `demos/` load installed `PSQuickGraph` and `PSGraphView` modules from `PSModulePath` by default.
- Install them for demo use with `Install-Module PSQuickGraph -Scope CurrentUser` and `Install-Module PSGraphView -Scope CurrentUser`.
- For local development before publishing, rerun a demo with `-UseLocalModules`; the demo loader will build a fresh `dotnet publish` output for `PSGraphView` and import the module from there.
- You can also point directly at build outputs with `-PSQuickGraphManifestPath <path-to-PSQuickGraph.psd1>` and `-PSGraphViewManifestPath <path-to-PSGraphView.psd1>`.

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
