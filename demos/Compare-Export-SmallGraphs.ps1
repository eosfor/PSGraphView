# Compare-Export-SmallGraphs.ps1
# Runs the export compare harness on a deterministic set of small graphs so
# early SVG/raster mismatches are easier to localize than on WikiVote.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-export-small-compare'),
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizSfdpPath,
    [int]$SfdpSeed = 42,
    [ValidateRange(1, 100000)]
    [int]$SfdpOverlapRemovalIterations = 1000,
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
        Name = 'single-edge'
        Graph = New-GraphFromDefinition -Edges @([pscustomobject]@{ From = 'a'; To = 'b' })
    }
    [ordered]@{
        Name = 'bidirectional-edge'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'a'; To = 'b' }
            [pscustomobject]@{ From = 'b'; To = 'a' }
        )
    }
    [ordered]@{
        Name = 'self-loop'
        Graph = New-GraphFromDefinition -Edges @([pscustomobject]@{ From = 'a'; To = 'a' })
    }
    [ordered]@{
        Name = 'triangle-cycle'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'a'; To = 'b' }
            [pscustomobject]@{ From = 'b'; To = 'c' }
            [pscustomobject]@{ From = 'c'; To = 'a' }
        )
    }
    [ordered]@{
        Name = 'disconnected-components'
        Graph = New-GraphFromDefinition -Vertices @('isolated') -Edges @(
            [pscustomobject]@{ From = 'a'; To = 'b' }
            [pscustomobject]@{ From = 'c'; To = 'd' }
        )
    }
    [ordered]@{
        Name = 'star'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'hub'; To = 'n1' }
            [pscustomobject]@{ From = 'hub'; To = 'n2' }
            [pscustomobject]@{ From = 'hub'; To = 'n3' }
            [pscustomobject]@{ From = 'hub'; To = 'n4' }
            [pscustomobject]@{ From = 'hub'; To = 'n5' }
        )
    }
)

$caseSummaries = @()

foreach ($case in $cases) {
    $caseName = $case.Name
    $graph = $case.Graph
    $runDir = Join-Path $OutputDir $caseName

    Write-Host "Running export comparison for '$caseName'..."

    $summary = Invoke-ExportComparisonRun `
        -Graph $graph `
        -GraphLabel $caseName `
        -OutputDir $runDir `
        -GraphvizSfdpPath $GraphvizSfdpPath `
        -SfdpSeed $SfdpSeed `
        -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
        -AllowPartial:$AllowPartial

    $summaryPath = Join-Path $runDir "$caseName-comparison.json"
    $summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath

    $caseSummaries += [ordered]@{
        Name = $caseName
        VertexCount = $summary.Graph.VertexCount
        EdgeCount = $summary.Graph.EdgeCount
        GraphvizSvgPath = $summary.Graphviz.Outputs.Svg.Path
        ManagedSvgPath = $summary.Managed.Outputs.Svg.Path
        ManagedPngSupported = $summary.Managed.Outputs.Png.Supported
        ManagedJpgSupported = $summary.Managed.Outputs.Jpg.Supported
        SvgComparison = $summary.Comparisons.Svg
        SummaryPath = $summaryPath
    }
}

$overview = [ordered]@{
    GeneratedAt = (Get-Date).ToString('o')
    GraphvizSfdpPath = $GraphvizSfdpPath
    Seed = $SfdpSeed
    OverlapRemovalIterations = $SfdpOverlapRemovalIterations
    CaseCount = $caseSummaries.Count
    Cases = $caseSummaries
}

$overviewPath = Join-Path $OutputDir 'small-graphs-overview.json'
$overview | ConvertTo-Json -Depth 8 | Set-Content -Path $overviewPath

Write-Host ''
Write-Host '=== Small Graph Export Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Cases             : $($overview.CaseCount)"
Write-Host "Graphviz sfdp     : $GraphvizSfdpPath"
Write-Host "Overview          : $overviewPath"
