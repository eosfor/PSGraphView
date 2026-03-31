# Compare-Export-FixedTextScene.ps1
# Builds a same-geometry text-only baseline by taking node label geometry from
# graphviz SVG and rendering the exact same scene through GVExport raster path.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-export-fixed-text-compare'),
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizSfdpPath,
    [string]$GraphvizNeatoPath,
    [int]$SfdpSeed = 42,
    [ValidateRange(1, 100000)]
    [int]$SfdpOverlapRemovalIterations = 1000,
    [ValidateRange(4.0, 64.0)]
    [double]$LabelFontSize = 14.0,
    [bool]$ManagedShowBackgroundRect = $true,
    [string]$ManagedBackgroundColor = '#ffffff',
    [switch]$AllowPartial = $true
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Compare-Export.Common.ps1')
. (Join-Path $PSScriptRoot 'Import-DemoModules.ps1')

if (-not $PSBoundParameters.ContainsKey('GraphvizSfdpPath') -or [string]::IsNullOrWhiteSpace($GraphvizSfdpPath)) {
    $GraphvizSfdpPath = Get-DefaultGraphvizSfdpPath
}

if (-not $PSBoundParameters.ContainsKey('GraphvizNeatoPath') -or [string]::IsNullOrWhiteSpace($GraphvizNeatoPath)) {
    $graphvizDir = Split-Path -Parent $GraphvizSfdpPath
    $candidate = Join-Path $graphvizDir 'neato'
    $GraphvizNeatoPath = if (Test-Path $candidate) { $candidate } else { 'neato' }
}

function New-GraphFromDefinition {
    param(
        [string[]]$Vertices = @(),
        [object[]]$Edges = @()
    )

    $graph = New-Graph

    foreach ($vertex in $Vertices) {
        Add-Vertex -Graph $graph -Vertex $vertex -ErrorAction SilentlyContinue | Out-Null
    }

    foreach ($edge in $Edges) {
        Add-Edge -From $edge.From -To $edge.To -Graph $graph | Out-Null
    }

    return $graph
}

function Load-GVExportAssembly {
    $assemblyPath = '/Users/andrei/repo/PSGraphView/src/PSGraphView.GVExport/bin/Debug/net9.0/PSGraphView.GVExport.dll'
    [System.Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null
}

function Convert-WidthStringToPoints {
    param([Parameter(Mandatory)][string]$Value)

    $match = [regex]::Match($Value, '-?\d+(?:\.\d+)?', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Unable to parse point value from '$Value'."
    }

    return [double]::Parse($match.Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-SvgNodeFixedPositionEntries {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$document = Get-Content -Path $Path -Raw
    $nodeGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' node ')]")
    $entries = New-Object System.Collections.ArrayList

    foreach ($nodeGroup in $nodeGroups) {
        $titleNode = $nodeGroup.SelectSingleNode("./*[local-name()='title']")
        $ellipseNode = $nodeGroup.SelectSingleNode("./*[local-name()='ellipse']")
        $textNode = $nodeGroup.SelectSingleNode("./*[local-name()='text']")
        if ($null -eq $titleNode -or $null -eq $ellipseNode -or $null -eq $textNode) {
            continue
        }

        $entry = New-Object System.Collections.Hashtable
        $entry['Id'] = [string]$titleNode.InnerText
        $entry['PosX'] = Get-SvgAttributeDouble -Node $ellipseNode -AttributeName 'cx'
        $entry['PosY'] = Get-SvgAttributeDouble -Node $ellipseNode -AttributeName 'cy'
        $entry['Label'] = [string]$textNode.InnerText
        $entry['FontSize'] = Get-SvgFontSizeValue -Value ([string]$textNode.Attributes['font-size']?.Value)
        $entry['FontFamily'] = [string]$textNode.Attributes['font-family']?.Value
        $entry['TextAnchor'] = [string]$textNode.Attributes['text-anchor']?.Value
        [void]$entries.Add($entry)
    }

    return $entries.ToArray()
}

function Get-FixedTextSvgSummary {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$document = Get-Content -Path $Path -Raw
    $svg = $document.DocumentElement
    if ($null -eq $svg) {
        throw "SVG '$Path' does not have a document element."
    }

    $graphGroup = $document.SelectSingleNode("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' graph ')]")
    return [ordered]@{
        Width = [string]$svg.GetAttribute('width')
        Height = [string]$svg.GetAttribute('height')
        ViewBox = [string]$svg.GetAttribute('viewBox')
        GraphGroupId = if ($null -ne $graphGroup -and $null -ne $graphGroup.Attributes['id']) { [string]$graphGroup.Attributes['id'].Value } else { 'graph0' }
        GraphGroupTransform = if ($null -ne $graphGroup -and $null -ne $graphGroup.Attributes['transform']) { [string]$graphGroup.Attributes['transform'].Value } else { 'scale(1 1) rotate(0) translate(0 0)' }
    }
}

function Get-FixedTextSvgLabelEntries {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$document = Get-Content -Path $Path -Raw
    $nodeGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' node ')]")
    $entries = @{}
    foreach ($nodeGroup in $nodeGroups) {
        $titleNode = $nodeGroup.SelectSingleNode("./*[local-name()='title']")
        $textNode = $nodeGroup.SelectSingleNode("./*[local-name()='text']")
        if ($null -eq $titleNode -or $null -eq $textNode) {
            continue
        }

        $entry = New-Object System.Collections.Hashtable
        $entry['NodeTitle'] = [string]$titleNode.InnerText
        $entry['Text'] = [string]$textNode.InnerText
        $entry['X'] = Get-SvgAttributeDouble -Node $textNode -AttributeName 'x'
        $entry['Y'] = Get-SvgAttributeDouble -Node $textNode -AttributeName 'y'
        $entry['FontSize'] = Get-SvgFontSizeValue -Value ([string]$textNode.Attributes['font-size']?.Value)
        $entry['FontFamily'] = [string]$textNode.Attributes['font-family']?.Value
        $entry['TextAnchor'] = [string]$textNode.Attributes['text-anchor']?.Value
        $entries[[string]$entry['NodeTitle']] = $entry
    }

    return $entries
}

function Escape-DotString {
    param([Parameter(Mandatory)][string]$Value)

    return $Value.Replace('\', '\\').Replace('"', '\"')
}

function Get-GraphvizFontNameForEntry {
    param([Parameter(Mandatory)]$Entry)

    if ([string]$Entry.FontFamily -like 'Times*') {
        return 'Times-Roman'
    }

    return 'Helvetica'
}

function Write-FixedTextOnlyDot {
    param(
        [Parameter(Mandatory)][object[]]$Entries,
        [Parameter(Mandatory)][string]$Path
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('graph G {') | Out-Null
    $lines.Add('  graph [outputorder=edgesfirst, dpi=72];') | Out-Null
    $lines.Add('  node [shape=plain, width=0, height=0, margin=0, fixedsize=true];') | Out-Null
    foreach ($entry in $Entries) {
        $fontName = Get-GraphvizFontNameForEntry -Entry $entry
        $escapedLabel = Escape-DotString -Value ([string]$entry.Label)
        $lines.Add(('  "{0}" [pos="{1},{2}!", label="{3}", fontname="{4}", fontsize={5}];' -f `
            [string]$entry.Id,
            ([double]$entry.PosX).ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture),
            ([double]$entry.PosY).ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture),
            $escapedLabel,
            $fontName,
            ([double]$entry.FontSize).ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture))) | Out-Null
    }
    $lines.Add('}') | Out-Null

    Set-Content -Path $Path -Value $lines
}

function Invoke-ManagedFixedTextRasterExport {
    param(
        [Parameter(Mandatory)][string]$SourceSvgPath,
        [Parameter(Mandatory)][string]$FormatName,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][bool]$ShowBackgroundRect,
        [Parameter(Mandatory)][string]$BackgroundColor,
        [double]$RasterWidthPoints = [double]::NaN,
        [double]$RasterHeightPoints = [double]::NaN
    )

    Load-GVExportAssembly

    $summary = Get-FixedTextSvgSummary -Path $SourceSvgPath
    $entryMap = Get-FixedTextSvgLabelEntries -Path $SourceSvgPath
    $viewBox = Get-SvgViewBoxRect -ViewBox ([string]$summary.ViewBox)
    $outputWidthPoints = Convert-WidthStringToPoints -Value ([string]$summary.Width)
    $outputHeightPoints = Convert-WidthStringToPoints -Value ([string]$summary.Height)
    $effectiveRasterWidthPoints = if ([double]::IsNaN($RasterWidthPoints)) { $outputWidthPoints } else { $RasterWidthPoints }
    $effectiveRasterHeightPoints = if ([double]::IsNaN($RasterHeightPoints)) { $outputHeightPoints } else { $RasterHeightPoints }
    $viewport = [PSGraphView.GVExport.GraphRenderViewport]::new(
        [double]$viewBox.MinX,
        [double]$viewBox.MinY,
        [double]$viewBox.Width,
        [double]$viewBox.Height,
        $outputWidthPoints,
        $outputHeightPoints,
        $effectiveRasterWidthPoints,
        $effectiveRasterHeightPoints)
    $style = [PSGraphView.GVExport.GraphRenderStyle]::new($ShowBackgroundRect, $BackgroundColor, $null)
    $canvas = [PSGraphView.GVExport.GraphRenderCanvas]::new(
        [string]$summary.GraphGroupId,
        'graph',
        'G',
        [string]$summary.GraphGroupTransform,
        '0,0 0,0 0,0 0,0')

    $nodeList = New-Object 'System.Collections.Generic.List[PSGraphView.GVExport.GraphRenderNode]'
    foreach ($entry in $entryMap.GetEnumerator()) {
        $labelEntry = $entry.Value
        $label = [PSGraphView.GVExport.GraphRenderLabel]::new(
            [string]$labelEntry.Text,
            [double]$labelEntry.X,
            [double]$labelEntry.Y,
            [double]$labelEntry.FontSize,
            [string]$labelEntry.FontFamily,
            [string]$labelEntry.TextAnchor,
            '#000000')
        $node = [PSGraphView.GVExport.GraphRenderNode]::new(
            [string]$entry.Key,
            [string]$entry.Key,
            [string]$entry.Key,
            [double]$labelEntry.X,
            [double]$labelEntry.Y,
            0.0,
            0.0,
            '#00000000',
            '#00000000',
            0.0,
            $label)
        $nodeList.Add($node) | Out-Null
    }

    $edgeList = New-Object 'System.Collections.Generic.List[PSGraphView.GVExport.GraphRenderEdge]'
    $scene = [PSGraphView.GVExport.GraphRenderScene]::new($viewport, $style, $canvas, $edgeList, $nodeList)
    $result = switch ($FormatName) {
        'Png' { [PSGraphView.GVExport.GraphRasterRenderSceneWriter]::RenderPng($scene) }
        'Jpg' { [PSGraphView.GVExport.GraphRasterRenderSceneWriter]::RenderJpg($scene) }
        default { throw "Unsupported fixed text raster format '$FormatName'." }
    }

    [System.IO.File]::WriteAllBytes($OutputPath, $result.Bytes)
    return [ordered]@{
        Supported = $true
        Format = $FormatName
        Path = $OutputPath
        Summary = Get-FormatSummary -Format $FormatName -Path $OutputPath
        DiagnosticsSummary = [ordered]@{
            Raster = @([ordered]@{
                format = $result.Format
                backend = $result.Backend
                pixelWidth = $result.PixelWidth
                pixelHeight = $result.PixelHeight
                scaleX = $result.ScaleX
                scaleY = $result.ScaleY
                flattenedForOpaqueOutput = $result.FlattenedForOpaqueOutput
                byteCount = $result.Bytes.Length
                encodeQuality = $result.EncodeQuality
                opaqueOutputPolicy = $result.OpaqueOutputPolicy
                opaqueFallbackColor = $result.OpaqueFallbackColor
                opaqueAlphaThreshold = $result.OpaqueAlphaThreshold
                textHintingLevel = $result.TextHintingLevel
                subpixelText = $result.SubpixelText
                lcdRenderText = $result.LcdRenderText
                autohintedText = $result.AutohintedText
                resolvedLabelFontFamilies = $result.ResolvedLabelFontFamilies
            })
        }
    }
}

Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

Ensure-Directory -Path $OutputDir

$cases = @(
    [ordered]@{
        Name = 'single-edge-fixed-text'
        Graph = New-GraphFromDefinition -Edges @([pscustomobject]@{ From = 'AlphaService'; To = 'BetaDatabase' })
    }
    [ordered]@{
        Name = 'star-fixed-text'
        Graph = New-GraphFromDefinition -Edges @(
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'North Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'South Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'East Region' }
            [pscustomobject]@{ From = 'Hub Gateway'; To = 'West Region' }
        )
    }
)

$caseSummaries = @()

foreach ($case in $cases) {
    $caseName = $case.Name
    $graph = $case.Graph
    $runDir = Join-Path $OutputDir $caseName
    $sourceDir = Join-Path $runDir 'source'
    $fixedDir = Join-Path $runDir 'fixed-text'
    $svgDir = Join-Path $fixedDir 'svg'
    $pngDir = Join-Path $fixedDir 'png'
    $jpgDir = Join-Path $fixedDir 'jpg'
    $logDir = Join-Path $fixedDir 'logs'
    Ensure-Directory -Path $runDir
    Ensure-Directory -Path $sourceDir
    Ensure-Directory -Path $svgDir
    Ensure-Directory -Path $pngDir
    Ensure-Directory -Path $jpgDir
    Ensure-Directory -Path $logDir

    $dotPath = Join-Path $sourceDir "$caseName.gv"
    Export-Graph -Graph $graph -Format Graphviz -Path $dotPath

    $graphvizArgs = @(
        '-Nshape=circle',
        '-Nfixedsize=true',
        '-Nwidth=0.02',
        '-Nheight=0.02',
        '-Earrowsize=0.08',
        '-Epenwidth=0.2',
        '-Ecolor=#00000018',
        "-Gstart=$SfdpSeed",
        "-Goverlap=prism$SfdpOverlapRemovalIterations",
        '-Gsep=+4',
        '-Goutputorder=edgesfirst'
    )

    Write-Host "Running source graphviz layout for '$caseName'..."
    $sourceSvg = Invoke-GraphvizExport `
        -GraphvizSfdpPath $GraphvizSfdpPath `
        -GraphvizArgs $graphvizArgs `
        -DotPath $dotPath `
        -FormatName 'Svg' `
        -TargetSpec 'svg:svg' `
        -OutputPath (Join-Path $sourceDir "$caseName-source-graphviz.svg") `
        -LogPath (Join-Path $sourceDir "$caseName-source-graphviz.verbose.log") `
        -AllowPartial:$AllowPartial

    $fixedEntries = Get-SvgNodeFixedPositionEntries -Path $sourceSvg.Path
    $fixedDotPath = Join-Path $fixedDir "$caseName-fixed-text.gv"
    Write-FixedTextOnlyDot -Entries $fixedEntries -Path $fixedDotPath

    Write-Host "Running graphviz fixed-text render for '$caseName'..."
    $graphvizFixedSvg = Invoke-GraphvizExport `
        -GraphvizSfdpPath $GraphvizNeatoPath `
        -GraphvizArgs @('-n') `
        -DotPath $fixedDotPath `
        -FormatName 'Svg' `
        -TargetSpec 'svg:svg' `
        -OutputPath (Join-Path $svgDir "$caseName-graphviz.svg") `
        -LogPath (Join-Path $logDir "$caseName-graphviz-svg.verbose.log") `
        -AllowPartial:$AllowPartial
    $graphvizFixedPng = Invoke-GraphvizExport `
        -GraphvizSfdpPath $GraphvizNeatoPath `
        -GraphvizArgs @('-n') `
        -DotPath $fixedDotPath `
        -FormatName 'Png' `
        -TargetSpec 'png:cairo' `
        -OutputPath (Join-Path $pngDir "$caseName-graphviz.png") `
        -LogPath (Join-Path $logDir "$caseName-graphviz-png.verbose.log") `
        -AllowPartial:$AllowPartial
    $graphvizFixedJpg = Invoke-GraphvizExport `
        -GraphvizSfdpPath $GraphvizNeatoPath `
        -GraphvizArgs @('-n') `
        -DotPath $fixedDotPath `
        -FormatName 'Jpg' `
        -TargetSpec 'jpg:cairo' `
        -OutputPath (Join-Path $jpgDir "$caseName-graphviz.jpg") `
        -LogPath (Join-Path $logDir "$caseName-graphviz-jpg.verbose.log") `
        -AllowPartial:$AllowPartial

    Write-Host "Running managed fixed-text render for '$caseName'..."
    $managedFixedPng = Invoke-ManagedFixedTextRasterExport `
        -SourceSvgPath $graphvizFixedSvg.Path `
        -FormatName 'Png' `
        -OutputPath (Join-Path $pngDir "$caseName-managed.png") `
        -ShowBackgroundRect $ManagedShowBackgroundRect `
        -BackgroundColor $ManagedBackgroundColor `
        -RasterWidthPoints (([double]$graphvizFixedPng.Summary.Width * 72.0) / 96.0) `
        -RasterHeightPoints (([double]$graphvizFixedPng.Summary.Height * 72.0) / 96.0)
    $managedFixedJpg = Invoke-ManagedFixedTextRasterExport `
        -SourceSvgPath $graphvizFixedSvg.Path `
        -FormatName 'Jpg' `
        -OutputPath (Join-Path $jpgDir "$caseName-managed.jpg") `
        -ShowBackgroundRect $ManagedShowBackgroundRect `
        -BackgroundColor $ManagedBackgroundColor `
        -RasterWidthPoints (([double]$graphvizFixedJpg.Summary.Width * 72.0) / 96.0) `
        -RasterHeightPoints (([double]$graphvizFixedJpg.Summary.Height * 72.0) / 96.0)

    $summary = [ordered]@{
        Name = $caseName
        SourceGraphvizSvgPath = $sourceSvg.Path
        FixedTextDotPath = $fixedDotPath
        GraphvizSvgPath = $graphvizFixedSvg.Path
        ManagedPngPath = $managedFixedPng.Path
        ManagedJpgPath = $managedFixedJpg.Path
        FontResolution = Get-FontResolutionComparisonSummary -GraphvizResult $graphvizFixedPng -ManagedResult $managedFixedPng
        Png = Get-RasterComparisonSummary -FormatName 'PNG' -GraphvizResult $graphvizFixedPng -ManagedResult $managedFixedPng
        Jpg = Get-RasterComparisonSummary -FormatName 'JPG' -GraphvizResult $graphvizFixedJpg -ManagedResult $managedFixedJpg
    }

    $summaryPath = Join-Path $runDir "$caseName-fixed-text-comparison.json"
    $summary | ConvertTo-Json -Depth 12 | Set-Content -Path $summaryPath
    $caseSummaries += $summary
}

$overview = [ordered]@{
    GeneratedAt = (Get-Date).ToString('o')
    GraphvizSfdpPath = $GraphvizSfdpPath
    GraphvizNeatoPath = $GraphvizNeatoPath
    Seed = $SfdpSeed
    OverlapRemovalIterations = $SfdpOverlapRemovalIterations
    LabelFontSize = $LabelFontSize
    ManagedShowBackgroundRect = $ManagedShowBackgroundRect
    ManagedBackgroundColor = $ManagedBackgroundColor
    CaseCount = $caseSummaries.Count
    Cases = $caseSummaries
}

$overviewPath = Join-Path $OutputDir 'fixed-text-overview.json'
$overview | ConvertTo-Json -Depth 12 | Set-Content -Path $overviewPath

Write-Host ''
Write-Host '=== Fixed Text Export Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Cases             : $($overview.CaseCount)"
Write-Host "Graphviz sfdp     : $GraphvizSfdpPath"
Write-Host "Graphviz neato    : $GraphvizNeatoPath"
Write-Host "Overview          : $overviewPath"
