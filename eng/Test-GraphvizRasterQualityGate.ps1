param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleManifestPath,

    [string]$ThresholdManifestPath = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz/quality/raster-quality-gate.json'),

    [string]$FixtureRoot = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz'),

    [string]$OutputDirectory,

    [string]$GraphvizNativeLibraryPath,

    [switch]$RequireBundledGraphvizRuntime,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RequiredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "$Description path is required."
    }

    if (-not (Test-Path -LiteralPath $PathValue)) {
        throw "$Description path '$PathValue' was not found."
    }

    return (Resolve-Path -LiteralPath $PathValue).Path
}

function Get-CurrentRuntimeIdentifier {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    $architectureSuffix = switch ($architecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { 'x64' }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { 'arm64' }
        default { throw "Unsupported process architecture '$architecture'." }
    }

    if ($IsWindows) {
        return "win-$architectureSuffix"
    }

    if ($IsMacOS) {
        return "osx-$architectureSuffix"
    }

    if ($IsLinux) {
        return "linux-$architectureSuffix"
    }

    throw 'Unsupported operating system.'
}

function Get-GraphvizNativeLibraryFileNames {
    if ($IsWindows) {
        return @('psgv.dll', 'libpsgv.dll')
    }

    if ($IsMacOS) {
        return @('libpsgv.dylib', 'libpsgv.0.dylib')
    }

    if ($IsLinux) {
        return @('libpsgv.so', 'libpsgv.so.0')
    }

    throw 'Unsupported operating system.'
}

function Find-BundledGraphvizNativeLibraryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleRoot,

        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    $nativeRoot = Join-Path $ModuleRoot "runtimes/$RuntimeIdentifier/native"
    foreach ($relativeDirectory in @('', 'lib', 'bin')) {
        $searchRoot = if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
            $nativeRoot
        }
        else {
            Join-Path $nativeRoot $relativeDirectory
        }

        foreach ($fileName in Get-GraphvizNativeLibraryFileNames) {
            $candidatePath = Join-Path $searchRoot $fileName
            if (Test-Path -LiteralPath $candidatePath) {
                return (Resolve-Path -LiteralPath $candidatePath).Path
            }
        }
    }

    return $null
}

function Get-RequiredCommandPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandName
    )

    $command = Get-Command $CommandName -CommandType Application -ErrorAction Stop |
        Select-Object -First 1

    return $command.Path
}

function Convert-ComparisonResult {
    param(
        [Parameter(Mandatory = $true)]
        [PSGraphView.Graphviz.RasterComparisonResult]$Comparison
    )

    return [ordered]@{
        baselineSize = [ordered]@{
            width = $Comparison.BaselineSize.Width
            height = $Comparison.BaselineSize.Height
        }
        candidateSize = [ordered]@{
            width = $Comparison.CandidateSize.Width
            height = $Comparison.CandidateSize.Height
        }
        comparisonSize = [ordered]@{
            width = $Comparison.ComparisonSize.Width
            height = $Comparison.ComparisonSize.Height
        }
        differentPixelThreshold = $Comparison.DifferentPixelThreshold
        metrics = [ordered]@{
            meanAbsoluteDifference = [math]::Round($Comparison.Metrics.MeanAbsoluteDifference, 4)
            rootMeanSquareDifference = [math]::Round($Comparison.Metrics.RootMeanSquareDifference, 4)
            maxAbsoluteDifference = [math]::Round($Comparison.Metrics.MaxAbsoluteDifference, 4)
            differentPixelRatio = [math]::Round($Comparison.Metrics.DifferentPixelRatio, 6)
            differentPixelPercent = [math]::Round($Comparison.Metrics.DifferentPixelRatio * 100, 4)
            globalStructuralSimilarity = [math]::Round($Comparison.Metrics.GlobalStructuralSimilarity, 6)
        }
    }
}

function Test-RasterThresholds {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScenarioId,

        [Parameter(Mandatory = $true)]
        [string]$FormatName,

        [Parameter(Mandatory = $true)]
        [object]$ComparisonSummary,

        [Parameter(Mandatory = $true)]
        [object]$Thresholds
    )

    $failures = New-Object System.Collections.Generic.List[string]
    $metrics = $ComparisonSummary.metrics

    if ($null -ne $Thresholds.maxRootMeanSquareDifference -and
        [double]$metrics.rootMeanSquareDifference -gt [double]$Thresholds.maxRootMeanSquareDifference) {
        $failures.Add(
            ("{0}/{1}: RMSE {2} exceeded max {3}." -f
                $ScenarioId,
                $FormatName,
                $metrics.rootMeanSquareDifference,
                $Thresholds.maxRootMeanSquareDifference)) | Out-Null
    }

    if ($null -ne $Thresholds.maxDifferentPixelPercent -and
        [double]$metrics.differentPixelPercent -gt [double]$Thresholds.maxDifferentPixelPercent) {
        $failures.Add(
            ("{0}/{1}: different-pixel % {2} exceeded max {3}." -f
                $ScenarioId,
                $FormatName,
                $metrics.differentPixelPercent,
                $Thresholds.maxDifferentPixelPercent)) | Out-Null
    }

    if ($null -ne $Thresholds.minGlobalStructuralSimilarity -and
        [double]$metrics.globalStructuralSimilarity -lt [double]$Thresholds.minGlobalStructuralSimilarity) {
        $failures.Add(
            ("{0}/{1}: SSIM {2} is below min {3}." -f
                $ScenarioId,
                $FormatName,
                $metrics.globalStructuralSimilarity,
                $Thresholds.minGlobalStructuralSimilarity)) | Out-Null
    }

    return [ordered]@{
        passed = ($failures.Count -eq 0)
        failures = @($failures)
    }
}

if ($RequireBundledGraphvizRuntime -and -not [string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    throw 'GraphvizNativeLibraryPath cannot be used together with RequireBundledGraphvizRuntime.'
}

$resolvedModuleManifestPath = Resolve-RequiredPath -PathValue $ModuleManifestPath -Description 'Module manifest'
$resolvedThresholdManifestPath = Resolve-RequiredPath -PathValue $ThresholdManifestPath -Description 'Threshold manifest'
$resolvedFixtureRoot = Resolve-RequiredPath -PathValue $FixtureRoot -Description 'Fixture root'
$moduleRoot = Split-Path -Parent $resolvedModuleManifestPath

$resolvedGraphvizNativeLibraryPath = if ([string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    $null
}
else {
    Resolve-RequiredPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-raster-quality-gate-" + [Guid]::NewGuid().ToString('N'))
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

$bundledGraphvizPath = $null
if ($RequireBundledGraphvizRuntime) {
    $runtimeIdentifier = Get-CurrentRuntimeIdentifier
    $bundledGraphvizPath = Find-BundledGraphvizNativeLibraryPath -ModuleRoot $moduleRoot -RuntimeIdentifier $runtimeIdentifier
    if ($null -eq $bundledGraphvizPath) {
        throw "Bundled Graphviz runtime was not found under '$moduleRoot'."
    }
}

$thresholdManifest = Get-Content -LiteralPath $resolvedThresholdManifestPath -Raw | ConvertFrom-Json
if ($null -eq $thresholdManifest -or $null -eq $thresholdManifest.scenarios) {
    throw "Threshold manifest '$resolvedThresholdManifestPath' does not contain a scenarios array."
}

$scenarios = @($thresholdManifest.scenarios)
if ($scenarios.Count -eq 0) {
    throw "Threshold manifest '$resolvedThresholdManifestPath' does not contain any scenarios."
}

$dotCommandPath = Get-RequiredCommandPath -CommandName 'dot'
$graphvizAssemblyPath = Join-Path $moduleRoot 'PSGraphView.Graphviz.dll'
$resolvedGraphvizAssemblyPath = Resolve-RequiredPath -PathValue $graphvizAssemblyPath -Description 'PSGraphView.Graphviz assembly'

$originalDotPath = $env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH
$originalGraphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH

try {
    $env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH = Join-Path $resolvedOutputDirectory $(if ($IsWindows) { 'missing-dot.exe' } else { 'missing-dot' })

    if ($RequireBundledGraphvizRuntime) {
        $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = $bundledGraphvizPath
    }
    elseif ($null -ne $resolvedGraphvizNativeLibraryPath) {
        $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = $resolvedGraphvizNativeLibraryPath
    }
    else {
        Remove-Item Env:PSGRAPHVIEW_PSGV_LIBRARY_PATH -ErrorAction SilentlyContinue
    }

    Remove-Module PSGraphView -ErrorAction SilentlyContinue
    Import-Module $resolvedModuleManifestPath -Force -ErrorAction Stop
    [void][System.Reflection.Assembly]::LoadFrom($resolvedGraphvizAssemblyPath)

    $scenarioResults = New-Object System.Collections.Generic.List[object]
    $failureMessages = New-Object System.Collections.Generic.List[string]
    $startedAt = [DateTimeOffset]::Now

    foreach ($scenario in $scenarios) {
        $scenarioId = [string]$scenario.id
        if ([string]::IsNullOrWhiteSpace($scenarioId)) {
            throw 'Raster quality manifest contains a scenario with an empty id.'
        }

        $relativeDotPath = [string]$scenario.dotPath
        $resolvedDotPath = Resolve-RequiredPath -PathValue (Join-Path $resolvedFixtureRoot $relativeDotPath) -Description "Scenario '$scenarioId' DOT input"
        $renderer = if ([string]::IsNullOrWhiteSpace([string]$scenario.renderer)) { 'Dot' } else { [string]$scenario.renderer }
        $layoutEngine = $renderer.ToLowerInvariant()
        $maxComparisonDimension = if ($null -eq $scenario.maxComparisonDimension) { 0 } else { [int]$scenario.maxComparisonDimension }
        $differentPixelThreshold = if ($null -eq $scenario.differentPixelThreshold) { [byte]8 } else { [byte]$scenario.differentPixelThreshold }

        $scenarioOutputDirectory = Join-Path $resolvedOutputDirectory $scenarioId
        New-Item -ItemType Directory -Path $scenarioOutputDirectory -Force | Out-Null

        $candidatePngPath = Join-Path $scenarioOutputDirectory 'native.png'
        $baselinePngPath = Join-Path $scenarioOutputDirectory 'graphviz.png'
        $candidateJpgPath = Join-Path $scenarioOutputDirectory 'native.jpg'
        $baselineJpgPath = Join-Path $scenarioOutputDirectory 'graphviz.jpg'
        $pngDiffPath = Join-Path $scenarioOutputDirectory 'diff-png.png'
        $jpgDiffPath = Join-Path $scenarioOutputDirectory 'diff-jpg.png'

        $dot = Get-Content -LiteralPath $resolvedDotPath -Raw
        Export-GraphvizView -InputObject $dot -Renderer $renderer -As Png -OutputPath $candidatePngPath
        Export-GraphvizView -InputObject $dot -Renderer $renderer -As Jpg -OutputPath $candidateJpgPath

        & $dotCommandPath "-K$layoutEngine" -Tpng $resolvedDotPath -o $baselinePngPath
        if ($LASTEXITCODE -ne 0) {
            throw "Graphviz baseline generation failed for scenario '$scenarioId' (png)."
        }

        & $dotCommandPath "-K$layoutEngine" -Tjpg $resolvedDotPath -o $baselineJpgPath
        if ($LASTEXITCODE -ne 0) {
            throw "Graphviz baseline generation failed for scenario '$scenarioId' (jpg)."
        }

        $baselinePngBytes = Get-Content -LiteralPath $baselinePngPath -AsByteStream -Raw
        $candidatePngBytes = Get-Content -LiteralPath $candidatePngPath -AsByteStream -Raw
        $baselineJpgBytes = Get-Content -LiteralPath $baselineJpgPath -AsByteStream -Raw
        $candidateJpgBytes = Get-Content -LiteralPath $candidateJpgPath -AsByteStream -Raw
        $comparisonOptions = [PSGraphView.Graphviz.RasterComparisonOptions]::new($maxComparisonDimension, $differentPixelThreshold)

        $pngComparison = [PSGraphView.Graphviz.RasterImageComparer]::Compare($baselinePngBytes, $candidatePngBytes, $comparisonOptions)
        $jpgComparison = [PSGraphView.Graphviz.RasterImageComparer]::Compare($baselineJpgBytes, $candidateJpgBytes, $comparisonOptions)
        [System.IO.File]::WriteAllBytes(
            $pngDiffPath,
            [PSGraphView.Graphviz.RasterImageComparer]::RenderDiffPng($baselinePngBytes, $candidatePngBytes, $comparisonOptions))
        [System.IO.File]::WriteAllBytes(
            $jpgDiffPath,
            [PSGraphView.Graphviz.RasterImageComparer]::RenderDiffPng($baselineJpgBytes, $candidateJpgBytes, $comparisonOptions))

        $pngSummary = Convert-ComparisonResult -Comparison $pngComparison
        $jpgSummary = Convert-ComparisonResult -Comparison $jpgComparison
        $pngGate = Test-RasterThresholds -ScenarioId $scenarioId -FormatName 'png' -ComparisonSummary $pngSummary -Thresholds $scenario.thresholds.png
        $jpgGate = Test-RasterThresholds -ScenarioId $scenarioId -FormatName 'jpg' -ComparisonSummary $jpgSummary -Thresholds $scenario.thresholds.jpg

        foreach ($message in @($pngGate.failures) + @($jpgGate.failures)) {
            $failureMessages.Add($message) | Out-Null
        }

        $scenarioResult = [ordered]@{
            id = $scenarioId
            dotPath = $resolvedDotPath
            renderer = $renderer
            layoutEngine = $layoutEngine
            maxComparisonDimension = $maxComparisonDimension
            differentPixelThreshold = $differentPixelThreshold
            outputDirectory = $scenarioOutputDirectory
            outputs = [ordered]@{
                nativePng = $candidatePngPath
                graphvizPng = $baselinePngPath
                nativeJpg = $candidateJpgPath
                graphvizJpg = $baselineJpgPath
                pngDiff = $pngDiffPath
                jpgDiff = $jpgDiffPath
            }
            sizes = [ordered]@{
                nativePngBytes = (Get-Item -LiteralPath $candidatePngPath).Length
                graphvizPngBytes = (Get-Item -LiteralPath $baselinePngPath).Length
                nativeJpgBytes = (Get-Item -LiteralPath $candidateJpgPath).Length
                graphvizJpgBytes = (Get-Item -LiteralPath $baselineJpgPath).Length
                pngDiffBytes = (Get-Item -LiteralPath $pngDiffPath).Length
                jpgDiffBytes = (Get-Item -LiteralPath $jpgDiffPath).Length
            }
            thresholds = [ordered]@{
                png = $scenario.thresholds.png
                jpg = $scenario.thresholds.jpg
            }
            png = [ordered]@{
                comparison = $pngSummary
                gate = $pngGate
            }
            jpg = [ordered]@{
                comparison = $jpgSummary
                gate = $jpgGate
            }
            passed = ([bool]$pngGate.passed -and [bool]$jpgGate.passed)
        }

        $scenarioSummaryPath = Join-Path $scenarioOutputDirectory 'raster-quality-result.json'
        $scenarioResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $scenarioSummaryPath -Encoding utf8
        $scenarioResults.Add([pscustomobject]$scenarioResult) | Out-Null
    }

    $finishedAt = [DateTimeOffset]::Now
    $summary = [ordered]@{
        moduleManifestPath = $resolvedModuleManifestPath
        moduleRoot = $moduleRoot
        thresholdManifestPath = $resolvedThresholdManifestPath
        fixtureRoot = $resolvedFixtureRoot
        outputDirectory = $resolvedOutputDirectory
        graphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH
        requireBundledGraphvizRuntime = [bool]$RequireBundledGraphvizRuntime
        graphvizDotCommandPath = $dotCommandPath
        startedAt = $startedAt.ToString('O')
        finishedAt = $finishedAt.ToString('O')
        durationMs = [math]::Round(($finishedAt - $startedAt).TotalMilliseconds, 2)
        scenarioCount = $scenarioResults.Count
        failureCount = $failureMessages.Count
        succeeded = ($failureMessages.Count -eq 0)
        failures = @($failureMessages)
        scenarios = $scenarioResults
    }

    $summaryPath = Join-Path $resolvedOutputDirectory 'raster-quality-gate-results.json'
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    Write-Host "Raster quality gate summary: $summaryPath"

    if ($failureMessages.Count -gt 0) {
        throw ("Raster quality gate failed:`n- " + ($failureMessages -join "`n- "))
    }

    if ($PassThru) {
        return [pscustomobject]$summary
    }
}
finally {
    if ($null -eq $originalDotPath) {
        Remove-Item Env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH = $originalDotPath
    }

    if ($null -eq $originalGraphvizNativeLibraryPath) {
        Remove-Item Env:PSGRAPHVIEW_PSGV_LIBRARY_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = $originalGraphvizNativeLibraryPath
    }
}
