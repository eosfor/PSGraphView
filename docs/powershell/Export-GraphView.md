---
external help file: PSGraphView.PowerShell.dll-Help.xml
Module Name: PSGraphView
online version: https://github.com/eosfor/PSGraphView#export-graphview-sfdp
schema: 2.0.0
---

# Export-GraphView

## SYNOPSIS
Exports a graph with one of the PSGraphView renderers.

## SYNTAX

### Graph (Default)
```
Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <GraphViewRenderer> [-As <ViewOutputKind>]
 [-Path <String>] [-UseVirtualTreeRoot] [-BackgroundColor <String>] [-ShowLabels] [-ShowArrows]
 [-NodeRadius <Double>] [-EdgeLineWidth <Double>] [-LabelFontSize <Double>] [-Width <Double>]
 [-Height <Double>] [-GroupMetadataKey <String>] [-DisableGroupColors] [-LabelOffsetX <Double>]
 [-LabelOffsetY <Double>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Sfdp
```
Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <GraphViewRenderer> [-As <ViewOutputKind>]
 [-Path <String>] [-UseVirtualTreeRoot] [-BackgroundColor <String>] [-ShowLabels] [-ShowArrows]
 [-NodeRadius <Double>] [-EdgeLineWidth <Double>] [-EdgeColor <String>] [-ArrowSize <Double>]
 [-LabelFontSize <Double>] [-Width <Double>] [-Height <Double>] [-GroupMetadataKey <String>]
 [-DisableGroupColors] [-LabelOffsetX <Double>] [-LabelOffsetY <Double>] [-SfdpSeed <Int32>]
 [-SfdpMaxIterations <Int32>] [-SfdpInitialStep <Double>] [-SfdpTolerance <Double>]
 [-DisableSfdpAdaptiveCooling] [-DisableSfdpBarnesHut] [-SfdpQuadtreeMode <SfdpQuadtreeMode>]
 [-SfdpBarnesHutThreshold <Int32>] [-SfdpBarnesHutTheta <Double>] [-SfdpQuadtreeHybridThreshold <Int32>]
 [-SfdpQuadtreeMaxDepth <Int32>] [-DisableSfdpMultilevel] [-SfdpMultilevelThreshold <Int32>]
 [-SfdpMaxMultilevelDepth <Int32>] [-SfdpRefinementIterations <Int32>] [-SfdpNaturalLength <Double>]
 [-SfdpRepulsiveExponent <Double>] [-SfdpSmoothing <SfdpSmoothingMode>] [-SfdpSmoothingIterations <Int32>]
 [-DisableSfdpPrincipalComponentRotation] [-SfdpRotationDegrees <Double>] [-DisableSfdpOverlapRemoval]
 [-SfdpOverlapRemovalIterations <Int32>] [-SfdpOverlapRemovalPadding <Double>]
 [-SfdpOverlapRemovalHalfWidth <Double>] [-SfdpOverlapRemovalHalfHeight <Double>]
 [-SfdpDiagnosticsPath <String>] [-SfdpDiagnosticsFormat <SfdpDiagnosticFormat>]
 [-DisableSfdpDiagnosticsIterations] [-SfdpDiagnosticsIncludeCoordinates] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Msagl
```
Export-GraphView -Graph <PsBidirectionalGraph> -Renderer <GraphViewRenderer> [-As <ViewOutputKind>]
 [-Path <String>] [-UseVirtualTreeRoot] [-BackgroundColor <String>] [-ShowLabels] [-ShowArrows]
 [-NodeRadius <Double>] [-EdgeLineWidth <Double>] [-LabelFontSize <Double>] [-Width <Double>]
 [-Height <Double>] [-GroupMetadataKey <String>] [-DisableGroupColors] [-SugiyamaDirection <String>]
 [-SugiyamaLayerSeparation <Double>] [-SugiyamaNodeSeparation <Double>] [-SugiyamaEdgeRouting <String>]
 [-LabelOffsetX <Double>] [-LabelOffsetY <Double>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to render a \`PsBidirectionalGraph\` through Vega, MSAGL, or the managed \`Sfdp\` pipeline.

The managed \`Sfdp\` renderer exports \`Svg\`, \`Png\`, and \`Jpg\`, and exposes additional layout parameters for multilevel solving, Barnes-Hut acceleration, and overlap removal.

## EXAMPLES

### Example 1
```powershell
PS C:\> Export-GraphView -Graph $graph -Renderer Sfdp -As Svg -Path ./graph.svg -ShowArrows -NodeRadius 3 -EdgeLineWidth 0.4
```

Exports the graph with the managed Sfdp renderer to an SVG file.

### Example 2
```powershell
PS C:\> Export-GraphView -Graph $graph -Renderer Sfdp -As Png -Path ./graph.png -ShowArrows -NodeRadius 3 -EdgeLineWidth 0.4
```

Exports the graph with the managed Sfdp renderer to a PNG file.

## PARAMETERS

### -ArrowSize
Scales SVG arrowheads for renderers that support arrows.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -As
Selects the output format.
Sfdp supports Svg, Png, and Jpg. MSAGL supports Svg. Vega renderers support Json and Html.

```yaml
Type: ViewOutputKind
Parameter Sets: (All)
Aliases:
Accepted values: Json, Html, Svg, Png, Jpg

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BackgroundColor
Sets the background fill color for Sfdp Svg/Png/Jpg output and for other SVG renderers.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableGroupColors
Disables group-based node coloring and uses the default node fill color.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpAdaptiveCooling
Turns off adaptive cooling in the managed Sfdp solver.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpBarnesHut
Turns off Barnes-Hut approximation for repulsive forces in Sfdp.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpMultilevel
Turns off the coarse-to-fine multilevel phase in Sfdp.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpOverlapRemoval
Turns off the Sfdp node overlap removal post-process.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -EdgeColor
Sets the stroke color for edges and arrowheads in Sfdp output.

```yaml
Type: String
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EdgeLineWidth
Sets the stroke width for edges.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Graph
Specifies the input graph to export.

```yaml
Type: PsBidirectionalGraph
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GroupMetadataKey
Specifies the node metadata key used for group-based coloring.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height
Sets the output height for Sfdp and other renderers that respect an explicit output size.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LabelFontSize
Sets the font size for node labels.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LabelOffsetX
Sets the base horizontal offset for label placement.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LabelOffsetY
Sets the base vertical offset for label placement.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NodeRadius
Sets the node circle radius for Sfdp output.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Writes the rendered output to the specified file path.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Renderer
Selects the graph renderer to use.

```yaml
Type: GraphViewRenderer
Parameter Sets: (All)
Aliases:
Accepted values: VegaForceDirected, VegaAdjacencyMatrix, VegaTreeLayout, Sfdp, MsaglMds, MsaglFastIncremental, MsaglSugiyama

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpBarnesHutTheta
Sets the Barnes-Hut theta value for Sfdp.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpBarnesHutThreshold
Sets the minimum component size that enables Barnes-Hut in Sfdp.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpInitialStep
Sets the initial movement step for the Sfdp solver.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpMaxIterations
Sets the maximum number of iterations for the Sfdp solver.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpMaxMultilevelDepth
Sets an optional maximum multilevel depth for Sfdp.
By default, the managed renderer does not use a small fixed depth cap.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpMultilevelThreshold
Sets an optional minimum component size for multilevel recursion.
The default is 4, which is closer to Graphviz coarsening semantics than the old early stop at 64.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpNaturalLength
Overrides the natural spring length used by Sfdp.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpOverlapRemovalIterations
Sets the maximum number of overlap removal passes in Sfdp.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpOverlapRemovalPadding
Adds extra padding between nodes during Sfdp overlap removal.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpRefinementIterations
Sets the number of refinement iterations after prolongation in Sfdp.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpRepulsiveExponent
Overrides the repulsive exponent used by Sfdp.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpSeed
Sets a deterministic random seed for Sfdp.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpTolerance
Sets the Sfdp convergence tolerance.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ShowArrows
Renders directional arrowheads when the selected renderer supports them.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -ShowLabels
Renders node labels when the selected renderer supports them.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SugiyamaDirection
Selects horizontal or vertical flow for the MSAGL Sugiyama renderer.

```yaml
Type: String
Parameter Sets: Msagl
Aliases:
Accepted values: Horizontal, Vertical

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SugiyamaEdgeRouting
Selects the edge routing mode for the MSAGL Sugiyama renderer.

```yaml
Type: String
Parameter Sets: Msagl
Aliases:
Accepted values: SugiyamaSplines, Spline, StraightLine, Rectilinear, RectilinearToCenter, None, SplineBundling

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SugiyamaLayerSeparation
Sets layer spacing for the MSAGL Sugiyama renderer.

```yaml
Type: Double
Parameter Sets: Msagl
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SugiyamaNodeSeparation
Sets node spacing inside layers for the MSAGL Sugiyama renderer.

```yaml
Type: Double
Parameter Sets: Msagl
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseVirtualTreeRoot
Adds a virtual root for tree-oriented Vega rendering when needed.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width
Sets the output width for Sfdp and other renderers that respect an explicit output size.

```yaml
Type: Double
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
Controls how PowerShell progress records are handled.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpPrincipalComponentRotation
Turns off the automatic principal-component rotation pass that normalizes the final Sfdp orientation.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpRotationDegrees
Applies an additional clockwise rotation in degrees after the principal-component alignment pass.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpSmoothing
Selects an optional managed post-process smoothing mode for Sfdp.

```yaml
Type: SfdpSmoothingMode
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpSmoothingIterations
Sets the iteration count for the managed Sfdp smoothing pass.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisableSfdpDiagnosticsIterations
No additional description is available for this parameter yet.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpDiagnosticsFormat
No additional description is available for this parameter yet.

```yaml
Type: SfdpDiagnosticFormat
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpDiagnosticsIncludeCoordinates
No additional description is available for this parameter yet.

```yaml
Type: SwitchParameter
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpDiagnosticsPath
No additional description is available for this parameter yet.

```yaml
Type: String
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpOverlapRemovalHalfHeight
No additional description is available for this parameter yet.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpOverlapRemovalHalfWidth
No additional description is available for this parameter yet.

```yaml
Type: Double
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpQuadtreeHybridThreshold
No additional description is available for this parameter yet.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpQuadtreeMaxDepth
No additional description is available for this parameter yet.

```yaml
Type: Int32
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SfdpQuadtreeMode
No additional description is available for this parameter yet.

```yaml
Type: SfdpQuadtreeMode
Parameter Sets: Sfdp
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
