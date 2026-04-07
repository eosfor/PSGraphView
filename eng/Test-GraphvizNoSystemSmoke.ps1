param(
    [Parameter(Mandatory = $true)]
    [string]$ModuleManifestPath,

    [string]$OutputDirectory,

    [string]$DotPath,

    [string]$Dot = 'digraph G { graph [rankdir=LR]; A -> B [label="edge"]; }',

    [string]$GraphvizNativeLibraryPath,

    [switch]$RequireBundledGraphvizRuntime,

    [string]$ResultsJsonPath
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

function New-FailingDotShim {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath
    )

    New-Item -ItemType Directory -Path $DirectoryPath -Force | Out-Null

    if ($IsWindows) {
        $shimPath = Join-Path $DirectoryPath 'dot.cmd'
        Set-Content -LiteralPath $shimPath -Encoding Ascii -Value @(
            '@echo off'
            'echo Graphviz process fallback is forbidden in no-system-graphviz smoke tests. 1>&2'
            'exit /b 86'
        )

        return $shimPath
    }

    $shimPath = Join-Path $DirectoryPath 'dot'
    Set-Content -LiteralPath $shimPath -Encoding Ascii -Value @(
        '#!/usr/bin/env sh'
        'echo "Graphviz process fallback is forbidden in no-system-graphviz smoke tests." >&2'
        'exit 86'
    )
    & chmod +x $shimPath

    return $shimPath
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

    return $bytes.Length
}

$resolvedManifestPath = Resolve-RequiredPath -PathValue $ModuleManifestPath -Description 'Module manifest'
$moduleRoot = Split-Path -Parent $resolvedManifestPath

if ($PSBoundParameters.ContainsKey('DotPath')) {
    $resolvedDotPath = Resolve-RequiredPath -PathValue $DotPath -Description 'DOT input'
    $Dot = Get-Content -LiteralPath $resolvedDotPath -Raw
}
else {
    $resolvedDotPath = $null
}

if ($RequireBundledGraphvizRuntime -and $PSBoundParameters.ContainsKey('GraphvizNativeLibraryPath')) {
    throw 'GraphvizNativeLibraryPath cannot be used together with RequireBundledGraphvizRuntime.'
}

if ($PSBoundParameters.ContainsKey('GraphvizNativeLibraryPath')) {
    $resolvedGraphvizNativeLibraryPath = Resolve-RequiredPath -PathValue $GraphvizNativeLibraryPath -Description 'Graphviz native library'
}
else {
    $resolvedGraphvizNativeLibraryPath = $null
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-no-system-graphviz-" + [Guid]::NewGuid().ToString('N'))
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($ResultsJsonPath)) {
    $ResultsJsonPath = Join-Path $OutputDirectory 'smoke-results.json'
}

$shimDirectory = Join-Path $OutputDirectory 'fake-tools'
$dotShimPath = New-FailingDotShim -DirectoryPath $shimDirectory
$missingDotPath = Join-Path $shimDirectory $(if ($IsWindows) { 'missing-dot.exe' } else { 'missing-dot' })

$bundledGraphvizPath = $null
if ($RequireBundledGraphvizRuntime) {
    $runtimeIdentifier = Get-CurrentRuntimeIdentifier
    $nativeRoot = Join-Path $moduleRoot "runtimes/$runtimeIdentifier/native"
    $bundledGraphvizPath = Find-BundledGraphvizNativeLibraryPath -ModuleRoot $moduleRoot -RuntimeIdentifier $runtimeIdentifier

    if ($null -eq $bundledGraphvizPath) {
        throw "Bundled Graphviz runtime was not found under '$nativeRoot'."
    }
}

$jsonPath = Join-Path $OutputDirectory 'graph.json'
$svgPath = Join-Path $OutputDirectory 'graph.svg'
$pngPath = Join-Path $OutputDirectory 'graph.png'
$jpgPath = Join-Path $OutputDirectory 'graph.jpg'

$originalPath = $env:PATH
$originalDotPath = $env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH
$originalGraphvizNativeLibraryPath = $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH

try {
    $env:PATH = $shimDirectory + [System.IO.Path]::PathSeparator + $originalPath
    $env:PSGRAPHVIEW_GRAPHVIZ_DOT_PATH = $missingDotPath

    if ($null -eq $resolvedGraphvizNativeLibraryPath) {
        Remove-Item Env:PSGRAPHVIEW_PSGV_LIBRARY_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PSGRAPHVIEW_PSGV_LIBRARY_PATH = $resolvedGraphvizNativeLibraryPath
    }

    Import-Module $resolvedManifestPath -Force -ErrorAction Stop

    $json = Export-GraphvizView -InputObject $Dot -Renderer Dot -As Json
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw 'Graphviz JSON output was empty.'
    }

    if ($json -notmatch '"_draw_"') {
        throw 'Graphviz JSON output does not contain draw commands.'
    }

    Set-Content -LiteralPath $jsonPath -Value $json -Encoding utf8

    Export-GraphvizView -InputObject $Dot -Renderer Dot -As Svg -OutputPath $svgPath
    Export-GraphvizView -InputObject $Dot -Renderer Dot -As Png -OutputPath $pngPath
    Export-GraphvizView -InputObject $Dot -Renderer Dot -As Jpg -OutputPath $jpgPath

    $svg = Get-Content -LiteralPath $svgPath -Raw
    if ($svg -notmatch '<svg\b') {
        throw 'SVG output does not contain an <svg> root element.'
    }

    $pngBytes = Assert-FileStartsWith -PathValue $pngPath -ExpectedPrefix ([byte[]](0x89, 0x50, 0x4E, 0x47))
    $jpgBytes = Assert-FileStartsWith -PathValue $jpgPath -ExpectedPrefix ([byte[]](0xFF, 0xD8))

    $results = [ordered]@{
        moduleManifestPath = $resolvedManifestPath
        moduleRoot = $moduleRoot
        dotPath = $resolvedDotPath
        outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
        jsonPath = $jsonPath
        svgPath = $svgPath
        pngPath = $pngPath
        jpgPath = $jpgPath
        jsonLength = $json.Length
        svgBytes = (Get-Item -LiteralPath $svgPath).Length
        pngBytes = $pngBytes
        jpgBytes = $jpgBytes
        fakeDotShimPath = $dotShimPath
        fakeDotEnvironmentPath = $missingDotPath
        graphvizNativeLibraryPath = $resolvedGraphvizNativeLibraryPath
        bundledGraphvizNativeLibraryPath = $bundledGraphvizPath
        requireBundledGraphvizRuntime = [bool]$RequireBundledGraphvizRuntime
        runtimeIdentifier = Get-CurrentRuntimeIdentifier
        timestamp = [DateTimeOffset]::Now.ToString('O')
    }

    $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultsJsonPath -Encoding utf8
    Write-Host "Graphviz no-system smoke passed. Results: $ResultsJsonPath"
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

    $env:PATH = $originalPath
}
