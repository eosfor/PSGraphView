# Visualize-AllDatasets.ps1
# Run all three dataset visualization demos in sequence.
# Outputs go to a common temp directory. Open the HTML files in a browser.
#
# Usage:
#   pwsh ./demos/Visualize-AllDatasets.ps1
#   pwsh ./demos/Visualize-AllDatasets.ps1 -OutputDir ~/my-graphs

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-demos')
    ,
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath
)

$ErrorActionPreference = 'Stop'
$demoDir = $PSScriptRoot

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  PSGraphView Public Dataset Demos" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

& "$demoDir/Visualize-LesMiserables.ps1" `
    -OutputDir $OutputDir `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')
& "$demoDir/Visualize-KarateClub.ps1" `
    -OutputDir $OutputDir `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')
& "$demoDir/Visualize-WikiVote.ps1" `
    -OutputDir $OutputDir `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "All output files:" -ForegroundColor Cyan
Get-ChildItem $OutputDir -File | Sort-Object Name | ForEach-Object {
    $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) }
            elseif ($_.Length -gt 1KB) { "{0:N0} KB" -f ($_.Length / 1KB) }
            else { "$($_.Length) B" }
    Write-Host ("  {0,-45} {1,10}" -f $_.Name, $size)
}
Write-Host "`nDirectory: $OutputDir" -ForegroundColor Cyan
Write-Host "Open .html files in a browser for interactive Vega visualizations."
Write-Host "Open .svg files in a browser or image viewer.`n"
