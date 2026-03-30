# Visualize-WikiVote.ps1
# Visualize the SNAP wiki-Vote graph with the managed PSGraphView Sfdp renderer.
# By default the script renders the full graph. You can opt into a smaller
# top-degree subgraph for quicker iteration.
#
# Produces:
#   - managed Sfdp layout of the selected graph (SVG)
#   - optional terminal preview via Out-Sixel

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demos'),
    [switch]$UseSubgraph,
    [int]$SubgraphSeedCount = 30,
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$LibSixelManifestPath,
    [switch]$ShowSixelPreview,
    [switch]$ForceSixelPreview,
    [ValidateRange(2, 256)]
    [int]$SixelColors = 128,
    [ValidateRange(1, 4000)]
    [int]$SixelWidth = 1200,
    [int]$SfdpSeed = 42,
    [ValidateRange(1, 10000)]
    [int]$ImageWidth = 800,
    [ValidateRange(1, 10000)]
    [int]$ImageHeight = 800
)

$ErrorActionPreference = 'Stop'

function Get-SixelPreviewSupport {
    [CmdletBinding()]
    param()

    if ($env:TERM -eq 'dumb') {
        return @{
            Supported = $false
            Reason = "TERM is set to 'dumb'."
        }
    }

    if ($env:TERM_PROGRAM -eq 'iTerm.app') {
        return @{
            Supported = $false
            Reason = "iTerm.app does not render SIXEL."
        }
    }

    if ($env:TERM_PROGRAM -eq 'vscode' -or $env:VSCODE_INJECTION) {
        return @{
            Supported = $false
            Reason = "VS Code integrated terminal does not render SIXEL."
        }
    }

    if ($env:TMUX) {
        return @{
            Supported = $false
            Reason = "tmux usually strips or ignores SIXEL unless explicitly configured."
        }
    }

    return @{
        Supported = $true
        Reason = $null
    }
}

. (Join-Path $PSScriptRoot 'Import-DemoModules.ps1')
Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -ImportLibSixel:$ShowSixelPreview `
    -LibSixelManifestPath $LibSixelManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

Write-Host "`n=== wiki-Vote Sfdp Demo ===" -ForegroundColor Cyan

# --- Download dataset ---
$dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir | Out-Null
}

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

if ($UseSubgraph) {
    Write-Host "Extracting subgraph (seed=$SubgraphSeedCount highest-degree vertices)..."

    $seedVertices = $fullGraph.Vertices |
        Sort-Object { $fullGraph.OutDegree($_) } -Descending |
        Select-Object -First $SubgraphSeedCount

    $graphToRender = New-Graph
    $included = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($vertex in $seedVertices) {
        [void]$included.Add($vertex.Label)
    }

    foreach ($vertex in $seedVertices) {
        $outEdges = Get-OutEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($outEdges) {
            foreach ($edge in $outEdges) {
                if ($included.Contains($edge.Target.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $graphToRender | Out-Null
                }
            }
        }

        $inEdges = Get-InEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($inEdges) {
            foreach ($edge in $inEdges) {
                if ($included.Contains($edge.Source.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $graphToRender | Out-Null
                }
            }
        }
    }

    foreach ($vertex in $seedVertices) {
        Add-Vertex -Graph $graphToRender -Vertex $vertex.Label -ErrorAction SilentlyContinue | Out-Null
    }

    Write-Host "Subgraph: $($graphToRender.VertexCount) vertices, $($graphToRender.EdgeCount) edges"
    $nodeRadius = 1
    $edgeLineWidth = 0.2
    $edgeColor = '#00000018'
    $showArrows = $true
    $arrowSize = 0.08
    $outputName = 'wiki-vote-sfdp-subgraph.svg'
}
else {
    $graphToRender = $fullGraph
    Write-Host "Rendering full graph with managed Sfdp..."
    $nodeRadius = 0.35
    $edgeLineWidth = 0.1
    $edgeColor = '#0000000f'
    $showArrows = $false
    $arrowSize = 0.08
    $outputName = 'wiki-vote-sfdp-full.svg'
    Write-Host "Render graph: $($graphToRender.VertexCount) vertices, $($graphToRender.EdgeCount) edges"
}

# --- Managed Sfdp layout (SVG) ---
$outFile = Join-Path $OutputDir $outputName
$exportParameters = @{
    Graph = $graphToRender
    Renderer = 'Sfdp'
    As = 'Svg'
    Path = $outFile
    NodeRadius = $nodeRadius
    EdgeLineWidth = $edgeLineWidth
    EdgeColor = $edgeColor
    ArrowSize = $arrowSize
    DisableGroupColors = $true
    SfdpSeed = $SfdpSeed
    SfdpOverlapRemovalPadding = 4
    Width = $ImageWidth
    Height = $ImageHeight
}
if ($showArrows) {
    $exportParameters.ShowArrows = $true
}

Export-GraphView @exportParameters

Write-Host "[1/1] Managed Sfdp layout: $outFile" -ForegroundColor Green

$sixelPreviewShown = $false
if ($ShowSixelPreview) {
    $sixelSupport = Get-SixelPreviewSupport
    if (-not $sixelSupport.Supported -and -not $ForceSixelPreview) {
        Write-Warning "Skipping SIXEL preview. $($sixelSupport.Reason) Use -ForceSixelPreview to send SIXEL anyway."
    }
    else {
        if (-not $sixelSupport.Supported) {
            Write-Warning "Forcing SIXEL preview even though the terminal reported: $($sixelSupport.Reason)"
        }

        Write-Host "Rendering SIXEL preview in terminal..." -ForegroundColor Cyan
        Out-Sixel -Path $outFile -Width $SixelWidth -Colors $SixelColors
        $sixelPreviewShown = $true
    }
}

Write-Host "`nAll outputs in: $OutputDir" -ForegroundColor Cyan
if ($sixelPreviewShown) {
    Write-Host "The SVG was also rendered in the current terminal via Out-Sixel.`n"
}
elseif ($ShowSixelPreview) {
    Write-Host "SIXEL preview was requested but skipped because the current terminal does not report usable SIXEL support.`n"
}
else {
    Write-Host "Rerun with -ShowSixelPreview to render the SVG directly in a SIXEL-capable terminal.`n"
}
