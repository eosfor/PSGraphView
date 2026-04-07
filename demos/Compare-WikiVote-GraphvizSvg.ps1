# Compare-WikiVote-GraphvizSvg.ps1
# Build a manageable wiki-Vote subgraph, render it through native PSGraphView
# and through the external dot process, then compare both output files and timing.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-wikivote-compare'),
    [string]$DotPath,
    [int]$SubgraphSeedCount = 30,
    [int]$WarmRunCount = 7,
    [int]$ColdRunCount = 5,
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizNativeLibraryPath
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

function Get-Stats {
    param(
        [Parameter(Mandatory)]
        [double[]]$Values
    )

    $sorted = $Values | Sort-Object
    $median = if ($sorted.Count % 2 -eq 1) {
        $sorted[[int]($sorted.Count / 2)]
    } else {
        ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2
    }

    [pscustomobject]@{
        AvgMs = [math]::Round((($Values | Measure-Object -Average).Average), 2)
        MedianMs = [math]::Round($median, 2)
        MinMs = [math]::Round((($Values | Measure-Object -Minimum).Minimum), 2)
        MaxMs = [math]::Round((($Values | Measure-Object -Maximum).Maximum), 2)
        SamplesMs = @($Values | ForEach-Object { [math]::Round($_, 2) })
    }
}

function Measure-Action {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,
        [Parameter(Mandatory)]
        [int]$Count
    )

    $samples = [System.Collections.Generic.List[double]]::new()
    foreach ($iteration in 1..$Count) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        & $Action
        $watch.Stop()
        $samples.Add($watch.Elapsed.TotalMilliseconds)
    }

    return ,$samples.ToArray()
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

function New-EncodedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$ScriptText
    )

    return [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($ScriptText))
}

if ($WarmRunCount -lt 1) {
    throw 'WarmRunCount must be greater than 0.'
}

if ($ColdRunCount -lt 1) {
    throw 'ColdRunCount must be greater than 0.'
}

$pwshPath = Get-RequiredCommandPath -CommandName 'pwsh'
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

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$benchmarkScratchDir = Join-Path $OutputDir '.benchmark'
if (-not (Test-Path $benchmarkScratchDir)) {
    New-Item -ItemType Directory -Path $benchmarkScratchDir | Out-Null
}

$resolvedDotPath = if ($DotPath) {
    Resolve-OptionalPath -PathValue $DotPath -Description 'DOT file'
} else {
    Join-Path $OutputDir 'wiki-vote.dot'
}

$graphInfo = $null
if (-not $DotPath) {
    Write-Host "Building wiki-Vote subgraph DOT (seed=$SubgraphSeedCount)..." -ForegroundColor Cyan
    $graphInfo = New-WikiVoteDotFile -OutputPath $resolvedDotPath -SeedCount $SubgraphSeedCount
}

$nativeSvgPath = Join-Path $OutputDir 'wiki-vote-psgraphview-native.svg'
$graphvizSvgPath = Join-Path $OutputDir 'wiki-vote-graphviz-dot.svg'
$benchmarkJsonPath = Join-Path $OutputDir 'wiki-vote-svg-benchmark.json'

Write-Host "DOT: $resolvedDotPath" -ForegroundColor DarkGray
Write-Host 'Generating baseline SVG outputs...' -ForegroundColor Cyan

Export-GraphvizView -DotPath $resolvedDotPath -Renderer Dot -As Svg -OutputPath $nativeSvgPath | Out-Null
& $dotCommandPath -Kdot -Tsvg $resolvedDotPath -o $graphvizSvgPath

$nativeWarmSamples = Measure-Action -Count $WarmRunCount -Action {
    Export-GraphvizView -DotPath $resolvedDotPath -Renderer Dot -As Svg -OutputPath $nativeSvgPath | Out-Null
}

$graphvizWarmSamples = Measure-Action -Count $WarmRunCount -Action {
    & $dotCommandPath -Kdot -Tsvg $resolvedDotPath -o $graphvizSvgPath
}

$nativeEnvironmentPrefix = if ([string]::IsNullOrWhiteSpace($env:PSGRAPHVIEW_PSGV_LIBRARY_PATH)) {
    ''
} else {
    "`$env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = '$($env:PSGRAPHVIEW_PSGV_LIBRARY_PATH.Replace("'", "''"))'; "
}

$nativeColdSamples = [System.Collections.Generic.List[double]]::new()
$graphvizColdSamples = [System.Collections.Generic.List[double]]::new()

foreach ($iteration in 1..$ColdRunCount) {
    $nativeColdOutputPath = Join-Path $benchmarkScratchDir "wiki-vote-native-cold-$iteration.svg"
    $nativeColdScript = @"
`$ErrorActionPreference = 'Stop'
$nativeEnvironmentPrefix`$ProgressPreference = 'SilentlyContinue'
Import-Module '$($psGraphViewModulePath.Replace("'", "''"))' -Force
Export-GraphvizView -DotPath '$($resolvedDotPath.Replace("'", "''"))' -Renderer Dot -As Svg -OutputPath '$($nativeColdOutputPath.Replace("'", "''"))' | Out-Null
"@

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    & $pwshPath -NoLogo -NoProfile -EncodedCommand (New-EncodedCommand -ScriptText $nativeColdScript) | Out-Null
    $watch.Stop()
    $nativeColdSamples.Add($watch.Elapsed.TotalMilliseconds)

    $graphvizColdOutputPath = Join-Path $benchmarkScratchDir "wiki-vote-graphviz-cold-$iteration.svg"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    & $dotCommandPath -Kdot -Tsvg $resolvedDotPath -o $graphvizColdOutputPath
    $watch.Stop()
    $graphvizColdSamples.Add($watch.Elapsed.TotalMilliseconds)
}

$result = [pscustomobject]@{
    DotPath = $resolvedDotPath
    Outputs = [pscustomobject]@{
        NativeSvg = $nativeSvgPath
        GraphvizSvg = $graphvizSvgPath
        BenchmarkJson = $benchmarkJsonPath
    }
    Environment = [pscustomobject]@{
        PSGraphViewModulePath = $psGraphViewModulePath
        GraphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH
        DotCommandPath = $dotCommandPath
        PwshPath = $pwshPath
    }
    Inputs = [pscustomobject]@{
        WarmRunCount = $WarmRunCount
        ColdRunCount = $ColdRunCount
        SubgraphSeedCount = $SubgraphSeedCount
        DotProvidedByCaller = [bool]$DotPath
    }
    Graph = $graphInfo
    Sizes = [pscustomobject]@{
        NativeSvgBytes = (Get-Item $nativeSvgPath).Length
        GraphvizSvgBytes = (Get-Item $graphvizSvgPath).Length
    }
    Warm = [pscustomobject]@{
        NativeSvg = Get-Stats -Values $nativeWarmSamples
        GraphvizDot = Get-Stats -Values $graphvizWarmSamples
    }
    Cold = [pscustomobject]@{
        NativeSvg = Get-Stats -Values $nativeColdSamples.ToArray()
        GraphvizDot = Get-Stats -Values $graphvizColdSamples.ToArray()
    }
}

$result | ConvertTo-Json -Depth 8 | Set-Content -Path $benchmarkJsonPath

Write-Host ''
Write-Host 'SVG compare:' -ForegroundColor Cyan
Write-Host "  Graphviz dot : $graphvizSvgPath"
Write-Host "  Native scene : $nativeSvgPath"
Write-Host ''
Write-Host 'Speed summary (avg ms):' -ForegroundColor Cyan
Write-Host ("  Warm  native={0}  dot={1}" -f $result.Warm.NativeSvg.AvgMs, $result.Warm.GraphvizDot.AvgMs)
Write-Host ("  Cold  native={0}  dot={1}" -f $result.Cold.NativeSvg.AvgMs, $result.Cold.GraphvizDot.AvgMs)
Write-Host ''
Write-Host "Benchmark JSON: $benchmarkJsonPath" -ForegroundColor Green

$result
