# Visualize-KarateClub.ps1
# Visualize Zachary's Karate Club social network using PSGraphView.
# 34 members, 78 ties -- compact enough for every layout and DSM analysis.
#
# Produces:
#   - Vega force-directed (HTML)
#   - Vega adjacency matrix (HTML)
#   - MSAGL MDS (SVG)
#   - MSAGL Fast Incremental (SVG)
#   - MSAGL Sugiyama (SVG)
#   - DSM plain matrix (SVG + Vega HTML)
#   - DSM after clustering with partition boundaries (SVG + Vega HTML)
#   - DSM after sequencing (SVG)

param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demos')
)

$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$psGraphRoot  = Join-Path (Split-Path -Parent $repoRoot) 'PSGraph'

Import-Module "$psGraphRoot/PSGraph.Tests/bin/Debug/net9.0/PSQuickGraph.psd1" -Force
Import-Module "$repoRoot/tests/PSGraphView.PowerShell.Tests/bin/Debug/net9.0/PSGraphView.psd1" -Force

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "`n=== Karate Club Visualization ===" -ForegroundColor Cyan

# --- Download dataset ---
$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }

$karateUrl = 'https://nrvis.com/download/data/soc/soc-karate.zip'
$karateZip = Join-Path $dataDir 'soc-karate.zip'
$karateMtx = Join-Path $dataDir 'soc-karate.mtx'

if (-not (Test-Path $karateMtx)) {
    Write-Host 'Downloading Karate Club dataset...'
    Invoke-WebRequest -Uri $karateUrl -OutFile $karateZip

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($karateZip)
    $entry = $zip.Entries | Where-Object { $_.Name -like '*.mtx' } | Select-Object -First 1
    if ($entry) {
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $karateMtx, $true)
    }
    $zip.Dispose()
    Remove-Item $karateZip -ErrorAction SilentlyContinue
} else {
    Write-Host "Using cached $karateMtx"
}

$graph = Import-Graph -Path $karateMtx -Format Csv -Delimiter ' ' -NoHeader
Write-Host "Loaded: $($graph.VertexCount) vertices, $($graph.EdgeCount) edges"

# --- 1. Vega Force-Directed ---
$outFile = Join-Path $OutputDir 'karate-force.html'
Export-GraphView -Graph $graph `
    -Renderer VegaForceDirected `
    -As Html `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 6 `
    -LabelFontSize 10
Write-Host "[1/8] Force-directed: $outFile" -ForegroundColor Green

# --- 2. Vega Adjacency Matrix ---
$outFile = Join-Path $OutputDir 'karate-matrix.html'
Export-GraphView -Graph $graph `
    -Renderer VegaAdjacencyMatrix `
    -As Html `
    -Path $outFile
Write-Host "[2/8] Adjacency matrix: $outFile" -ForegroundColor Green

# --- 3. MSAGL MDS ---
$outFile = Join-Path $OutputDir 'karate-mds.svg'
Export-GraphView -Graph $graph `
    -Renderer MsaglMds `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 4 `
    -LabelFontSize 9
Write-Host "[3/8] MDS layout: $outFile" -ForegroundColor Green

# --- 4. MSAGL Fast Incremental ---
$outFile = Join-Path $OutputDir 'karate-fast-incremental.svg'
Export-GraphView -Graph $graph `
    -Renderer MsaglFastIncremental `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -NodeRadius 4 `
    -LabelFontSize 9
Write-Host "[4/8] Fast incremental: $outFile" -ForegroundColor Green

# --- 5. MSAGL Sugiyama ---
$outFile = Join-Path $OutputDir 'karate-sugiyama.svg'
Export-GraphView -Graph $graph `
    -Renderer MsaglSugiyama `
    -As Svg `
    -Path $outFile `
    -ShowLabels `
    -SugiyamaDirection Vertical `
    -SugiyamaNodeSeparation 16 `
    -SugiyamaEdgeRouting Spline
Write-Host "[5/8] Sugiyama layout: $outFile" -ForegroundColor Green

# --- 6. DSM plain matrix (SVG + Vega HTML) ---
$dsm = New-DSM -Graph $graph
$outFile = Join-Path $OutputDir 'karate-dsm.svg'
Export-DSMView -Dsm $dsm `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 20
Write-Host "[6/8] DSM plain (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'karate-dsm.html'
Export-DSMView -Dsm $dsm `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile `
    -ItemSize 20
Write-Host "       DSM plain (Vega HTML): $outFile" -ForegroundColor Green

# --- 7. DSM after clustering (partition boundaries highlighted) ---
$clustered = Start-DSMClustering -Dsm $dsm
$outFile = Join-Path $OutputDir 'karate-dsm-clustered.svg'
Export-DSMView -Result $clustered `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 20
Write-Host "[7/8] DSM clustered (SVG): $outFile" -ForegroundColor Green

$outFile = Join-Path $OutputDir 'karate-dsm-clustered.html'
Export-DSMView -Result $clustered `
    -Renderer DsmVegaMatrix `
    -As Html `
    -Path $outFile `
    -ItemSize 20
Write-Host "       DSM clustered (Vega HTML): $outFile" -ForegroundColor Green

# --- 8. DSM after sequencing ---
$sequenced = Start-DSMSequencing -Dsm $dsm
$outFile = Join-Path $OutputDir 'karate-dsm-sequenced.svg'
Export-DSMView -SequencedDsm $sequenced `
    -Renderer DsmMatrixSvg `
    -As Svg `
    -Path $outFile `
    -ItemSize 20
Write-Host "[8/8] DSM sequenced (SVG): $outFile" -ForegroundColor Green

Write-Host "`nAll outputs in: $OutputDir" -ForegroundColor Cyan
Write-Host "Open the .html files in a browser for interactive exploration.`n"
