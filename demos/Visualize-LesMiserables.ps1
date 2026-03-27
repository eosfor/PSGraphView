# Visualize-LesMiserables.ps1
# Visualize the D3.js Les Miserables co-appearance network using PSGraphView.
# 77 characters, 254 links, grouped by community.
#
# Produces interactive HTML files with several layouts:
#   - Vega force-directed (with group-based coloring)
#   - Vega adjacency matrix
#   - MSAGL MDS (multidimensional scaling)
#   - MSAGL Sugiyama (hierarchical)
#   - DSM plain matrix (SVG + Vega HTML)
#   - DSM after clustering with partition boundaries
#   - DSM after sequencing

param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demos')
)

$ErrorActionPreference = 'Stop'

# Resolve paths relative to repository roots
$repoRoot     = Split-Path -Parent $PSScriptRoot                      # PSGraphView repo root
$psGraphRoot  = Join-Path (Split-Path -Parent $repoRoot) 'PSGraph'    # sibling PSGraph repo

Import-Module "$psGraphRoot/PSGraph.Tests/bin/Debug/net9.0/PSQuickGraph.psd1" -Force
Import-Module "$repoRoot/tests/PSGraphView.PowerShell.Tests/bin/Debug/net9.0/PSGraphView.psd1" -Force

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "`n=== Les Miserables Visualization ===" -ForegroundColor Cyan

# --- Download dataset ---
$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }

$lesMisUrl  = 'https://raw.githubusercontent.com/d3/d3-plugins/master/graph/data/miserables.json'
$lesMisJson = Join-Path $dataDir 'miserables.json'

if (-not (Test-Path $lesMisJson)) {
    Write-Host 'Downloading Les Miserables dataset...'
    Invoke-WebRequest -Uri $lesMisUrl -OutFile $lesMisJson
} else {
    Write-Host "Using cached $lesMisJson"
}

$graph = Import-Graph -Path $lesMisJson -Format Json
Write-Host "Loaded: $($graph.VertexCount) vertices, $($graph.EdgeCount) edges"

# --- 1. Vega Force-Directed (interactive HTML) ---
$outFile = Join-Path $OutputDir 'les-miserables-force.html'
Export-GraphView -Graph $graph `
    -Renderer VegaForceDirected `
    -As Html `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 5 `
    -LabelFontSize 9 `
    -GroupMetadataKey 'group'
Write-Host "[1/7] Force-directed: $outFile" -ForegroundColor Green

# --- 2. Vega Adjacency Matrix (interactive HTML) ---
$outFile = Join-Path $OutputDir 'les-miserables-matrix.html'
Export-GraphView -Graph $graph `
    -Renderer VegaAdjacencyMatrix `
    -As Html `
    -Path $outFile `
    -GroupMetadataKey 'group'
Write-Host "[2/7] Adjacency matrix: $outFile" -ForegroundColor Green

# --- 3. MSAGL MDS layout (SVG) ---
$outFile = Join-Path $OutputDir 'les-miserables-mds.svg'
Export-GraphView -Graph $graph `
    -Renderer MsaglMds `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 3 `
    -LabelFontSize 7 `
    -GroupMetadataKey 'group'
Write-Host "[3/7] MDS layout: $outFile" -ForegroundColor Green

# --- 4. MSAGL Sugiyama hierarchical (SVG) ---
$outFile = Join-Path $OutputDir 'les-miserables-sugiyama.svg'
Export-GraphView -Graph $graph `
    -Renderer MsaglSugiyama `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 3 `
    -LabelFontSize 6 `
    -SugiyamaDirection Horizontal `
    -SugiyamaNodeSeparation 12 `
    -GroupMetadataKey 'group'
Write-Host "[4/7] Sugiyama layout: $outFile" -ForegroundColor Green

# --- 5. DSM plain matrix (SVG + Vega HTML) ---
$dsm = New-DSM -Graph $graph
$outFile = Join-Path $OutputDir 'les-miserables-dsm.svg'
Export-DSMView -Dsm $dsm `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 12
Write-Host "[5/7] DSM plain (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'les-miserables-dsm.html'
Export-DSMView -Dsm $dsm `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile `
    -ItemSize 12
Write-Host "       DSM plain (Vega HTML): $outFile" -ForegroundColor Green

# --- 6. DSM after clustering (partition boundaries highlighted) ---
$clustered = Start-DSMClustering -Dsm $dsm
$outFile = Join-Path $OutputDir 'les-miserables-dsm-clustered.svg'
Export-DSMView -Result $clustered `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 12
Write-Host "[6/7] DSM clustered (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'les-miserables-dsm-clustered.html'
Export-DSMView -Result $clustered `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile `
    -ItemSize 12
Write-Host "       DSM clustered (Vega HTML): $outFile" -ForegroundColor Green

# --- 7. DSM after sequencing ---
$sequenced = Start-DSMSequencing -Dsm $dsm
$outFile = Join-Path $OutputDir 'les-miserables-dsm-sequenced.svg'
Export-DSMView -SequencedDsm $sequenced `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 12
Write-Host "[7/7] DSM sequenced (SVG): $outFile" -ForegroundColor Green

Write-Host "`nAll outputs in: $OutputDir" -ForegroundColor Cyan
Write-Host "Open the .html files in a browser for interactive exploration.`n"
