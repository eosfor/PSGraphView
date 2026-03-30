# Compare-WikiVote-Export.ps1
# Builds graphviz and managed export baselines for svg/png/jpg on the same
# wiki-Vote graph. PNG/JPG managed exports are allowed to be missing for now.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-export-compare'),
    [switch]$UseSubgraph,
    [int]$SubgraphSeedCount = 30,
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

function Get-WikiVoteGraph {
    param(
        [switch]$UseSubgraph,
        [int]$SubgraphSeedCount
    )

    $dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
    Ensure-Directory -Path $dataDir

    $wikiVoteUrl = 'https://snap.stanford.edu/data/wiki-Vote.txt.gz'
    $wikiVoteGz = Join-Path $dataDir 'wiki-Vote.txt.gz'
    $wikiVoteTxt = Join-Path $dataDir 'wiki-Vote.txt'

    if (-not (Test-Path $wikiVoteTxt)) {
        Write-Host 'Downloading wiki-Vote dataset...'
        Invoke-WebRequest -Uri $wikiVoteUrl -OutFile $wikiVoteGz

        $inStream = [System.IO.File]::OpenRead($wikiVoteGz)
        $gzStream = [System.IO.Compression.GZipStream]::new($inStream, [System.IO.Compression.CompressionMode]::Decompress)
        $outStream = [System.IO.File]::Create($wikiVoteTxt)
        $gzStream.CopyTo($outStream)
        $outStream.Close()
        $gzStream.Close()
        $inStream.Close()
        Remove-Item $wikiVoteGz -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "Using cached $wikiVoteTxt"
    }

    $fullGraph = Import-Graph -Path $wikiVoteTxt -Format Csv -Delimiter "`t" -NoHeader
    Write-Host "Full graph: $($fullGraph.VertexCount) vertices, $($fullGraph.EdgeCount) edges"

    if (-not $UseSubgraph) {
        return $fullGraph
    }

    Write-Host "Extracting subgraph (seed=$SubgraphSeedCount highest-degree vertices)..."

    $seedVertices = $fullGraph.Vertices |
        Sort-Object { $fullGraph.OutDegree($_) } -Descending |
        Select-Object -First $SubgraphSeedCount

    $subgraph = New-Graph
    $included = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($vertex in $seedVertices) {
        [void]$included.Add($vertex.Label)
    }

    foreach ($vertex in $seedVertices) {
        foreach ($edge in @(Get-OutEdge -Graph $fullGraph -Vertex $vertex.Label)) {
            if ($included.Contains($edge.Target.Label)) {
                Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
            }
        }

        foreach ($edge in @(Get-InEdge -Graph $fullGraph -Vertex $vertex.Label)) {
            if ($included.Contains($edge.Source.Label)) {
                Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
            }
        }
    }

    foreach ($vertex in $seedVertices) {
        Add-Vertex -Graph $subgraph -Vertex $vertex.Label -ErrorAction SilentlyContinue | Out-Null
    }

    Write-Host "Subgraph: $($subgraph.VertexCount) vertices, $($subgraph.EdgeCount) edges"
    return $subgraph
}

Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

$graph = Get-WikiVoteGraph -UseSubgraph:$UseSubgraph -SubgraphSeedCount $SubgraphSeedCount
$graphLabel = if ($UseSubgraph) { "wiki-vote-subgraph-$SubgraphSeedCount" } else { 'wiki-vote-full' }
$runDir = Join-Path $OutputDir $graphLabel

$summary = Invoke-ExportComparisonRun `
    -Graph $graph `
    -GraphLabel $graphLabel `
    -OutputDir $runDir `
    -GraphvizSfdpPath $GraphvizSfdpPath `
    -SfdpSeed $SfdpSeed `
    -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
    -AllowPartial:$AllowPartial

$summaryPath = Join-Path $runDir "$graphLabel-comparison.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath

Write-Host ''
Write-Host '=== Export Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Graph             : $graphLabel"
Write-Host "Vertices / Edges  : $($summary.Graph.VertexCount) / $($summary.Graph.EdgeCount)"
Write-Host "Graphviz SVG      : $($summary.Graphviz.Outputs.Svg.Path)"
Write-Host "Managed  SVG      : $($summary.Managed.Outputs.Svg.Path)"
Write-Host "Graphviz PNG      : $($summary.Graphviz.Outputs.Png.Path)"
Write-Host "Managed  PNG      : $($summary.Managed.Outputs.Png.Path)"
Write-Host "Graphviz JPG      : $($summary.Graphviz.Outputs.Jpg.Path)"
Write-Host "Managed  JPG      : $($summary.Managed.Outputs.Jpg.Path)"
Write-Host "Summary           : $summaryPath"

if (-not $summary.Managed.Outputs.Png.Supported) {
    Write-Host "Managed PNG       : $($summary.Managed.Outputs.Png.Error)"
}

if (-not $summary.Managed.Outputs.Jpg.Supported) {
    Write-Host "Managed JPG       : $($summary.Managed.Outputs.Jpg.Error)"
}
