param(
    [string]$ModuleManifestPath = './src/PSGraphView.PowerShell/bin/Debug/net8.0/PSGraphView.psd1',

    [string]$GraphvizNativeLibraryPath,

    [string]$StagingDirectory,

    [string]$OutputDirectory,

    [switch]$KeepStagingDirectory
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

$resolvedManifestPath = Resolve-RequiredPath -PathValue $ModuleManifestPath -Description 'Module manifest'

if ([string]::IsNullOrWhiteSpace($GraphvizNativeLibraryPath)) {
    $GraphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH
}

$resolvedGraphvizNativeLibraryPath = Resolve-RequiredPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
$moduleRoot = Split-Path -Parent $resolvedManifestPath
$runtimeIdentifier = Get-CurrentRuntimeIdentifier

if ([string]::IsNullOrWhiteSpace($StagingDirectory)) {
    $StagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-no-system-graphviz-stage-" + [Guid]::NewGuid().ToString('N'))
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-no-system-graphviz-output-" + [Guid]::NewGuid().ToString('N'))
}

$resolvedStagingDirectory = [System.IO.Path]::GetFullPath($StagingDirectory)
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if (Test-Path -LiteralPath $resolvedStagingDirectory) {
    Remove-Item -LiteralPath $resolvedStagingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedStagingDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $moduleRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $resolvedStagingDirectory -Recurse -Force
}

$nativeDestination = Join-Path $resolvedStagingDirectory "runtimes/$runtimeIdentifier/native"
New-Item -ItemType Directory -Path $nativeDestination -Force | Out-Null
Copy-Item -LiteralPath $resolvedGraphvizNativeLibraryPath -Destination (Join-Path $nativeDestination (Split-Path -Leaf $resolvedGraphvizNativeLibraryPath)) -Force

$stagedManifestPath = Join-Path $resolvedStagingDirectory 'PSGraphView.psd1'
$smokeScriptPath = Join-Path $PSScriptRoot 'Test-GraphvizNoSystemSmoke.ps1'

try {
    & $smokeScriptPath `
        -ModuleManifestPath $stagedManifestPath `
        -OutputDirectory $resolvedOutputDirectory `
        -RequireBundledGraphvizRuntime

    Write-Host "Local bundled no-system-graphviz smoke passed."
    Write-Host "Staged module root: $resolvedStagingDirectory"
    Write-Host "Smoke output: $resolvedOutputDirectory"
}
finally {
    if (-not $KeepStagingDirectory) {
        Remove-Item -LiteralPath $resolvedStagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
