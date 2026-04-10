param(
    [string]$GraphvizSourceRoot,

    [string]$ManifestPath = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz/manifest.json'),

    [string]$DestinationRoot = (Join-Path $PSScriptRoot '../tests/Fixtures/Graphviz'),

    [ValidateSet('all', 'core', 'extended')]
    [string]$Tier = 'all',

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

function Resolve-GraphvizSourceRoot {
    param(
        [string]$ExplicitPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        return Resolve-RequiredPath -PathValue $ExplicitPath -Description 'Graphviz source root'
    }

    $environmentPath = $env:PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR
    if (-not [string]::IsNullOrWhiteSpace($environmentPath) -and (Test-Path -LiteralPath $environmentPath)) {
        return (Resolve-Path -LiteralPath $environmentPath).Path
    }

    throw "Graphviz source root was not provided. Pass -GraphvizSourceRoot or set PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR."
}

$resolvedGraphvizSourceRoot = Resolve-GraphvizSourceRoot -ExplicitPath $GraphvizSourceRoot
$resolvedManifestPath = Resolve-RequiredPath -PathValue $ManifestPath -Description 'Fixture manifest'
$resolvedDestinationRoot = [System.IO.Path]::GetFullPath($DestinationRoot)

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
if ($null -eq $manifest -or $null -eq $manifest.fixtures) {
    throw "Fixture manifest '$resolvedManifestPath' does not contain a fixtures array."
}

$selectedFixtures = @($manifest.fixtures)
if ($Tier -ne 'all') {
    $selectedFixtures = @($selectedFixtures | Where-Object { $_.tier -eq $Tier })
}

if ($selectedFixtures.Count -eq 0) {
    throw "No fixtures matched tier '$Tier'."
}

if ($Clean) {
    $directoriesToClean = if ($Tier -eq 'all') { @('core', 'extended') } else { @($Tier) }
    foreach ($relativeDirectory in $directoriesToClean) {
        $directoryPath = Join-Path $resolvedDestinationRoot $relativeDirectory
        if (Test-Path -LiteralPath $directoryPath) {
            Remove-Item -LiteralPath $directoryPath -Recurse -Force
        }
    }
}

New-Item -ItemType Directory -Path $resolvedDestinationRoot -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]
foreach ($fixture in $selectedFixtures) {
    $sourcePath = Join-Path $resolvedGraphvizSourceRoot $fixture.sourcePath
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Fixture '$($fixture.id)' source '$sourcePath' was not found."
    }

    $destinationPath = Join-Path $resolvedDestinationRoot $fixture.destinationPath
    $destinationDirectory = Split-Path -Parent $destinationPath
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force

    $copiedFixture = [pscustomobject]@{
        Id = [string]$fixture.id
        Tier = [string]$fixture.tier
        Renderer = [string]$fixture.renderer
        SourcePath = [string]$fixture.sourcePath
        DestinationPath = [System.IO.Path]::GetRelativePath($resolvedDestinationRoot, $destinationPath)
    }

    $results.Add($copiedFixture) | Out-Null
    Write-Host "Synced fixture '$($copiedFixture.Id)' -> '$($copiedFixture.DestinationPath)'."
}

if ($PassThru) {
    $results
}
