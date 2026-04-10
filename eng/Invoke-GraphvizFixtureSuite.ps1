param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleManifestPath,

    [string]$ManifestPath = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz/manifest.json'),

    [string]$FixtureRoot = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz'),

    [ValidateSet('all', 'core', 'extended')]
    [string]$Tier = 'core',

    [string[]]$FixtureId,

    [string]$OutputDirectory,

    [string]$GraphvizNativeLibraryPath,

    [switch]$RequireBundledGraphvizRuntime,

    [switch]$StopOnFirstFailure,

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

function Test-NullOrWhiteSpace {
    param(
        [AllowNull()]
        [string]$Value
    )

    return [string]::IsNullOrWhiteSpace($Value)
}

function Get-SelectedFixtures {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Fixtures,

        [Parameter(Mandatory = $true)]
        [string]$SelectedTier,

        [string[]]$SelectedIds
    )

    $selected = if ($SelectedTier -eq 'all') {
        @($Fixtures)
    }
    else {
        @($Fixtures | Where-Object { $_.tier -eq $SelectedTier })
    }

    if ($null -ne $SelectedIds -and $SelectedIds.Count -gt 0) {
        $idSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($id in $SelectedIds) {
            if (-not [string]::IsNullOrWhiteSpace($id)) {
                $idSet.Add($id) | Out-Null
            }
        }

        $selected = @($selected | Where-Object { $idSet.Contains([string]$_.id) })
    }

    return $selected
}

function Assert-FixturesAreWellFormed {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Fixtures
    )

    $idSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $destinationSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($fixture in $Fixtures) {
        if (Test-NullOrWhiteSpace $fixture.id) {
            throw 'Fixture manifest contains an entry with an empty id.'
        }

        if (-not $idSet.Add([string]$fixture.id)) {
            throw "Fixture manifest contains a duplicate id '$($fixture.id)'."
        }

        if (Test-NullOrWhiteSpace $fixture.destinationPath) {
            throw "Fixture '$($fixture.id)' does not define destinationPath."
        }

        if (-not $destinationSet.Add([string]$fixture.destinationPath)) {
            throw "Fixture manifest contains a duplicate destinationPath '$($fixture.destinationPath)'."
        }
    }
}

if ($RequireBundledGraphvizRuntime -and -not [string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    throw 'GraphvizNativeLibraryPath cannot be used together with RequireBundledGraphvizRuntime.'
}

$resolvedModuleManifestPath = Resolve-RequiredPath -PathValue $ModuleManifestPath -Description 'Module manifest'
$resolvedManifestPath = Resolve-RequiredPath -PathValue $ManifestPath -Description 'Fixture manifest'
$resolvedFixtureRoot = Resolve-RequiredPath -PathValue $FixtureRoot -Description 'Fixture root'
$resolvedGraphvizNativeLibraryPath = if ([string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    $null
}
else {
    Resolve-RequiredPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-graphviz-fixture-suite-" + [Guid]::NewGuid().ToString('N'))
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
if ($null -eq $manifest -or $null -eq $manifest.fixtures) {
    throw "Fixture manifest '$resolvedManifestPath' does not contain a fixtures array."
}

$allFixtures = @($manifest.fixtures)
Assert-FixturesAreWellFormed -Fixtures $allFixtures
$selectedFixtures = @(Get-SelectedFixtures -Fixtures $allFixtures -SelectedTier $Tier -SelectedIds $FixtureId)

if ($selectedFixtures.Count -eq 0) {
    throw "No fixtures matched tier '$Tier'."
}

$smokeScriptPath = Resolve-RequiredPath -PathValue (Join-Path $PSScriptRoot 'Test-GraphvizNoSystemSmoke.ps1') -Description 'Smoke script'
$fixtureResults = New-Object System.Collections.Generic.List[object]
$failureCount = 0
$suiteStartedAt = [DateTimeOffset]::Now

foreach ($fixture in $selectedFixtures) {
    $fixtureInputPath = Join-Path $resolvedFixtureRoot $fixture.destinationPath
    $resolvedFixtureInputPath = Resolve-RequiredPath -PathValue $fixtureInputPath -Description "Fixture '$($fixture.id)' input"
    $fixtureOutputDirectory = Join-Path $resolvedOutputDirectory ([string]$fixture.id)
    New-Item -ItemType Directory -Path $fixtureOutputDirectory -Force | Out-Null

    $resultsJsonPath = Join-Path $fixtureOutputDirectory 'smoke-results.json'
    $logPath = Join-Path $fixtureOutputDirectory 'smoke.log'

    $pwshArguments = @(
        '-NoLogo',
        '-NoProfile',
        '-File',
        $smokeScriptPath,
        '-ModuleManifestPath',
        $resolvedModuleManifestPath,
        '-DotPath',
        $resolvedFixtureInputPath,
        '-OutputDirectory',
        $fixtureOutputDirectory,
        '-ResultsJsonPath',
        $resultsJsonPath
    )

    if ($RequireBundledGraphvizRuntime) {
        $pwshArguments += '-RequireBundledGraphvizRuntime'
    }
    elseif ($null -ne $resolvedGraphvizNativeLibraryPath) {
        $pwshArguments += @('-GraphvizNativeLibraryPath', $resolvedGraphvizNativeLibraryPath)
    }

    $startedAt = [DateTimeOffset]::Now
    $outputLines = @(& pwsh @pwshArguments 2>&1)
    $exitCode = $LASTEXITCODE
    $finishedAt = [DateTimeOffset]::Now

    $logText = ($outputLines | ForEach-Object { "$_" }) -join [Environment]::NewLine
    Set-Content -LiteralPath $logPath -Value $logText -Encoding utf8

    $smokeResults = if (Test-Path -LiteralPath $resultsJsonPath) {
        Get-Content -LiteralPath $resultsJsonPath -Raw | ConvertFrom-Json
    }
    else {
        $null
    }

    $fixtureSucceeded = ($exitCode -eq 0)
    if (-not $fixtureSucceeded) {
        $failureCount++
    }

    $fixtureResult = [ordered]@{
        id = [string]$fixture.id
        tier = [string]$fixture.tier
        renderer = [string]$fixture.renderer
        sourcePath = [string]$fixture.sourcePath
        destinationPath = [string]$fixture.destinationPath
        inputPath = $resolvedFixtureInputPath
        outputDirectory = $fixtureOutputDirectory
        resultsJsonPath = $resultsJsonPath
        logPath = $logPath
        startedAt = $startedAt.ToString('O')
        finishedAt = $finishedAt.ToString('O')
        durationMs = [Math]::Round(($finishedAt - $startedAt).TotalMilliseconds, 2)
        exitCode = $exitCode
        succeeded = $fixtureSucceeded
        smokeResults = $smokeResults
    }

    $fixtureResults.Add([pscustomobject]$fixtureResult) | Out-Null

    if ($fixtureSucceeded) {
        Write-Host "Fixture '$($fixture.id)' passed."
    }
    else {
        Write-Warning "Fixture '$($fixture.id)' failed with exit code $exitCode. See '$logPath'."
        if ($StopOnFirstFailure) {
            break
        }
    }
}

$suiteFinishedAt = [DateTimeOffset]::Now
$suiteSummary = [ordered]@{
    moduleManifestPath = $resolvedModuleManifestPath
    manifestPath = $resolvedManifestPath
    fixtureRoot = $resolvedFixtureRoot
    outputDirectory = $resolvedOutputDirectory
    selectedTier = $Tier
    selectedFixtureIds = @($FixtureId)
    requireBundledGraphvizRuntime = [bool]$RequireBundledGraphvizRuntime
    graphvizNativeLibraryPath = $resolvedGraphvizNativeLibraryPath
    startedAt = $suiteStartedAt.ToString('O')
    finishedAt = $suiteFinishedAt.ToString('O')
    durationMs = [Math]::Round(($suiteFinishedAt - $suiteStartedAt).TotalMilliseconds, 2)
    totalFixtureCount = $fixtureResults.Count
    failureCount = $failureCount
    successCount = $fixtureResults.Count - $failureCount
    fixtures = $fixtureResults
}

$suiteSummaryPath = Join-Path $resolvedOutputDirectory 'fixture-suite-results.json'
$suiteSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $suiteSummaryPath -Encoding utf8

Write-Host "Fixture suite summary: $suiteSummaryPath"

if ($PassThru) {
    [pscustomobject]$suiteSummary
}

if ($failureCount -gt 0) {
    throw "$failureCount fixture(s) failed."
}
