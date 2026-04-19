[CmdletBinding(DefaultParameterSetName = 'Installed')]
param(
    [Parameter(ParameterSetName = 'Manifest', Mandatory = $true)]
    [string]$ModuleManifestPath,

    [Parameter(ParameterSetName = 'Installed')]
    [ValidateNotNullOrEmpty()]
    [string]$ModuleName = 'PSGraphView',

    [Parameter(ParameterSetName = 'Installed')]
    [string]$RequiredVersion,

    [string]$BaselineManifestPath = (Join-Path $PSScriptRoot '../tests/Baselines/Graphviz/manifest.json'),

    [string]$FixtureRoot = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz'),

    [string]$BaselineRoot = (Join-Path $PSScriptRoot '../tests/Baselines/Graphviz'),

    [string]$OutputDirectory,

    [string]$GraphvizNativeLibraryPath,

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

function Get-FileSizeOrNull {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if (-not (Test-Path -LiteralPath $PathValue)) {
        return $null
    }

    return (Get-Item -LiteralPath $PathValue).Length
}

function Assert-ScenarioManifest {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Scenarios
    )

    $idSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($scenario in $Scenarios) {
        if ([string]::IsNullOrWhiteSpace([string]$scenario.id)) {
            throw 'Baseline manifest contains a scenario with an empty id.'
        }

        if (-not $idSet.Add([string]$scenario.id)) {
            throw "Baseline manifest contains a duplicate id '$($scenario.id)'."
        }
    }
}

function Assert-FileStartsWith {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,

        [Parameter(Mandatory = $true)]
        [byte[]]$ExpectedPrefix
    )

    $bytes = [System.IO.File]::ReadAllBytes($PathValue)
    if ($bytes.Length -lt $ExpectedPrefix.Length) {
        throw "File '$PathValue' is shorter than the expected signature."
    }

    for ($index = 0; $index -lt $ExpectedPrefix.Length; $index++) {
        if ($bytes[$index] -ne $ExpectedPrefix[$index]) {
            throw "File '$PathValue' does not match the expected binary signature."
        }
    }

    return $bytes
}

function Get-ThresholdResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Comparison,

        [AllowNull()]
        [object]$Thresholds
    )

    if ($null -eq $Thresholds) {
        return [pscustomobject]@{
            enforced = $false
            succeeded = $true
            failures = @()
        }
    }

    $failures = New-Object System.Collections.Generic.List[string]
    if ($null -ne $Thresholds.rootMeanSquareDifferenceMax -and
        $Comparison.Metrics.RootMeanSquareDifference -gt [double]$Thresholds.rootMeanSquareDifferenceMax) {
        $failures.Add("rmse=$([Math]::Round($Comparison.Metrics.RootMeanSquareDifference, 4)) exceeds $($Thresholds.rootMeanSquareDifferenceMax)") | Out-Null
    }

    if ($null -ne $Thresholds.differentPixelPercentMax -and
        ($Comparison.Metrics.DifferentPixelRatio * 100.0) -gt [double]$Thresholds.differentPixelPercentMax) {
        $failures.Add("differentPixel%=$([Math]::Round($Comparison.Metrics.DifferentPixelRatio * 100.0, 4)) exceeds $($Thresholds.differentPixelPercentMax)") | Out-Null
    }

    if ($null -ne $Thresholds.globalStructuralSimilarityMin -and
        $Comparison.Metrics.GlobalStructuralSimilarity -lt [double]$Thresholds.globalStructuralSimilarityMin) {
        $failures.Add("ssim=$([Math]::Round($Comparison.Metrics.GlobalStructuralSimilarity, 6)) is below $($Thresholds.globalStructuralSimilarityMin)") | Out-Null
    }

    return [pscustomobject]@{
        enforced = $true
        succeeded = ($failures.Count -eq 0)
        failures = @($failures)
    }
}

function Convert-ComparisonResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Comparison
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
            meanAbsoluteDifference = [Math]::Round($Comparison.Metrics.MeanAbsoluteDifference, 4)
            rootMeanSquareDifference = [Math]::Round($Comparison.Metrics.RootMeanSquareDifference, 4)
            maxAbsoluteDifference = [Math]::Round($Comparison.Metrics.MaxAbsoluteDifference, 4)
            differentPixelRatio = [Math]::Round($Comparison.Metrics.DifferentPixelRatio, 6)
            differentPixelPercent = [Math]::Round($Comparison.Metrics.DifferentPixelRatio * 100.0, 4)
            globalStructuralSimilarity = [Math]::Round($Comparison.Metrics.GlobalStructuralSimilarity, 6)
        }
    }
}

function Get-SvgTelemetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaselinePath,

        [Parameter(Mandatory = $true)]
        [string]$CandidatePath
    )

    $baselineSvg = Get-Content -LiteralPath $BaselinePath -Raw
    $candidateSvg = Get-Content -LiteralPath $CandidatePath -Raw

    if ($candidateSvg -notmatch '<svg\b') {
        throw "Candidate SVG '$CandidatePath' does not contain an <svg> root element."
    }

    $viewBoxPattern = 'viewBox="([^"]+)"'
    $widthPattern = 'width="([^"]+)"'
    $heightPattern = 'height="([^"]+)"'

    return [ordered]@{
        baselineBytes = (Get-Item -LiteralPath $BaselinePath).Length
        candidateBytes = (Get-Item -LiteralPath $CandidatePath).Length
        baselineViewBox = ([regex]::Match($baselineSvg, $viewBoxPattern).Groups[1].Value)
        candidateViewBox = ([regex]::Match($candidateSvg, $viewBoxPattern).Groups[1].Value)
        baselineWidth = ([regex]::Match($baselineSvg, $widthPattern).Groups[1].Value)
        candidateWidth = ([regex]::Match($candidateSvg, $widthPattern).Groups[1].Value)
        baselineHeight = ([regex]::Match($baselineSvg, $heightPattern).Groups[1].Value)
        candidateHeight = ([regex]::Match($candidateSvg, $heightPattern).Groups[1].Value)
        candidateContainsSvgRoot = $true
    }
}

$resolvedBaselineManifestPath = Resolve-RequiredPath -PathValue $BaselineManifestPath -Description 'Baseline manifest'
$resolvedFixtureRoot = Resolve-RequiredPath -PathValue $FixtureRoot -Description 'Fixture root'
$resolvedBaselineRoot = Resolve-RequiredPath -PathValue $BaselineRoot -Description 'Baseline root'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-baseline-suite-" + [Guid]::NewGuid().ToString('N'))
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

$manifest = Get-Content -LiteralPath $resolvedBaselineManifestPath -Raw | ConvertFrom-Json
if ($null -eq $manifest -or $null -eq $manifest.scenarios) {
    throw "Baseline manifest '$resolvedBaselineManifestPath' does not contain a scenarios array."
}

$scenarios = @($manifest.scenarios)
Assert-ScenarioManifest -Scenarios $scenarios

if ($PSBoundParameters.ContainsKey('ModuleManifestPath')) {
    $resolvedModuleManifestPath = Resolve-RequiredPath -PathValue $ModuleManifestPath -Description 'Module manifest'
    Import-Module $resolvedModuleManifestPath -Force -ErrorAction Stop
}
else {
    $importParameters = @{
        Name = $ModuleName
        Force = $true
        ErrorAction = 'Stop'
    }

    if (-not [string]::IsNullOrWhiteSpace($RequiredVersion)) {
        $importParameters.RequiredVersion = $RequiredVersion
    }

    Import-Module @importParameters
    $resolvedModuleManifestPath = (Get-Module $ModuleName | Select-Object -First 1 -ExpandProperty Path)
    if ([string]::IsNullOrWhiteSpace($resolvedModuleManifestPath)) {
        throw "Imported module '$ModuleName' did not expose a manifest path."
    }
}

$modulePath = Get-Module PSGraphView | Select-Object -First 1 -ExpandProperty Path
if ([string]::IsNullOrWhiteSpace($modulePath)) {
    throw 'PSGraphView module path could not be resolved after import.'
}

$graphvizAssemblyPath = Join-Path (Split-Path -Parent $modulePath) 'PSGraphView.Graphviz.dll'
$resolvedGraphvizAssemblyPath = Resolve-RequiredPath -PathValue $graphvizAssemblyPath -Description 'PSGraphView.Graphviz assembly'
[void][System.Reflection.Assembly]::LoadFrom($resolvedGraphvizAssemblyPath)

$graphvizLayoutEngineType = [System.Type]::GetType('PSGraphView.PowerShell.GraphvizLayoutEngine, PSGraphView.PowerShell', $true)
$rasterComparisonOptionsType = [System.Type]::GetType('PSGraphView.Graphviz.RasterComparisonOptions, PSGraphView.Graphviz', $true)
$rasterImageComparerType = [System.Type]::GetType('PSGraphView.Graphviz.RasterImageComparer, PSGraphView.Graphviz', $true)

if (-not [string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = Resolve-RequiredPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
}

$comparisonOptions = [System.Activator]::CreateInstance($rasterComparisonOptionsType, @(0, [byte]8))
$startedAt = [DateTimeOffset]::Now
$scenarioResults = New-Object System.Collections.Generic.List[object]
$failureMessages = New-Object System.Collections.Generic.List[string]

foreach ($scenario in $scenarios) {
    $scenarioInputPath = Resolve-RequiredPath -PathValue (Join-Path $resolvedFixtureRoot ([string]$scenario.inputPath)) -Description "Scenario '$($scenario.id)' input"
    $scenarioBaselineDirectory = Resolve-RequiredPath -PathValue (Join-Path $resolvedBaselineRoot ([string]$scenario.baselinePath)) -Description "Scenario '$($scenario.id)' baseline directory"
    $scenarioOutputDirectory = Join-Path $resolvedOutputDirectory ([string]$scenario.id)
    New-Item -ItemType Directory -Path $scenarioOutputDirectory -Force | Out-Null

    $baselineSvgPath = Resolve-RequiredPath -PathValue (Join-Path $scenarioBaselineDirectory 'graphviz.svg') -Description "Scenario '$($scenario.id)' baseline svg"
    $baselinePngPath = Resolve-RequiredPath -PathValue (Join-Path $scenarioBaselineDirectory 'graphviz.png') -Description "Scenario '$($scenario.id)' baseline png"
    $baselineJpgPath = Resolve-RequiredPath -PathValue (Join-Path $scenarioBaselineDirectory 'graphviz.jpg') -Description "Scenario '$($scenario.id)' baseline jpg"

    $candidateSvgPath = Join-Path $scenarioOutputDirectory 'candidate.svg'
    $candidatePngPath = Join-Path $scenarioOutputDirectory 'candidate.png'
    $candidateJpgPath = Join-Path $scenarioOutputDirectory 'candidate.jpg'
    $pngDiffPath = Join-Path $scenarioOutputDirectory 'diff-png.png'
    $jpgDiffPath = Join-Path $scenarioOutputDirectory 'diff-jpg.png'

    $renderer = [System.Enum]::Parse($graphvizLayoutEngineType, [string]$scenario.renderer)

    Export-GraphvizView -DotPath $scenarioInputPath -Renderer $renderer -As Svg -OutputPath $candidateSvgPath | Out-Null
    Export-GraphvizView -DotPath $scenarioInputPath -Renderer $renderer -As Png -OutputPath $candidatePngPath | Out-Null
    Export-GraphvizView -DotPath $scenarioInputPath -Renderer $renderer -As Jpg -OutputPath $candidateJpgPath | Out-Null

    $candidatePngBytes = Assert-FileStartsWith -PathValue $candidatePngPath -ExpectedPrefix ([byte[]](0x89, 0x50, 0x4E, 0x47))
    $candidateJpgBytes = Assert-FileStartsWith -PathValue $candidateJpgPath -ExpectedPrefix ([byte[]](0xFF, 0xD8))

    $baselinePngBytes = [System.IO.File]::ReadAllBytes($baselinePngPath)
    $baselineJpgBytes = [System.IO.File]::ReadAllBytes($baselineJpgPath)

    $pngComparison = $rasterImageComparerType::Compare($baselinePngBytes, $candidatePngBytes, $comparisonOptions)
    $jpgComparison = $rasterImageComparerType::Compare($baselineJpgBytes, $candidateJpgBytes, $comparisonOptions)

    [System.IO.File]::WriteAllBytes(
        $pngDiffPath,
        $rasterImageComparerType::RenderDiffPng($baselinePngBytes, $candidatePngBytes, $comparisonOptions))
    [System.IO.File]::WriteAllBytes(
        $jpgDiffPath,
        $rasterImageComparerType::RenderDiffPng($baselineJpgBytes, $candidateJpgBytes, $comparisonOptions))

    $rasterThresholds = $null
    if ($null -ne $scenario.PSObject.Properties['rasterThresholds']) {
        $rasterThresholds = $scenario.rasterThresholds
    }

    $pngThresholds = $null
    $jpgThresholds = $null
    if ($null -ne $rasterThresholds) {
        if ($null -ne $rasterThresholds.PSObject.Properties['png']) {
            $pngThresholds = $rasterThresholds.png
        }

        if ($null -ne $rasterThresholds.PSObject.Properties['jpg']) {
            $jpgThresholds = $rasterThresholds.jpg
        }
    }

    $pngThresholdResult = Get-ThresholdResult -Comparison $pngComparison -Thresholds $pngThresholds
    $jpgThresholdResult = Get-ThresholdResult -Comparison $jpgComparison -Thresholds $jpgThresholds
    $succeeded = $pngThresholdResult.succeeded -and $jpgThresholdResult.succeeded

    if (-not $succeeded) {
        foreach ($failure in @($pngThresholdResult.failures)) {
            $failureMessages.Add("Scenario '$($scenario.id)' png: $failure") | Out-Null
        }

        foreach ($failure in @($jpgThresholdResult.failures)) {
            $failureMessages.Add("Scenario '$($scenario.id)' jpg: $failure") | Out-Null
        }
    }

    $scenarioResult = [ordered]@{
        id = [string]$scenario.id
        renderer = [string]$scenario.renderer
        inputPath = $scenarioInputPath
        baselineDirectory = $scenarioBaselineDirectory
        outputDirectory = $scenarioOutputDirectory
        succeeded = $succeeded
        svg = Get-SvgTelemetry -BaselinePath $baselineSvgPath -CandidatePath $candidateSvgPath
        png = [ordered]@{
            baselineBytes = Get-FileSizeOrNull -PathValue $baselinePngPath
            candidateBytes = Get-FileSizeOrNull -PathValue $candidatePngPath
            diffPath = $pngDiffPath
            comparison = Convert-ComparisonResult -Comparison $pngComparison
            threshold = $pngThresholdResult
        }
        jpg = [ordered]@{
            baselineBytes = Get-FileSizeOrNull -PathValue $baselineJpgPath
            candidateBytes = Get-FileSizeOrNull -PathValue $candidateJpgPath
            diffPath = $jpgDiffPath
            comparison = Convert-ComparisonResult -Comparison $jpgComparison
            threshold = $jpgThresholdResult
        }
    }

    $scenarioResultPath = Join-Path $scenarioOutputDirectory 'baseline-compare-result.json'
    $scenarioResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $scenarioResultPath -Encoding utf8
    $scenarioResults.Add([pscustomobject]$scenarioResult) | Out-Null

    if ($succeeded) {
        Write-Host "Baseline scenario '$($scenario.id)' passed."
    }
    else {
        Write-Warning "Baseline scenario '$($scenario.id)' exceeded one or more raster thresholds."
    }
}

$finishedAt = [DateTimeOffset]::Now
$summary = [ordered]@{
    moduleManifestPath = $resolvedModuleManifestPath
    baselineManifestPath = $resolvedBaselineManifestPath
    fixtureRoot = $resolvedFixtureRoot
    baselineRoot = $resolvedBaselineRoot
    outputDirectory = $resolvedOutputDirectory
    startedAt = $startedAt.ToString('O')
    finishedAt = $finishedAt.ToString('O')
    durationMs = [Math]::Round(($finishedAt - $startedAt).TotalMilliseconds, 2)
    scenarioCount = $scenarioResults.Count
    failureCount = $failureMessages.Count
    scenarios = $scenarioResults
}

$summaryPath = Join-Path $resolvedOutputDirectory 'baseline-suite-results.json'
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host "Baseline suite summary: $summaryPath"

if ($failureMessages.Count -gt 0) {
    throw ("Baseline suite failed:`n- " + ($failureMessages -join "`n- "))
}

if ($PassThru) {
    [pscustomobject]$summary
}
