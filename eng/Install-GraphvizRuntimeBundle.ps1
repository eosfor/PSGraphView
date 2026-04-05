param(
    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory,

    [string]$Token,

    [string[]]$RuntimeIdentifiers = @('linux-x64', 'osx-arm64', 'win-x64')
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:PSGRAPHVIEW_GITHUB_TOKEN
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:GITHUB_TOKEN
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    throw 'A GitHub token is required to download Graphviz runtime assets from the private repository.'
}

$tarCommand = Get-Command tar -ErrorAction SilentlyContinue
if ($null -eq $tarCommand) {
    throw "The 'tar' command is required to unpack Graphviz runtime assets."
}

$tag = "psgv-runtime-v$Version"
$apiBase = "https://api.github.com/repos/$Repository"
$headers = @{
    Authorization         = "Bearer $Token"
    Accept                = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'          = 'PSGraphView-Publish'
}

$release = Invoke-RestMethod -Method Get -Uri "$apiBase/releases/tags/$tag" -Headers $headers
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("psgraphview-graphviz-runtime-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null

try {
    foreach ($rid in $RuntimeIdentifiers) {
        $assetName = "graphviz-psgv-runtime-$Version-$rid.tar.gz"
        $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
        if ($null -eq $asset) {
            throw "Release '$tag' does not contain expected asset '$assetName'."
        }

        $assetArchivePath = Join-Path $temporaryRoot $assetName
        $assetExtractRoot = Join-Path $temporaryRoot $rid
        $nativeDestination = Join-Path $DestinationDirectory "runtimes/$rid/native"
        $bundleRoot = Join-Path $assetExtractRoot ("graphviz-psgv-runtime-$Version-$rid")

        New-Item -ItemType Directory -Path $assetExtractRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $nativeDestination -Force | Out-Null

        $assetHeaders = @{
            Authorization         = "Bearer $Token"
            Accept                = 'application/octet-stream'
            'X-GitHub-Api-Version' = '2022-11-28'
            'User-Agent'          = 'PSGraphView-Publish'
        }

        Invoke-WebRequest -Method Get -Uri $asset.url -Headers $assetHeaders -OutFile $assetArchivePath

        & $tarCommand.Source -xzf $assetArchivePath -C $assetExtractRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to extract asset '$assetName'."
        }

        if (-not (Test-Path -LiteralPath $bundleRoot)) {
            throw "Extracted bundle root '$bundleRoot' was not found."
        }

        $bundleContents = Get-ChildItem -LiteralPath $bundleRoot -Force
        if ($bundleContents.Count -eq 0) {
            throw "Extracted bundle root '$bundleRoot' is empty."
        }

        foreach ($bundleEntry in $bundleContents) {
            Copy-Item -LiteralPath $bundleEntry.FullName -Destination $nativeDestination -Recurse -Force
        }
    }
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
