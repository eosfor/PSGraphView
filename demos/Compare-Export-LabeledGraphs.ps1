# Compare-Export-LabeledGraphs.ps1
# Runs the export compare harness on a deterministic set of small labeled graphs
# so text/font/label mismatches are easier to inspect than on large graphs.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-export-labeled-compare'),
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizSfdpPath,
    [int]$SfdpSeed = 42,
    [ValidateRange(1, 100000)]
    [int]$SfdpOverlapRemovalIterations = 1000,
    [ValidateRange(4.0, 64.0)]
    [double]$LabelFontSize = 14.0,
    [switch]$AllowPartial = $true
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Compare-Export.Common.ps1')
. (Join-Path $PSScriptRoot 'Import-DemoModules.ps1')

if (-not $PSBoundParameters.ContainsKey('GraphvizSfdpPath') -or [string]::IsNullOrWhiteSpace($GraphvizSfdpPath)) {
    $GraphvizSfdpPath = Get-DefaultGraphvizSfdpPath
}

function New-GraphFromDefinition {
    param(
        [string[]]$Vertices = @(),
        [object[]]$Edges = @()
    )

    $graph = New-Graph

    foreach ($vertex in $Vertices) {
        Add-Vertex -Graph $graph -Vertex $vertex -ErrorAction SilentlyContinue | Out-Null
    }

    foreach ($edge in $Edges) {
        Add-Edge -From $edge.From -To $edge.To -Graph $graph | Out-Null
    }

    return $graph
}

Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

Ensure-Directory -Path $OutputDir

$cases = @(
    [ordered]@{
        Name = 'single-edge-labeled'
        Graph = New-GraphFromDefinition -Edges @([pscustomobject]@{ From = 'AlphaService'; To = 'BetaDatabase' })
    }
    [ordered]@{
        Name = 'triangle-cycle-labeled'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'Alpha API'; To = 'Beta Worker' }
            [pscustomobject]@{ From = 'Beta Worker'; To = 'Gamma Cache' }
            [pscustomobject]@{ From = 'Gamma Cache'; To = 'Alpha API' }
        )
    }
    [ordered]@{
        Name = 'star-labeled'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'North Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'South Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'East Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'West Region' }
        )
    }
)

$caseSummaries = @()

foreach ($case in $cases) {
    $caseName = $case.Name
    $graph = $case.Graph
    $runDir = Join-Path $OutputDir $caseName

    Write-Host "Running labeled export comparison for '$caseName'..."

    $summary = Invoke-ExportComparisonRun `
        -Graph $graph `
        -GraphLabel $caseName `
        -OutputDir $runDir `
        -GraphvizSfdpPath $GraphvizSfdpPath `
        -SfdpSeed $SfdpSeed `
        -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
        -IncludeLabels `
        -LabelFontSize $LabelFontSize `
        -AllowPartial:$AllowPartial

    $summaryPath = Join-Path $runDir "$caseName-comparison.json"
    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath

    $caseSummaries += [ordered]@{
        Name = $caseName
        VertexCount = $summary.Graph.VertexCount
        EdgeCount = $summary.Graph.EdgeCount
        GraphvizSvgPath = $summary.Graphviz.Outputs.Svg.Path
        ManagedSvgPath = $summary.Managed.Outputs.Svg.Path
        TextComparison = $summary.Comparisons.Text
        FontResolution = $summary.Comparisons.FontResolution
        PngComparison = [ordered]@{
            WidthDelta = $summary.Comparisons.Png.WidthDelta
            HeightDelta = $summary.Comparisons.Png.HeightDelta
            DarkPixelDelta = $summary.Comparisons.Png.DarkPixelDelta
            DarkPixelDensityDelta = $summary.Comparisons.Png.DarkPixelDensityDelta
            NonWhitePixelDensityDelta = $summary.Comparisons.Png.NonWhitePixelDensityDelta
        }
        JpgComparison = [ordered]@{
            WidthDelta = $summary.Comparisons.Jpg.WidthDelta
            HeightDelta = $summary.Comparisons.Jpg.HeightDelta
            DarkPixelDelta = $summary.Comparisons.Jpg.DarkPixelDelta
            DarkPixelDensityDelta = $summary.Comparisons.Jpg.DarkPixelDensityDelta
            NonWhitePixelDensityDelta = $summary.Comparisons.Jpg.NonWhitePixelDensityDelta
        }
        SummaryPath = $summaryPath
    }
}

$overview = [ordered]@{
    GeneratedAt = (Get-Date).ToString('o')
    GraphvizSfdpPath = $GraphvizSfdpPath
    Seed = $SfdpSeed
    OverlapRemovalIterations = $SfdpOverlapRemovalIterations
    LabelFontSize = $LabelFontSize
    CaseCount = $caseSummaries.Count
    Cases = $caseSummaries
}

$overviewPath = Join-Path $OutputDir 'labeled-graphs-overview.json'
$overview | ConvertTo-Json -Depth 10 | Set-Content -Path $overviewPath

Write-Host ''
Write-Host '=== Labeled Graph Export Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Cases             : $($overview.CaseCount)"
Write-Host "Graphviz sfdp     : $GraphvizSfdpPath"
Write-Host "Overview          : $overviewPath"
