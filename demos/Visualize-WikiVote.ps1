# Visualize-WikiVote.ps1
# Visualize a subset of the SNAP wiki-Vote directed network using PSGraphView.
# The full dataset has ~7k nodes and ~100k edges, which is too large for
# node-link layouts. This script imports the full graph, extracts a small
# subgraph (configurable), and renders it with multiple layouts.
#
# For the full graph we also produce a Vega adjacency matrix which handles
# large graphs better than node-link diagrams.
#
# Produces:
#   - Vega force-directed of the subgraph (HTML)
#   - MSAGL MDS of the subgraph (SVG)
#   - MSAGL Sugiyama of the subgraph (SVG)
#   - DSM plain, clustered, and sequenced matrices of the subgraph

param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demos'),
    [int]$SubgraphSeedCount = 30
)

$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$psGraphRoot  = Join-Path (Split-Path -Parent $repoRoot) 'PSGraph'
$psQuickGraphModuleRoot = Join-Path $psGraphRoot 'PSGraph.Tests/bin/Debug/net9.0'
$psGraphViewModuleRoot = Join-Path $repoRoot 'tests/PSGraphView.PowerShell.Tests/bin/Debug/net9.0'
$psQuickGraphManifest = Join-Path $psQuickGraphModuleRoot 'PSQuickGraph.psd1'
$psGraphViewManifest = Join-Path $psGraphViewModuleRoot 'PSGraphView.psd1'

if (-not (Test-Path $psQuickGraphManifest)) {
    throw "Expected PSQuickGraph module manifest was not found at '$psQuickGraphManifest'. Build PSGraph.Tests first."
}

if (-not (Test-Path $psGraphViewManifest)) {
    throw "Expected PSGraphView module manifest was not found at '$psGraphViewManifest'. Build PSGraphView.PowerShell.Tests first."
}

Remove-Module PSQuickGraph, PSGraphView -ErrorAction SilentlyContinue
Import-Module $psQuickGraphManifest -Force
Import-Module $psGraphViewManifest -Force

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "`n=== wiki-Vote Visualization ===" -ForegroundColor Cyan

# --- Download dataset ---
$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }

$wikiVoteUrl = 'https://snap.stanford.edu/data/wiki-Vote.txt.gz'
$wikiVoteGz  = Join-Path $dataDir 'wiki-Vote.txt.gz'
$wikiVoteTxt = Join-Path $dataDir 'wiki-Vote.txt'

if (-not (Test-Path $wikiVoteTxt)) {
    Write-Host 'Downloading wiki-Vote dataset...'
    Invoke-WebRequest -Uri $wikiVoteUrl -OutFile $wikiVoteGz

    $inStream  = [System.IO.File]::OpenRead($wikiVoteGz)
    $gzStream  = [System.IO.Compression.GZipStream]::new($inStream, [System.IO.Compression.CompressionMode]::Decompress)
    $outStream = [System.IO.File]::Create($wikiVoteTxt)
    $gzStream.CopyTo($outStream)
    $outStream.Close(); $gzStream.Close(); $inStream.Close()
    Remove-Item $wikiVoteGz -ErrorAction SilentlyContinue
} else {
    Write-Host "Using cached $wikiVoteTxt"
}

$fullGraph = Import-Graph -Path $wikiVoteTxt -Format Csv -Delimiter "`t" -NoHeader
Write-Host "Full graph: $($fullGraph.VertexCount) vertices, $($fullGraph.EdgeCount) edges"

# --- Extract a manageable subgraph ---
# Pick seed vertices with the most outgoing edges, then include their 1-hop neighbors.
Write-Host "Extracting subgraph (seed=$SubgraphSeedCount highest-degree vertices)..."

$seedVertices = $fullGraph.Vertices |
    Sort-Object { $fullGraph.OutDegree($_) } -Descending |
    Select-Object -First $SubgraphSeedCount

$subgraph = New-Graph
$included = [System.Collections.Generic.HashSet[string]]::new()

foreach ($v in $seedVertices) {
    [void]$included.Add($v.Label)
}

foreach ($v in $seedVertices) {
    $outEdges = Get-OutEdge -Graph $fullGraph -Vertex $v.Label
    if ($outEdges) {
        foreach ($e in $outEdges) {
            if ($included.Contains($e.Target.Label)) {
                Add-Edge -From $e.Source.Label -To $e.Target.Label -Graph $subgraph | Out-Null
            }
        }
    }
    $inEdges = Get-InEdge -Graph $fullGraph -Vertex $v.Label
    if ($inEdges) {
        foreach ($e in $inEdges) {
            if ($included.Contains($e.Source.Label)) {
                Add-Edge -From $e.Source.Label -To $e.Target.Label -Graph $subgraph | Out-Null
            }
        }
    }
}

# Ensure all seed vertices present even if isolated in subgraph
foreach ($v in $seedVertices) {
    Add-Vertex -Graph $subgraph -Vertex $v.Label -ErrorAction SilentlyContinue | Out-Null
}

Write-Host "Subgraph: $($subgraph.VertexCount) vertices, $($subgraph.EdgeCount) edges"

# --- 1. Vega Force-Directed (subgraph, interactive HTML) ---
$outFile = Join-Path $OutputDir 'wiki-vote-force.html'
Export-GraphView -Graph $subgraph `
    -Renderer VegaForceDirected `
    -As Html `
    -Path $outFile `
    -ShowLabels `
    -ShowArrows `
    -NodeRadius 4 `
    -LabelFontSize 8
Write-Host "[1/7] Force-directed (subgraph): $outFile" -ForegroundColor Green

# --- 2. Vega Adjacency Matrix (subgraph, interactive HTML) ---
$outFile = Join-Path $OutputDir 'wiki-vote-matrix.html'
Export-GraphView -Graph $subgraph `
    -Renderer VegaAdjacencyMatrix `
    -As Html `
    -Path $outFile
Write-Host "[2/7] Adjacency matrix (subgraph): $outFile" -ForegroundColor Green

# --- 3. MSAGL MDS (subgraph, SVG) ---
$outFile = Join-Path $OutputDir 'wiki-vote-mds.svg'
Export-GraphView -Graph $subgraph `
    -Renderer MsaglMds `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -ShowArrows `
    -NodeRadius 3 `
    -LabelFontSize 7
Write-Host "[3/7] MDS layout (subgraph): $outFile" -ForegroundColor Green

# --- 4. MSAGL Sugiyama (subgraph, SVG) ---
$outFile = Join-Path $OutputDir 'wiki-vote-sugiyama.svg'
Export-GraphView -Graph $subgraph `
    -Renderer MsaglSugiyama `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -ShowArrows `
    -SugiyamaDirection Horizontal `
    -SugiyamaNodeSeparation 10
Write-Host "[4/7] Sugiyama layout (subgraph): $outFile" -ForegroundColor Green

# --- 5. DSM plain matrix ---
$dsm = New-DSM -Graph $subgraph
$outFile = Join-Path $OutputDir 'wiki-vote-dsm.svg'
Export-DSMView -Dsm $dsm `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile
Write-Host "[5/7] DSM plain (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'wiki-vote-dsm.html'
Export-DSMView -Dsm $dsm `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile
Write-Host "       DSM plain (Vega HTML): $outFile" -ForegroundColor Green

# --- 6. DSM after clustering ---
$clustered = Start-DSMClustering -Dsm $dsm
$outFile = Join-Path $OutputDir 'wiki-vote-dsm-clustered.svg'
Export-DSMView -Result $clustered `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile
Write-Host "[6/7] DSM clustered (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'wiki-vote-dsm-clustered.html'
Export-DSMView -Result $clustered `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile
Write-Host "       DSM clustered (Vega HTML): $outFile" -ForegroundColor Green

# --- 7. DSM after sequencing ---
$sequenced = Start-DSMSequencing -Dsm $dsm
$outFile = Join-Path $OutputDir 'wiki-vote-dsm-sequenced.svg'
Export-DSMView -SequencedDsm $sequenced `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile
Write-Host "[7/7] DSM sequenced (SVG): $outFile" -ForegroundColor Green

Write-Host "`nAll outputs in: $OutputDir" -ForegroundColor Cyan
Write-Host "Open the .html files in a browser for interactive exploration.`n"
