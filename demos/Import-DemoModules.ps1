function Import-PSGraphViewDemoModules {
    [CmdletBinding()]
    param(
        [switch]$UseLocalModules,
        [string]$PSQuickGraphManifestPath,
        [string]$PSGraphViewManifestPath,
        [switch]$ImportLibSixel,
        [string]$LibSixelManifestPath
    )

    $repoRoot = Split-Path -Parent $PSScriptRoot
    $defaultPsQuickGraphManifest = Join-Path (Join-Path (Split-Path -Parent $repoRoot) 'PSGraph') 'PSGraph.Tests/bin/Debug/net9.0/PSQuickGraph.psd1'
    $defaultPsGraphViewManifest = Get-PSGraphViewPublishManifestPath -RepoRoot $repoRoot
    $defaultLibSixelManifest = Join-Path (Join-Path (Split-Path -Parent $repoRoot) 'libsixel') 'src/LibSixel.PowerShell/bin/Debug/net9.0/LibSixel.PowerShell.psd1'

    Remove-Module PSQuickGraph, PSGraphView, LibSixel.PowerShell -ErrorAction SilentlyContinue

    Import-DemoModule `
        -ModuleName 'PSQuickGraph' `
        -PreferredManifestPath $PSQuickGraphManifestPath `
        -DefaultManifestPath $defaultPsQuickGraphManifest `
        -UseLocalModules:$UseLocalModules `
        -ResolutionHint "Install-Module PSQuickGraph -Scope CurrentUser"

    Import-DemoModule `
        -ModuleName 'PSGraphView' `
        -PreferredManifestPath $PSGraphViewManifestPath `
        -DefaultManifestPath $defaultPsGraphViewManifest `
        -UseLocalModules:$UseLocalModules `
        -ResolutionHint "Install-Module PSGraphView -Scope CurrentUser"

    if ($ImportLibSixel) {
        Import-DemoModule `
            -ModuleName 'LibSixel.PowerShell' `
            -PreferredManifestPath $LibSixelManifestPath `
            -DefaultManifestPath $defaultLibSixelManifest `
            -UseLocalModules:$UseLocalModules `
            -ResolutionHint "Build libsixel locally with 'dotnet build ../libsixel/src/LibSixel.PowerShell/LibSixel.PowerShell.csproj' and rerun with -UseLocalModules or pass -LibSixelManifestPath."
    }
}

function Get-PSGraphViewPublishManifestPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $publishRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demo-publish'
    $publishDir = Join-Path $publishRoot 'PSGraphView.PowerShell'
    $tfmDir = Join-Path $publishDir 'net9.0'
    if (Test-Path $tfmDir) {
        return Join-Path $tfmDir 'PSGraphView.psd1'
    }

    return Join-Path $publishDir 'PSGraphView.psd1'
}

function Publish-PSGraphViewLocalModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $projectPath = Join-Path $RepoRoot 'src/PSGraphView.PowerShell/PSGraphView.PowerShell.csproj'
    $publishManifestPath = Get-PSGraphViewPublishManifestPath -RepoRoot $RepoRoot
    $publishDir = Split-Path -Parent $publishManifestPath

    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Write-Verbose "Publishing PSGraphView local module to '$publishDir'."
    dotnet publish $projectPath -c Debug -o $publishDir | Out-Host

    if (-not (Test-Path $publishManifestPath)) {
        throw "Published PSGraphView manifest was not found at '$publishManifestPath'."
    }

    return $publishManifestPath
}

function Import-DemoModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ModuleName,
        [string]$PreferredManifestPath,
        [string]$DefaultManifestPath,
        [switch]$UseLocalModules,
        [string]$ResolutionHint
    )

    if ($PreferredManifestPath) {
        $resolvedManifestPath = Resolve-ManifestPath -ManifestPath $PreferredManifestPath -ModuleName $ModuleName
        Write-Verbose "Loading $ModuleName from explicit manifest '$resolvedManifestPath'."
        Import-Module $resolvedManifestPath -Force -ErrorAction Stop
        return
    }

    if ($UseLocalModules -and $ModuleName -eq 'PSGraphView') {
        $repoRoot = Split-Path -Parent $PSScriptRoot
        $DefaultManifestPath = Publish-PSGraphViewLocalModule -RepoRoot $repoRoot

        Write-Verbose "Loading $ModuleName from repo manifest '$DefaultManifestPath'."
        Import-Module $DefaultManifestPath -Force -ErrorAction Stop
        return
    }

    if ($UseLocalModules -and (Test-Path $DefaultManifestPath)) {

        Write-Verbose "Loading $ModuleName from repo manifest '$DefaultManifestPath'."
        Import-Module $DefaultManifestPath -Force -ErrorAction Stop
        return
    }

    try {
        Write-Verbose "Loading installed module '$ModuleName' from PSModulePath."
        Import-Module $ModuleName -Force -ErrorAction Stop
    }
    catch {
        $resolutionHintText = if ($ResolutionHint) {
            $ResolutionHint
        }
        else {
            "Install-Module $ModuleName -Scope CurrentUser"
        }

        if ($UseLocalModules) {
            throw "Module '$ModuleName' is not installed and no usable local manifest was found. $resolutionHintText"
        }

        throw "Module '$ModuleName' is not installed. $resolutionHintText"
    }
}

function Resolve-ManifestPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ManifestPath,
        [Parameter(Mandatory)]
        [string]$ModuleName
    )

    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ManifestPath)
    if (-not (Test-Path $resolvedPath)) {
        throw "Manifest for module '$ModuleName' was not found at '$resolvedPath'."
    }

    return $resolvedPath
}
