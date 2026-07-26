function Import-PSGraphViewDemoModules {
    [CmdletBinding()]
    param(
        [switch]$UseLocalModules,
        [string]$PSQuickGraphManifestPath,
        [string]$PSGraphViewManifestPath
    )

    $repoRoot = Split-Path -Parent $PSScriptRoot
    $defaultPsQuickGraphManifest = Join-Path (Join-Path (Split-Path -Parent $repoRoot) 'PSGraph') 'PSGraph.Tests/bin/Debug/net8.0/PSQuickGraph.psd1'
    $defaultPsGraphViewManifest = Join-Path $repoRoot 'tests/PSGraphView.PowerShell.Tests/bin/Debug/net8.0/PSGraphView.psd1'

    Remove-Module PSQuickGraph, PSGraphView -ErrorAction SilentlyContinue

    Import-DemoModule `
        -ModuleName 'PSQuickGraph' `
        -PreferredManifestPath $PSQuickGraphManifestPath `
        -DefaultManifestPath $defaultPsQuickGraphManifest `
        -UseLocalModules:$UseLocalModules

    Import-DemoModule `
        -ModuleName 'PSGraphView' `
        -PreferredManifestPath $PSGraphViewManifestPath `
        -DefaultManifestPath $defaultPsGraphViewManifest `
        -UseLocalModules:$UseLocalModules
}

function Import-DemoModule {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ModuleName,
        [string]$PreferredManifestPath,
        [string]$DefaultManifestPath,
        [switch]$UseLocalModules
    )

    if ($PreferredManifestPath) {
        $resolvedManifestPath = Resolve-ManifestPath -ManifestPath $PreferredManifestPath -ModuleName $ModuleName
        Write-Verbose "Loading $ModuleName from explicit manifest '$resolvedManifestPath'."
        Import-Module $resolvedManifestPath -Force -ErrorAction Stop
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
        $installHint = "Install-Module $ModuleName -Scope CurrentUser"
        if ($UseLocalModules) {
            throw "Module '$ModuleName' is not installed and no usable local manifest was found. Install it from PSGallery with '$installHint' or pass -${ModuleName}ManifestPath to a local build output."
        }

        throw "Module '$ModuleName' is not installed. Install it from PSGallery with '$installHint', or rerun the demo with -UseLocalModules or an explicit local manifest path."
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
