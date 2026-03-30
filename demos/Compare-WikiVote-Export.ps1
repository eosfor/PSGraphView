# Compare-WikiVote-Export.ps1
# Builds graphviz and managed export baselines for svg/png/jpg on the same
# wiki-Vote graph. PNG/JPG managed exports are allowed to be missing for now.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-export-compare'),
    [switch]$UseSubgraph,
    [ValidateSet('ExpandedTopDegree', 'InducedTopDegree')]
    [string]$SubgraphMode = 'ExpandedTopDegree',
    [int]$SubgraphSeedCount = 30,
    [ValidateRange(1, 1000)]
    [int]$SubgraphStartVertexCount = 3,
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
        [string]$SubgraphMode,
        [int]$SubgraphSeedCount,
        [int]$SubgraphStartVertexCount
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
        return [pscustomobject]@{
            Graph = $fullGraph
            Selection = [ordered]@{
                Mode = 'Full'
                TargetVertexCount = $fullGraph.VertexCount
                StartVertexCount = $null
                ActualVertexCount = $fullGraph.VertexCount
                ActualEdgeCount = $fullGraph.EdgeCount
            }
        }
    }

    $rankedVertices = $fullGraph.Vertices |
        Sort-Object { $fullGraph.OutDegree($_) } -Descending |
        Select-Object

    $seedVertices = $rankedVertices | Select-Object -First $SubgraphStartVertexCount

    if ($SubgraphMode -eq 'InducedTopDegree') {
        Write-Host "Extracting induced top-degree subgraph (vertices=$SubgraphSeedCount)..."
        $includedVertices = $rankedVertices | Select-Object -First $SubgraphSeedCount
        $subgraph = New-Graph
        $included = [System.Collections.Generic.HashSet[string]]::new()

        foreach ($vertex in $includedVertices) {
            [void]$included.Add($vertex.Label)
        }

        foreach ($vertex in $includedVertices) {
            foreach ($edge in (Get-EnumeratedEdges -EdgeCollection (Get-OutEdge -Graph $fullGraph -Vertex $vertex.Label))) {
                if ($included.Contains($edge.Target.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }

            foreach ($edge in (Get-EnumeratedEdges -EdgeCollection (Get-InEdge -Graph $fullGraph -Vertex $vertex.Label))) {
                if ($included.Contains($edge.Source.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }
        }

        foreach ($vertex in $includedVertices) {
            Add-Vertex -Graph $subgraph -Vertex $vertex.Label -ErrorAction SilentlyContinue | Out-Null
        }

        Write-Host "Subgraph: $($subgraph.VertexCount) vertices, $($subgraph.EdgeCount) edges"
        return [pscustomobject]@{
            Graph = $subgraph
            Selection = [ordered]@{
                Mode = 'InducedTopDegree'
                TargetVertexCount = $SubgraphSeedCount
                StartVertexCount = $SubgraphStartVertexCount
                ActualVertexCount = $subgraph.VertexCount
                ActualEdgeCount = $subgraph.EdgeCount
            }
        }
    }

    Write-Host "Extracting expanded top-degree subgraph (target vertices=$SubgraphSeedCount, start vertices=$SubgraphStartVertexCount)..."

    $included = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $queue = [System.Collections.Generic.Queue[string]]::new()
    $queued = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

    foreach ($vertex in $seedVertices) {
        if ([string]::IsNullOrWhiteSpace($vertex.Label)) {
            continue
        }

        [void]$included.Add($vertex.Label)
        [void]$queue.Enqueue($vertex.Label)
        [void]$queued.Add($vertex.Label)
    }

    while ($queue.Count -gt 0 -and $included.Count -lt $SubgraphSeedCount) {
        $currentVertex = $queue.Dequeue()
        if ([string]::IsNullOrWhiteSpace($currentVertex)) {
            continue
        }

        foreach ($edge in (Get-EnumeratedEdges -EdgeCollection (Get-OutEdge -Graph $fullGraph -Vertex $currentVertex))) {
            if ($included.Count -ge $SubgraphSeedCount) {
                break
            }

            $targetLabel = $edge.Target.Label
            if ([string]::IsNullOrWhiteSpace($targetLabel)) {
                continue
            }

            if ($included.Add($targetLabel)) {
                if ($queued.Add($targetLabel)) {
                    [void]$queue.Enqueue($targetLabel)
                }
            }
        }

        foreach ($edge in (Get-EnumeratedEdges -EdgeCollection (Get-InEdge -Graph $fullGraph -Vertex $currentVertex))) {
            if ($included.Count -ge $SubgraphSeedCount) {
                break
            }

            $sourceLabel = $edge.Source.Label
            if ([string]::IsNullOrWhiteSpace($sourceLabel)) {
                continue
            }

            if ($included.Add($sourceLabel)) {
                if ($queued.Add($sourceLabel)) {
                    [void]$queue.Enqueue($sourceLabel)
                }
            }
        }
    }

    if ($included.Count -lt $SubgraphSeedCount) {
        foreach ($vertex in $rankedVertices) {
            if ($included.Count -ge $SubgraphSeedCount) {
                break
            }

            if ([string]::IsNullOrWhiteSpace($vertex.Label)) {
                continue
            }

            [void]$included.Add($vertex.Label)
        }
    }

    $subgraph = New-Graph
    foreach ($vertexLabel in $included) {
        Add-Vertex -Graph $subgraph -Vertex $vertexLabel -ErrorAction SilentlyContinue | Out-Null
    }

    foreach ($vertexLabel in $included) {
        foreach ($edge in (Get-EnumeratedEdges -EdgeCollection (Get-OutEdge -Graph $fullGraph -Vertex $vertexLabel))) {
            if ($included.Contains($edge.Target.Label)) {
                Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
            }
        }
    }

    Write-Host "Subgraph: $($subgraph.VertexCount) vertices, $($subgraph.EdgeCount) edges"
    return [pscustomobject]@{
        Graph = $subgraph
        Selection = [ordered]@{
            Mode = 'ExpandedTopDegree'
            TargetVertexCount = $SubgraphSeedCount
            StartVertexCount = $SubgraphStartVertexCount
            ActualVertexCount = $subgraph.VertexCount
            ActualEdgeCount = $subgraph.EdgeCount
        }
    }
}

function Get-EnumeratedEdges {
    param(
        $EdgeCollection
    )

    if ($null -eq $EdgeCollection) {
        return @()
    }

    if ($EdgeCollection -is [System.Collections.IEnumerable] -and $EdgeCollection -isnot [string]) {
        $edges = @()
        foreach ($edge in $EdgeCollection) {
            $edges += $edge
        }

        return $edges
    }

    return @($EdgeCollection)
}

Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

$selection = Get-WikiVoteGraph `
    -UseSubgraph:$UseSubgraph `
    -SubgraphMode $SubgraphMode `
    -SubgraphSeedCount $SubgraphSeedCount `
    -SubgraphStartVertexCount $SubgraphStartVertexCount
$graph = $selection.Graph
$graphLabel = if (-not $UseSubgraph) {
    'wiki-vote-full'
}
elseif ($selection.Selection.Mode -eq 'InducedTopDegree') {
    "wiki-vote-induced-$SubgraphSeedCount"
}
else {
    "wiki-vote-expanded-$SubgraphSeedCount-s$SubgraphStartVertexCount"
}
$runDir = Join-Path $OutputDir $graphLabel

$summary = Invoke-ExportComparisonRun `
    -Graph $graph `
    -GraphLabel $graphLabel `
    -OutputDir $runDir `
    -GraphvizSfdpPath $GraphvizSfdpPath `
    -SfdpSeed $SfdpSeed `
    -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
    -AllowPartial:$AllowPartial

$summary['Selection'] = $selection.Selection

$summaryPath = Join-Path $runDir "$graphLabel-comparison.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath

Write-Host ''
Write-Host '=== Export Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Graph             : $graphLabel"
Write-Host "Selection         : $($selection.Selection.Mode)"
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
