[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..' 'src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj'),
    [string]$MarkdownOutputPath = (Join-Path $PSScriptRoot '..' 'docs/powershell'),
    [string]$ExternalHelpOutputPath = (Join-Path $PSScriptRoot '..' 'src/PSGraphView.PowerShell/en-US')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Set-MarkdownContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [hashtable]$Replacements
    )

    $content = [System.IO.File]::ReadAllText($Path)

    foreach ($entry in $Replacements.GetEnumerator()) {
        $content = $content.Replace($entry.Key, $entry.Value)
    }

    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '\{\{ Fill ([^}]+) Description \}\}',
        'No additional description is available for this parameter yet.')

    $content = $content.Replace('{{ Add example description here }}', 'Example generated from the current command surface.')
    $content = $content.Replace('{{ Fill in the Synopsis }}', 'Help generated with platyPS.')
    $content = $content.Replace('{{ Fill in the Description }}', 'Help generated with platyPS.')

    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Update-ExportGraphViewHelp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $replacements = @{
        '{{ Fill in the Synopsis }}' = 'Exports a graph with one of the PSGraphView renderers.'
        '{{ Fill in the Description }}' = @'
Use this cmdlet to render a `PsBidirectionalGraph` through Vega, MSAGL, or the managed `Sfdp` pipeline.

The managed `Sfdp` renderer currently exports SVG only and exposes additional layout parameters for multilevel solving, Barnes-Hut acceleration, and overlap removal.
'@
        '{{ Add example description here }}' = 'Exports the graph with the managed Sfdp renderer to an SVG file.'
        '{{ Fill ArrowSize Description }}' = 'Scales SVG arrowheads for renderers that support arrows.'
        '{{ Fill As Description }}' = 'Selects the output format. Sfdp and MSAGL renderers currently support SVG output.'
        '{{ Fill BackgroundColor Description }}' = 'Sets the background fill color for SVG renderers.'
        '{{ Fill DisableGroupColors Description }}' = 'Disables group-based node coloring and uses the default node fill color.'
        '{{ Fill DisableSfdpAdaptiveCooling Description }}' = 'Turns off adaptive cooling in the managed Sfdp solver.'
        '{{ Fill DisableSfdpBarnesHut Description }}' = 'Turns off Barnes-Hut approximation for repulsive forces in Sfdp.'
        '{{ Fill DisableSfdpMultilevel Description }}' = 'Turns off the coarse-to-fine multilevel phase in Sfdp.'
        '{{ Fill DisableSfdpOverlapRemoval Description }}' = 'Turns off the Sfdp node overlap removal post-process.'
        '{{ Fill DisableSfdpPrincipalComponentRotation Description }}' = 'Turns off the automatic principal-component rotation pass that normalizes the final Sfdp orientation.'
        '{{ Fill EdgeColor Description }}' = 'Sets the SVG stroke color for edges and arrowheads in Sfdp.'
        '{{ Fill EdgeLineWidth Description }}' = 'Sets the SVG stroke width for edges.'
        '{{ Fill Graph Description }}' = 'Specifies the input graph to export.'
        '{{ Fill GroupMetadataKey Description }}' = 'Specifies the node metadata key used for group-based coloring.'
        '{{ Fill Height Description }}' = 'Sets the output height for SVG renderers.'
        '{{ Fill LabelFontSize Description }}' = 'Sets the SVG font size for node labels.'
        '{{ Fill LabelOffsetX Description }}' = 'Sets the base horizontal offset for label placement.'
        '{{ Fill LabelOffsetY Description }}' = 'Sets the base vertical offset for label placement.'
        '{{ Fill NodeRadius Description }}' = 'Sets the node circle radius for SVG renderers.'
        '{{ Fill Path Description }}' = 'Writes the rendered output to the specified file path.'
        '{{ Fill ProgressAction Description }}' = 'Controls how PowerShell progress records are handled.'
        '{{ Fill Renderer Description }}' = 'Selects the graph renderer to use.'
        '{{ Fill SfdpBarnesHutTheta Description }}' = 'Sets the Barnes-Hut theta value for Sfdp.'
        '{{ Fill SfdpBarnesHutThreshold Description }}' = 'Sets the minimum component size that enables Barnes-Hut in Sfdp.'
        '{{ Fill SfdpInitialStep Description }}' = 'Sets the initial movement step for the Sfdp solver.'
        '{{ Fill SfdpMaxIterations Description }}' = 'Sets the maximum number of iterations for the Sfdp solver.'
        '{{ Fill SfdpMaxMultilevelDepth Description }}' = 'Sets the maximum multilevel depth for Sfdp.'
        '{{ Fill SfdpMultilevelThreshold Description }}' = 'Sets the minimum component size that enables multilevel Sfdp.'
        '{{ Fill SfdpNaturalLength Description }}' = 'Overrides the natural spring length used by Sfdp.'
        '{{ Fill SfdpOverlapRemovalIterations Description }}' = 'Sets the maximum number of overlap removal passes in Sfdp.'
        '{{ Fill SfdpOverlapRemovalPadding Description }}' = 'Adds extra padding between nodes during Sfdp overlap removal.'
        '{{ Fill SfdpRefinementIterations Description }}' = 'Sets the number of refinement iterations after prolongation in Sfdp.'
        '{{ Fill SfdpRepulsiveExponent Description }}' = 'Overrides the repulsive exponent used by Sfdp.'
        '{{ Fill SfdpRotationDegrees Description }}' = 'Applies an additional clockwise rotation in degrees after the principal-component alignment pass.'
        '{{ Fill SfdpSeed Description }}' = 'Sets a deterministic random seed for Sfdp.'
        '{{ Fill SfdpSmoothing Description }}' = 'Selects an optional managed post-process smoothing mode for Sfdp. Supported modes currently include GraphDistance, AverageDistance, PowerDistance, and Spring.'
        '{{ Fill SfdpSmoothingIterations Description }}' = 'Sets the iteration count for the managed Sfdp smoothing pass.'
        '{{ Fill SfdpTolerance Description }}' = 'Sets the Sfdp convergence tolerance.'
        '{{ Fill ShowArrows Description }}' = 'Renders directional arrowheads when the selected renderer supports them.'
        '{{ Fill ShowLabels Description }}' = 'Renders node labels when the selected renderer supports them.'
        '{{ Fill SugiyamaDirection Description }}' = 'Selects horizontal or vertical flow for the MSAGL Sugiyama renderer.'
        '{{ Fill SugiyamaEdgeRouting Description }}' = 'Selects the edge routing mode for the MSAGL Sugiyama renderer.'
        '{{ Fill SugiyamaLayerSeparation Description }}' = 'Sets layer spacing for the MSAGL Sugiyama renderer.'
        '{{ Fill SugiyamaNodeSeparation Description }}' = 'Sets node spacing inside layers for the MSAGL Sugiyama renderer.'
        '{{ Fill UseVirtualTreeRoot Description }}' = 'Adds a virtual root for tree-oriented Vega rendering when needed.'
        '{{ Fill Width Description }}' = 'Sets the output width for SVG renderers.'
    }

    Set-MarkdownContent -Path $Path -Replacements $replacements

    $content = [System.IO.File]::ReadAllText($Path)
    $exampleBlock = @'
### Example 1
```powershell
PS C:\> Export-GraphView -Graph $graph -Renderer Sfdp -As Svg -Path ./graph.svg -ShowArrows -NodeRadius 3 -EdgeLineWidth 0.4
```
'@
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '### Example 1\r?\n```[\s\S]*?```\r?\n',
        $exampleBlock + [Environment]::NewLine,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $sectionDescriptions = @{
        'DisableSfdpPrincipalComponentRotation' = 'Turns off the automatic principal-component rotation pass that normalizes the final Sfdp orientation.'
        'SfdpRotationDegrees' = 'Applies an additional clockwise rotation in degrees after the principal-component alignment pass.'
        'SfdpSmoothing' = 'Selects an optional managed post-process smoothing mode for Sfdp. Supported modes currently include GraphDistance, AverageDistance, PowerDistance, and Spring.'
        'SfdpSmoothingIterations' = 'Sets the iteration count for the managed Sfdp smoothing pass.'
    }

    foreach ($section in $sectionDescriptions.GetEnumerator()) {
        $pattern = "### -$([System.Text.RegularExpressions.Regex]::Escape($section.Key))\r?\nNo additional description is available for this parameter yet\."
        $replacement = "### -$($section.Key)$([Environment]::NewLine)$($section.Value)"
        $content = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, $replacement)
    }

    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Update-ExportDsmViewHelp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $replacements = @{
        '{{ Fill in the Synopsis }}' = 'Exports a DSM view with one of the PSGraphView DSM renderers.'
        '{{ Fill in the Description }}' = @'
Use this cmdlet to render a plain, partitioned, or sequenced DSM through the SVG or Vega DSM renderers.
'@
        '{{ Add example description here }}' = 'Exports a plain DSM to an SVG matrix.'
        '{{ Fill As Description }}' = 'Selects the output format for the DSM renderer.'
        '{{ Fill Dsm Description }}' = 'Specifies a plain DSM instance to export.'
        '{{ Fill ItemSize Description }}' = 'Sets the rendered item size used by DSM renderers.'
        '{{ Fill Path Description }}' = 'Writes the rendered output to the specified file path.'
        '{{ Fill ProgressAction Description }}' = 'Controls how PowerShell progress records are handled.'
        '{{ Fill Renderer Description }}' = 'Selects the DSM renderer to use.'
        '{{ Fill Result Description }}' = 'Specifies a clustered or partitioned DSM result to export.'
        '{{ Fill SequencedDsm Description }}' = 'Specifies a sequenced DSM to export.'
    }

    Set-MarkdownContent -Path $Path -Replacements $replacements

    $content = [System.IO.File]::ReadAllText($Path)
    $exampleBlock = @'
### Example 1
```powershell
PS C:\> Export-DSMView -Dsm $dsm -Renderer DsmMatrixSvg -As Svg -Path ./matrix.svg
```
'@
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '### Example 1\r?\n```[\s\S]*?```\r?\n',
        $exampleBlock + [Environment]::NewLine,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

$projectPath = [System.IO.Path]::GetFullPath($ProjectPath)
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$markdownOutputPath = [System.IO.Path]::GetFullPath($MarkdownOutputPath)
$externalHelpOutputPath = [System.IO.Path]::GetFullPath($ExternalHelpOutputPath)
$publishPath = Join-Path ([System.IO.Path]::GetTempPath()) ("PSGraphView-help-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Force -Path $markdownOutputPath | Out-Null
    New-Item -ItemType Directory -Force -Path $externalHelpOutputPath | Out-Null

    dotnet publish $projectPath -o $publishPath | Out-Host

    Import-Module platyPS -Force
    Import-Module (Join-Path $publishPath 'PSGraphView.psd1') -Force

    New-MarkdownHelp -Command Export-GraphView, Export-DSMView -OutputFolder $markdownOutputPath -Force | Out-Null

    Update-ExportGraphViewHelp -Path (Join-Path $markdownOutputPath 'Export-GraphView.md')
    Update-ExportDsmViewHelp -Path (Join-Path $markdownOutputPath 'Export-DSMView.md')

    New-ExternalHelp -Path $markdownOutputPath -OutputPath $externalHelpOutputPath -Force | Out-Null
}
finally {
    Remove-Item -Recurse -Force $publishPath -ErrorAction Ignore
}
