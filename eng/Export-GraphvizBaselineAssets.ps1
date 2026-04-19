param(
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../tests/Baselines/Graphviz/manifest.json'),

    [string]$FixtureRoot = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz'),

    [string]$OutputRoot = (Join-Path $PSScriptRoot '../tests/Baselines/Graphviz'),

    [string]$DotCommandPath,

    [switch]$Clean,

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

function Get-DotCommandPath {
    param(
        [string]$ExplicitPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        return Resolve-RequiredPath -PathValue $ExplicitPath -Description 'dot command'
    }

    $command = Get-Command dot -CommandType Application -ErrorAction Stop | Select-Object -First 1
    if ($null -eq $command -or [string]::IsNullOrWhiteSpace($command.Path)) {
        throw "The 'dot' command was not found."
    }

    return $command.Path
}

function Assert-ScenarioManifest {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Scenarios
    )

    $idSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $baselinePathSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($scenario in $Scenarios) {
        if ([string]::IsNullOrWhiteSpace([string]$scenario.id)) {
            throw 'Baseline manifest contains a scenario with an empty id.'
        }

        if (-not $idSet.Add([string]$scenario.id)) {
            throw "Baseline manifest contains a duplicate id '$($scenario.id)'."
        }

        if ([string]::IsNullOrWhiteSpace([string]$scenario.inputPath)) {
            throw "Baseline scenario '$($scenario.id)' does not define inputPath."
        }

        if ([string]::IsNullOrWhiteSpace([string]$scenario.baselinePath)) {
            throw "Baseline scenario '$($scenario.id)' does not define baselinePath."
        }

        if (-not $baselinePathSet.Add([string]$scenario.baselinePath)) {
            throw "Baseline manifest contains a duplicate baselinePath '$($scenario.baselinePath)'."
        }
    }
}

function New-BaselineHashInfo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $item = Get-Item -LiteralPath $PathValue
    $hash = Get-FileHash -LiteralPath $PathValue -Algorithm SHA256
    return [ordered]@{
        path = $RelativePath
        bytes = $item.Length
        sha256 = $hash.Hash.ToLowerInvariant()
    }
}

$resolvedManifestPath = Resolve-RequiredPath -PathValue $ManifestPath -Description 'Baseline manifest'
$resolvedFixtureRoot = Resolve-RequiredPath -PathValue $FixtureRoot -Description 'Fixture root'
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$resolvedDotCommandPath = Get-DotCommandPath -ExplicitPath $DotCommandPath
$dotCommandName = Split-Path -Leaf $resolvedDotCommandPath

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
if ($null -eq $manifest -or $null -eq $manifest.scenarios) {
    throw "Baseline manifest '$resolvedManifestPath' does not contain a scenarios array."
}

$scenarios = @($manifest.scenarios)
Assert-ScenarioManifest -Scenarios $scenarios

$assetsRoot = Join-Path $resolvedOutputRoot 'assets'
if ($Clean -and (Test-Path -LiteralPath $assetsRoot)) {
    Remove-Item -LiteralPath $assetsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $assetsRoot -Force | Out-Null

$startedAt = [DateTimeOffset]::Now
$results = New-Object System.Collections.Generic.List[object]
foreach ($scenario in $scenarios) {
    $scenarioInputPath = Join-Path $resolvedFixtureRoot ([string]$scenario.inputPath)
    $resolvedScenarioInputPath = Resolve-RequiredPath -PathValue $scenarioInputPath -Description "Baseline input '$($scenario.id)'"
    $scenarioOutputDirectory = Join-Path $resolvedOutputRoot ([string]$scenario.baselinePath)

    if (Test-Path -LiteralPath $scenarioOutputDirectory) {
        Remove-Item -LiteralPath $scenarioOutputDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $scenarioOutputDirectory -Force | Out-Null

    $engine = ([string]$scenario.renderer).ToLowerInvariant()
    $svgPath = Join-Path $scenarioOutputDirectory 'graphviz.svg'
    $pngPath = Join-Path $scenarioOutputDirectory 'graphviz.png'
    $jpgPath = Join-Path $scenarioOutputDirectory 'graphviz.jpg'

    & $resolvedDotCommandPath "-K$engine" -Tsvg $resolvedScenarioInputPath -o $svgPath
    if ($LASTEXITCODE -ne 0) {
        throw "Graphviz baseline generation failed for scenario '$($scenario.id)' (svg)."
    }

    & $resolvedDotCommandPath "-K$engine" -Tpng $resolvedScenarioInputPath -o $pngPath
    if ($LASTEXITCODE -ne 0) {
        throw "Graphviz baseline generation failed for scenario '$($scenario.id)' (png)."
    }

    & $resolvedDotCommandPath "-K$engine" -Tjpg $resolvedScenarioInputPath -o $jpgPath
    if ($LASTEXITCODE -ne 0) {
        throw "Graphviz baseline generation failed for scenario '$($scenario.id)' (jpg)."
    }

    $scenarioMetadata = [ordered]@{
        id = [string]$scenario.id
        renderer = [string]$scenario.renderer
        inputPath = [string]$scenario.inputPath
        baselinePath = [string]$scenario.baselinePath
        tags = @($scenario.tags)
        generatedAt = [DateTimeOffset]::Now.ToString('O')
        dotCommand = $dotCommandName
        outputs = [ordered]@{
            svg = New-BaselineHashInfo -PathValue $svgPath -RelativePath ([System.IO.Path]::GetRelativePath($resolvedOutputRoot, $svgPath))
            png = New-BaselineHashInfo -PathValue $pngPath -RelativePath ([System.IO.Path]::GetRelativePath($resolvedOutputRoot, $pngPath))
            jpg = New-BaselineHashInfo -PathValue $jpgPath -RelativePath ([System.IO.Path]::GetRelativePath($resolvedOutputRoot, $jpgPath))
        }
    }

    $metadataPath = Join-Path $scenarioOutputDirectory 'baseline-metadata.json'
    $scenarioMetadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $metadataPath -Encoding utf8

    $results.Add([pscustomobject]$scenarioMetadata) | Out-Null
    Write-Host "Generated baseline '$($scenario.id)' -> '$scenarioOutputDirectory'."
}

$finishedAt = [DateTimeOffset]::Now
$summary = [ordered]@{
    manifestPath = $ManifestPath
    fixtureRoot = $FixtureRoot
    outputRoot = $OutputRoot
    dotCommand = $dotCommandName
    startedAt = $startedAt.ToString('O')
    finishedAt = $finishedAt.ToString('O')
    durationMs = [Math]::Round(($finishedAt - $startedAt).TotalMilliseconds, 2)
    scenarioCount = $results.Count
    scenarios = $results
}

$summaryPath = Join-Path $resolvedOutputRoot 'baseline-summary.json'
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host "Baseline summary: $summaryPath"

if ($PassThru) {
    [pscustomobject]$summary
}
