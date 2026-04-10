# Compare-WikiVote-GraphvizRaster.ps1
# Build or reuse one DOT file, render it through Graphviz and through native
# PSGraphView raster path, then compute numeric divergence metrics.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-wikivote-raster-compare'),
    [string]$DotPath,
    [int]$SubgraphSeedCount = 30,
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizNativeLibraryPath,
    [int]$MaxComparisonDimension = 0,
    [byte]$DifferentPixelThreshold = 8
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Import-DemoModules.ps1')

function Resolve-OptionalPath {
    param(
        [Parameter(Mandatory)]
        [string]$PathValue,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($PathValue)
    if (-not (Test-Path $resolvedPath)) {
        throw "$Description was not found at '$resolvedPath'."
    }

    return $resolvedPath
}

function Get-RequiredCommandPath {
    param(
        [Parameter(Mandatory)]
        [string]$CommandName
    )

    $command = Get-Command $CommandName -CommandType Application -ErrorAction Stop |
        Select-Object -First 1

    return $command.Path
}

function Get-WikiVoteDatasetPath {
    $dataDirectory = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
    $wikiVoteGzipPath = Join-Path $dataDirectory 'wiki-Vote.txt.gz'
    $wikiVoteTextPath = Join-Path $dataDirectory 'wiki-Vote.txt'

    if (-not (Test-Path $dataDirectory)) {
        New-Item -ItemType Directory -Path $dataDirectory | Out-Null
    }

    if (-not (Test-Path $wikiVoteTextPath)) {
        Write-Host 'Downloading wiki-Vote dataset...' -ForegroundColor Cyan
        Invoke-WebRequest -Uri 'https://snap.stanford.edu/data/wiki-Vote.txt.gz' -OutFile $wikiVoteGzipPath

        $inputStream = [System.IO.File]::OpenRead($wikiVoteGzipPath)
        try {
            $gzipStream = [System.IO.Compression.GZipStream]::new($inputStream, [System.IO.Compression.CompressionMode]::Decompress)
            try {
                $outputStream = [System.IO.File]::Create($wikiVoteTextPath)
                try {
                    $gzipStream.CopyTo($outputStream)
                }
                finally {
                    $outputStream.Dispose()
                }
            }
            finally {
                $gzipStream.Dispose()
            }
        }
        finally {
            $inputStream.Dispose()
        }

        Remove-Item $wikiVoteGzipPath -ErrorAction SilentlyContinue
    }

    return $wikiVoteTextPath
}

function New-WikiVoteDotFile {
    param(
        [Parameter(Mandatory)]
        [string]$OutputPath,
        [Parameter(Mandatory)]
        [int]$SeedCount
    )

    $datasetPath = Get-WikiVoteDatasetPath
    $fullGraph = Import-Graph -Path $datasetPath -Format Csv -Delimiter "`t" -NoHeader

    $seedVertices = $fullGraph.Vertices |
        Sort-Object { $fullGraph.OutDegree($_) } -Descending |
        Select-Object -First $SeedCount

    $subgraph = New-Graph
    $included = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($vertex in $seedVertices) {
        [void]$included.Add($vertex.Label)
    }

    foreach ($vertex in $seedVertices) {
        $outEdges = Get-OutEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($outEdges) {
            foreach ($edge in $outEdges) {
                if ($included.Contains($edge.Target.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }
        }

        $inEdges = Get-InEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($inEdges) {
            foreach ($edge in $inEdges) {
                if ($included.Contains($edge.Source.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }
        }
    }

    foreach ($vertex in $seedVertices) {
        Add-Vertex -Graph $subgraph -Vertex $vertex.Label -ErrorAction SilentlyContinue | Out-Null
    }

    Export-Graph -Graph $subgraph -Format Graphviz -Path $OutputPath

    return [pscustomobject]@{
        DatasetPath = $datasetPath
        FullGraphVertexCount = $fullGraph.VertexCount
        FullGraphEdgeCount = $fullGraph.EdgeCount
        SubgraphVertexCount = $subgraph.VertexCount
        SubgraphEdgeCount = $subgraph.EdgeCount
    }
}

function Convert-ComparisonResult {
    param(
        [Parameter(Mandatory)]
        [PSGraphView.Graphviz.RasterComparisonResult]$Comparison
    )

    return [pscustomobject]@{
        BaselineSize = [pscustomobject]@{
            Width = $Comparison.BaselineSize.Width
            Height = $Comparison.BaselineSize.Height
        }
        CandidateSize = [pscustomobject]@{
            Width = $Comparison.CandidateSize.Width
            Height = $Comparison.CandidateSize.Height
        }
        ComparisonSize = [pscustomobject]@{
            Width = $Comparison.ComparisonSize.Width
            Height = $Comparison.ComparisonSize.Height
        }
        DifferentPixelThreshold = $Comparison.DifferentPixelThreshold
        Metrics = [pscustomobject]@{
            MeanAbsoluteDifference = [math]::Round($Comparison.Metrics.MeanAbsoluteDifference, 4)
            RootMeanSquareDifference = [math]::Round($Comparison.Metrics.RootMeanSquareDifference, 4)
            MaxAbsoluteDifference = [math]::Round($Comparison.Metrics.MaxAbsoluteDifference, 4)
            DifferentPixelRatio = [math]::Round($Comparison.Metrics.DifferentPixelRatio, 6)
            DifferentPixelPercent = [math]::Round($Comparison.Metrics.DifferentPixelRatio * 100, 4)
            GlobalStructuralSimilarity = [math]::Round($Comparison.Metrics.GlobalStructuralSimilarity, 6)
        }
    }
}

$dotCommandPath = Get-RequiredCommandPath -CommandName 'dot'

if ($GraphvizNativeLibraryPath) {
    $resolvedNativeLibraryPath = Resolve-OptionalPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
    $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = $resolvedNativeLibraryPath
}
elseif (-not [string]::IsNullOrWhiteSpace($env:PSGRAPHVIEW_PSGV_LIBRARY_PATH)) {
    $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = Resolve-OptionalPath -PathValue $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH -Description 'Graphviz native library'
}

Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

$psGraphViewModulePath = (Get-Module PSGraphView | Select-Object -First 1 -ExpandProperty Path)
if ([string]::IsNullOrWhiteSpace($psGraphViewModulePath) -or -not (Test-Path $psGraphViewModulePath)) {
    throw 'PSGraphView module path could not be resolved after import.'
}

$graphvizAssemblyPath = Join-Path (Split-Path -Parent $psGraphViewModulePath) 'PSGraphView.Graphviz.dll'
if (-not (Test-Path $graphvizAssemblyPath)) {
    throw "PSGraphView.Graphviz assembly was not found at '$graphvizAssemblyPath'."
}

[void][System.Reflection.Assembly]::LoadFrom($graphvizAssemblyPath)

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$resolvedDotPath = if ($DotPath) {
    Resolve-OptionalPath -PathValue $DotPath -Description 'DOT file'
}
else {
    Join-Path $OutputDir 'wiki-vote.dot'
}

$graphInfo = $null
if (-not $DotPath) {
    Write-Host "Building wiki-Vote subgraph DOT (seed=$SubgraphSeedCount)..." -ForegroundColor Cyan
    $graphInfo = New-WikiVoteDotFile -OutputPath $resolvedDotPath -SeedCount $SubgraphSeedCount
}

$nativePngPath = Join-Path $OutputDir 'wiki-vote-psgraphview-native.png'
$graphvizPngPath = Join-Path $OutputDir 'wiki-vote-graphviz-dot.png'
$nativeJpgPath = Join-Path $OutputDir 'wiki-vote-psgraphview-native.jpg'
$graphvizJpgPath = Join-Path $OutputDir 'wiki-vote-graphviz-dot.jpg'
$pngDiffPath = Join-Path $OutputDir 'wiki-vote-raster-diff-png.png'
$jpgDiffPath = Join-Path $OutputDir 'wiki-vote-raster-diff-jpg.png'
$compareJsonPath = Join-Path $OutputDir 'wiki-vote-raster-compare.json'

Write-Host "DOT: $resolvedDotPath" -ForegroundColor DarkGray
Write-Host 'Generating raster outputs...' -ForegroundColor Cyan

Export-GraphvizView -DotPath $resolvedDotPath -Renderer Dot -As Png -OutputPath $nativePngPath | Out-Null
Export-GraphvizView -DotPath $resolvedDotPath -Renderer Dot -As Jpg -OutputPath $nativeJpgPath | Out-Null
& $dotCommandPath -Kdot -Tpng $resolvedDotPath -o $graphvizPngPath
& $dotCommandPath -Kdot -Tjpg $resolvedDotPath -o $graphvizJpgPath

$comparisonOptions = [PSGraphView.Graphviz.RasterComparisonOptions]::new($MaxComparisonDimension, $DifferentPixelThreshold)
$graphvizPngBytes = Get-Content -LiteralPath $graphvizPngPath -AsByteStream -Raw
$nativePngBytes = Get-Content -LiteralPath $nativePngPath -AsByteStream -Raw
$graphvizJpgBytes = Get-Content -LiteralPath $graphvizJpgPath -AsByteStream -Raw
$nativeJpgBytes = Get-Content -LiteralPath $nativeJpgPath -AsByteStream -Raw

$pngComparison = [PSGraphView.Graphviz.RasterImageComparer]::Compare($graphvizPngBytes, $nativePngBytes, $comparisonOptions)
$jpgComparison = [PSGraphView.Graphviz.RasterImageComparer]::Compare($graphvizJpgBytes, $nativeJpgBytes, $comparisonOptions)

[System.IO.File]::WriteAllBytes(
    $pngDiffPath,
    [PSGraphView.Graphviz.RasterImageComparer]::RenderDiffPng($graphvizPngBytes, $nativePngBytes, $comparisonOptions))
[System.IO.File]::WriteAllBytes(
    $jpgDiffPath,
    [PSGraphView.Graphviz.RasterImageComparer]::RenderDiffPng($graphvizJpgBytes, $nativeJpgBytes, $comparisonOptions))

$result = [pscustomobject]@{
    DotPath = $resolvedDotPath
    Outputs = [pscustomobject]@{
        NativePng = $nativePngPath
        GraphvizPng = $graphvizPngPath
        NativeJpg = $nativeJpgPath
        GraphvizJpg = $graphvizJpgPath
        PngDiff = $pngDiffPath
        JpgDiff = $jpgDiffPath
        CompareJson = $compareJsonPath
    }
    Environment = [pscustomobject]@{
        PSGraphViewModulePath = $psGraphViewModulePath
        PSGraphViewGraphvizAssemblyPath = $graphvizAssemblyPath
        GraphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH
        DotCommandPath = $dotCommandPath
    }
    Inputs = [pscustomobject]@{
        SubgraphSeedCount = $SubgraphSeedCount
        DotProvidedByCaller = [bool]$DotPath
        MaxComparisonDimension = $MaxComparisonDimension
        DifferentPixelThreshold = $DifferentPixelThreshold
    }
    Graph = $graphInfo
    Sizes = [pscustomobject]@{
        NativePngBytes = (Get-Item $nativePngPath).Length
        GraphvizPngBytes = (Get-Item $graphvizPngPath).Length
        NativeJpgBytes = (Get-Item $nativeJpgPath).Length
        GraphvizJpgBytes = (Get-Item $graphvizJpgPath).Length
        PngDiffBytes = (Get-Item $pngDiffPath).Length
        JpgDiffBytes = (Get-Item $jpgDiffPath).Length
    }
    Png = Convert-ComparisonResult -Comparison $pngComparison
    Jpg = Convert-ComparisonResult -Comparison $jpgComparison
}

$result | ConvertTo-Json -Depth 8 | Set-Content -Path $compareJsonPath

Write-Host ''
Write-Host 'Raster compare:' -ForegroundColor Cyan
Write-Host "  Graphviz png : $graphvizPngPath"
Write-Host "  Native png   : $nativePngPath"
Write-Host "  Graphviz jpg : $graphvizJpgPath"
Write-Host "  Native jpg   : $nativeJpgPath"
Write-Host "  PNG diff     : $pngDiffPath"
Write-Host "  JPG diff     : $jpgDiffPath"
Write-Host ''
Write-Host 'Metric summary:' -ForegroundColor Cyan
Write-Host ("  PNG  rmse={0}  diff%={1}  ssim={2}" -f $result.Png.Metrics.RootMeanSquareDifference, $result.Png.Metrics.DifferentPixelPercent, $result.Png.Metrics.GlobalStructuralSimilarity)
Write-Host ("  JPG  rmse={0}  diff%={1}  ssim={2}" -f $result.Jpg.Metrics.RootMeanSquareDifference, $result.Jpg.Metrics.DifferentPixelPercent, $result.Jpg.Metrics.GlobalStructuralSimilarity)
Write-Host ''
Write-Host "Compare JSON: $compareJsonPath" -ForegroundColor Green

$result
