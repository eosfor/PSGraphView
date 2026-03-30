function Get-DefaultGraphvizSfdpPath {
    $localGraphviz = '/tmp/graphviz-prefix/bin/sfdp'
    if (Test-Path $localGraphviz) {
        return $localGraphviz
    }

    return 'sfdp'
}

function Ensure-Directory {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Get-DotNodeIdMap {
    param([Parameter(Mandatory)][string]$Path)

    $nodeIdMap = @{}
    foreach ($line in Get-Content -Path $Path) {
        if ($line -match '^\s*([^\s\[]+)\s+\[label="((?:[^"\\]|\\.)*)"\];\s*$') {
            $nodeId = $matches[1]
            $label = $matches[2] -replace '\\"', '"'
            $nodeIdMap[$nodeId] = $label
        }
    }

    return $nodeIdMap
}

function Set-DotNodeLabelsEmpty {
    param([Parameter(Mandatory)][string]$Path)

    $content = Get-Content -Path $Path
    $updated = foreach ($line in $content) {
        if ($line -match '^\s*[^\s\[]+\s+\[') {
            $line -replace 'label="((?:[^"\\]|\\.)*)"', 'label=""'
        }
        else {
            $line
        }
    }

    Set-Content -Path $Path -Value $updated
}

function New-GraphWithRemappedIds {
    param(
        [Parameter(Mandatory)]$Graph,
        [Parameter(Mandatory)][hashtable]$OriginalIdToRemappedId
    )

    $remapped = New-Graph

    foreach ($vertex in $Graph.Vertices) {
        Add-Vertex -Graph $remapped -Vertex $OriginalIdToRemappedId[$vertex.Label] | Out-Null
    }

    foreach ($edge in $Graph.Edges) {
        Add-Edge `
            -From $OriginalIdToRemappedId[$edge.Source.Label] `
            -To $OriginalIdToRemappedId[$edge.Target.Label] `
            -Graph $remapped | Out-Null
    }

    return $remapped
}

function Get-SvgStructureSummary {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    [xml]$document = Get-Content -Path $Path -Raw
    $svg = $document.DocumentElement
    if ($null -eq $svg) {
        return $null
    }

    $nodeGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' node ')]")
    $edgeGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' edge ')]")
    $clusterGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' cluster ')]")
    $graphGroup = $document.SelectSingleNode("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' graph ')]")
    $titles = $document.SelectNodes("//*[local-name()='title']")
    $groups = $document.SelectNodes("//*[local-name()='g']")
    $paths = $document.SelectNodes("//*[local-name()='path']")
    $circles = $document.SelectNodes("//*[local-name()='circle']")
    $ellipses = $document.SelectNodes("//*[local-name()='ellipse']")
    $texts = $document.SelectNodes("//*[local-name()='text']")
    $anchors = $document.SelectNodes("//*[local-name()='a']")
    $rectangles = $document.SelectNodes("//*[local-name()='rect']")

    return [ordered]@{
        Width = [string]$svg.GetAttribute('width')
        Height = [string]$svg.GetAttribute('height')
        ViewBox = [string]$svg.GetAttribute('viewBox')
        GraphGroupId = if ($null -ne $graphGroup) { [string]$graphGroup.Attributes['id'].Value } else { $null }
        GraphGroupTransform = if ($null -ne $graphGroup) { [string]$graphGroup.Attributes['transform'].Value } else { $null }
        GroupCount = $groups.Count
        NodeGroupCount = $nodeGroups.Count
        EdgeGroupCount = $edgeGroups.Count
        ClusterGroupCount = $clusterGroups.Count
        TitleCount = $titles.Count
        PathCount = $paths.Count
        CircleCount = $circles.Count
        EllipseCount = $ellipses.Count
        TextCount = $texts.Count
        AnchorCount = $anchors.Count
        RectCount = $rectangles.Count
    }
}

function Get-PngMetadata {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24) {
        throw "PNG file '$Path' is too small."
    }

    $signature = [System.BitConverter]::ToString($bytes, 0, 8)
    if ($signature -ne '89-50-4E-47-0D-0A-1A-0A') {
        throw "File '$Path' is not a PNG."
    }

    $width = ($bytes[16] -shl 24) -bor ($bytes[17] -shl 16) -bor ($bytes[18] -shl 8) -bor $bytes[19]
    $height = ($bytes[20] -shl 24) -bor ($bytes[21] -shl 16) -bor ($bytes[22] -shl 8) -bor $bytes[23]

    return [ordered]@{
        Width = $width
        Height = $height
        ByteCount = $bytes.Length
    }
}

function Get-JpegMetadata {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 4 -or $bytes[0] -ne 0xFF -or $bytes[1] -ne 0xD8) {
        throw "File '$Path' is not a JPEG."
    }

    $index = 2
    while ($index + 8 -lt $bytes.Length) {
        while ($index -lt $bytes.Length -and $bytes[$index] -ne 0xFF) {
            $index++
        }

        if ($index + 1 -ge $bytes.Length) {
            break
        }

        while ($index + 1 -lt $bytes.Length -and $bytes[$index + 1] -eq 0xFF) {
            $index++
        }

        $marker = $bytes[$index + 1]
        $index += 2

        if ($marker -in 0xD8, 0xD9) {
            continue
        }

        if ($index + 1 -ge $bytes.Length) {
            break
        }

        $segmentLength = ($bytes[$index] -shl 8) -bor $bytes[$index + 1]
        if ($segmentLength -lt 2 -or $index + $segmentLength -gt $bytes.Length) {
            break
        }

        if ($marker -in 0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF) {
            $height = ($bytes[$index + 3] -shl 8) -bor $bytes[$index + 4]
            $width = ($bytes[$index + 5] -shl 8) -bor $bytes[$index + 6]
            return [ordered]@{
                Width = $width
                Height = $height
                ByteCount = $bytes.Length
            }
        }

        $index += $segmentLength
    }

    throw "Unable to read JPEG dimensions from '$Path'."
}

function Get-FormatSummary {
    param(
        [Parameter(Mandatory)][string]$Format,
        [Parameter(Mandatory)][string]$Path
    )

    if (-not (Test-Path $Path)) {
        return $null
    }

    switch ($Format) {
        'Svg' {
            $summary = Get-SvgStructureSummary -Path $Path
            if ($null -eq $summary) {
                return $null
            }

            $summary['ByteCount'] = (Get-Item -Path $Path).Length
            return $summary
        }
        'Png' {
            return Get-PngMetadata -Path $Path
        }
        'Jpg' {
            return Get-JpegMetadata -Path $Path
        }
        default {
            throw "Unsupported format '$Format'."
        }
    }
}

function Convert-StructuredLogValue {
    param([Parameter(Mandatory)][string]$Value)

    if ($Value.Length -ge 2 -and $Value[0] -eq '"' -and $Value[-1] -eq '"') {
        return $Value.Substring(1, $Value.Length - 2)
    }

    if ($Value -eq 'true') {
        return $true
    }

    if ($Value -eq 'false') {
        return $false
    }

    $integerValue = 0L
    if ([long]::TryParse($Value, [System.Globalization.NumberStyles]::Integer, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$integerValue)) {
        return $integerValue
    }

    $doubleValue = 0.0
    if ([double]::TryParse($Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$doubleValue)) {
        return $doubleValue
    }

    return $Value
}

function Convert-StructuredLogEntry {
    param([Parameter(Mandatory)][string]$Line)

    $tokens = $Line -split '\s+'
    $entry = [ordered]@{}

    for ($index = 1; $index -lt $tokens.Length; $index++) {
        $token = $tokens[$index]
        $separatorIndex = $token.IndexOf('=')
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $token.Substring(0, $separatorIndex)
        $value = $token.Substring($separatorIndex + 1)
        $entry[$key] = Convert-StructuredLogValue -Value $value
    }

    return $entry
}

function Get-GraphvizVerboseSummary {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    $summary = [ordered]@{
        Viewport = $null
        Scene = $null
        Svg = $null
        Cairo = $null
        Gd = $null
    }

    foreach ($line in Get-Content -Path $Path) {
        if ($line.StartsWith('GVEXPORT_VIEW ')) {
            $summary.Viewport = Convert-StructuredLogEntry -Line $line
            continue
        }

        if ($line.StartsWith('GVEXPORT_SCENE ')) {
            $summary.Scene = Convert-StructuredLogEntry -Line $line
            continue
        }

        if ($line.StartsWith('GVEXPORT_SVG ')) {
            $summary.Svg = Convert-StructuredLogEntry -Line $line
            continue
        }

        if ($line.StartsWith('GVEXPORT_CAIRO ')) {
            $summary.Cairo = Convert-StructuredLogEntry -Line $line
            continue
        }

        if ($line.StartsWith('GVEXPORT_GD ')) {
            $summary.Gd = Convert-StructuredLogEntry -Line $line
        }
    }

    return $summary
}

function Get-ManagedDiagnosticsSummary {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    $summary = [ordered]@{
        Scene = $null
        Viewport = $null
        SvgStructure = $null
        SvgGeometry = @()
    }

    foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $event = ConvertFrom-Json -InputObject $line -AsHashtable
        $phase = [string]$event['Phase']
        $name = [string]$event['Name']
        $data = $event['Data']

        if ($phase -eq 'render' -and $name -eq 'scene') {
            $summary.Scene = $data
            continue
        }

        if ($phase -eq 'render' -and $name -eq 'viewport') {
            $summary.Viewport = $data
            continue
        }

        if ($phase -eq 'svg' -and $name -eq 'structure') {
            $summary.SvgStructure = $data
            continue
        }

        if ($phase -eq 'svg' -and $name -eq 'geometry') {
            $summary.SvgGeometry += $data
        }
    }

    return $summary
}

function Get-DiagnosticsComparisonSummary {
    param(
        [object]$GraphvizResult,
        [object]$ManagedResult
    )

    if (-not $GraphvizResult.Supported -or -not $ManagedResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = 'One side did not produce output for diagnostics comparison.'
        }
    }

    $graphvizVerbose = $GraphvizResult.VerboseSummary
    $managedDiagnostics = $ManagedResult.DiagnosticsSummary
    if ($null -eq $graphvizVerbose -or $null -eq $managedDiagnostics) {
        return [ordered]@{
            Available = $false
            Reason = 'Verbose or managed diagnostics summary is missing.'
        }
    }

    $graphvizViewport = $graphvizVerbose.Viewport
    $managedViewport = $managedDiagnostics.Viewport
    $graphvizScene = $graphvizVerbose.Scene
    $managedScene = $managedDiagnostics.Scene
    $graphvizSvg = $graphvizVerbose.Svg
    $managedSvg = $managedDiagnostics.SvgStructure
    $graphvizStructure = $GraphvizResult.Summary

    return [ordered]@{
        Available = $true
        Viewport = [ordered]@{
            Available = ($null -ne $graphvizViewport -and $null -ne $managedViewport)
            OutputWidthDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['outputWidth'] - [double]$graphvizViewport['width'] } else { $null }
            OutputHeightDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['outputHeight'] - [double]$graphvizViewport['height'] } else { $null }
            ViewBoxMinXDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['viewBoxMinX'] - [double]$graphvizViewport['pagebb_ll_x'] } else { $null }
            ViewBoxMinYDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['viewBoxMinY'] - [double]$graphvizViewport['pagebb_ll_y'] } else { $null }
            ViewBoxWidthDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['viewBoxWidth'] - [double]$graphvizViewport['pagebb_ur_x'] } else { $null }
            ViewBoxHeightDelta = if ($null -ne $graphvizViewport -and $null -ne $managedViewport) { [double]$managedViewport['viewBoxHeight'] - [double]$graphvizViewport['pagebb_ur_y'] } else { $null }
            Graphviz = $graphvizViewport
            Managed = $managedViewport
        }
        Scene = [ordered]@{
            Available = ($null -ne $graphvizScene -and $null -ne $managedScene)
            NodeCountDelta = if ($null -ne $graphvizScene -and $null -ne $managedScene) { [long]$managedScene['nodeCount'] - [long]$graphvizScene['nodes'] } else { $null }
            EdgeCountDelta = if ($null -ne $graphvizScene -and $null -ne $managedScene) { [long]$managedScene['edgeCount'] - [long]$graphvizScene['edges'] } else { $null }
            Graphviz = $graphvizScene
            Managed = $managedScene
        }
        SvgStructure = [ordered]@{
            Available = ($null -ne $graphvizStructure -and $null -ne $managedSvg)
            WidthMatch = if ($null -ne $graphvizStructure -and $null -ne $managedSvg) { [string]$managedSvg['width'] -eq [string]$graphvizStructure.Width } else { $null }
            HeightMatch = if ($null -ne $graphvizStructure -and $null -ne $managedSvg) { [string]$managedSvg['height'] -eq [string]$graphvizStructure.Height } else { $null }
            ViewBoxMatch = if ($null -ne $graphvizStructure -and $null -ne $managedSvg) { [string]$managedSvg['viewBox'] -eq [string]$graphvizStructure.ViewBox } else { $null }
            TitleCountDelta = if ($null -ne $graphvizStructure -and $null -ne $managedSvg) { [long]$managedSvg['titleCount'] - [long]$graphvizStructure.TitleCount } else { $null }
            RectCountDelta = if ($null -ne $graphvizStructure -and $null -ne $managedSvg) { [long]$managedSvg['rectCount'] - [long]$graphvizStructure.RectCount } else { $null }
            Graphviz = [ordered]@{
                Structure = $graphvizStructure
                Verbose = $graphvizSvg
            }
            Managed = $managedSvg
        }
    }
}

function Invoke-GraphvizExport {
    param(
        [Parameter(Mandatory)][string]$GraphvizSfdpPath,
        [Parameter(Mandatory)][string[]]$GraphvizArgs,
        [Parameter(Mandatory)][string]$DotPath,
        [Parameter(Mandatory)][string]$FormatName,
        [Parameter(Mandatory)][string]$TargetSpec,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$LogPath,
        [switch]$AllowPartial
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $GraphvizSfdpPath '-v' @GraphvizArgs "-T$TargetSpec" '-o' $OutputPath $DotPath 2> $LogPath | Out-Null
        $watch.Stop()

        return [ordered]@{
            Supported = $true
            Format = $FormatName
            Target = $TargetSpec
            Path = $OutputPath
            LogPath = $LogPath
            Error = $null
            ElapsedMilliseconds = $watch.ElapsedMilliseconds
            Summary = Get-FormatSummary -Format $FormatName -Path $OutputPath
            VerboseSummary = Get-GraphvizVerboseSummary -Path $LogPath
        }
    }
    catch {
        $watch.Stop()
        if (-not $AllowPartial) {
            throw
        }

        return [ordered]@{
            Supported = $false
            Format = $FormatName
            Target = $TargetSpec
            Path = $OutputPath
            LogPath = $LogPath
            Error = $_.Exception.Message
            ElapsedMilliseconds = $watch.ElapsedMilliseconds
            Summary = $null
            VerboseSummary = Get-GraphvizVerboseSummary -Path $LogPath
        }
    }
}

function Invoke-ManagedExport {
    param(
        [Parameter(Mandatory)]$Graph,
        [Parameter(Mandatory)][string]$FormatName,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$DiagnosticsPath,
        [Parameter(Mandatory)][int]$SfdpSeed,
        [Parameter(Mandatory)][int]$SfdpOverlapRemovalIterations,
        [switch]$AllowPartial
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $parameters = @{
            Graph = $Graph
            Renderer = 'Sfdp'
            As = $FormatName
            Path = $OutputPath
            DisableGroupColors = $true
            NodeRadius = 0.72
            EdgeLineWidth = 0.2
            EdgeColor = '#00000018'
            SfdpSeed = $SfdpSeed
            SfdpOverlapRemovalIterations = $SfdpOverlapRemovalIterations
            SfdpDiagnosticsPath = $DiagnosticsPath
            SfdpOverlapRemovalPadding = 4
            SfdpOverlapRemovalBoxUnits = 'GraphvizPoints'
            ErrorAction = 'Stop'
        }

        Export-GraphView @parameters | Out-Null
        $watch.Stop()

        return [ordered]@{
            Supported = $true
            Format = $FormatName
            Path = $OutputPath
            DiagnosticsPath = $DiagnosticsPath
            Error = $null
            ElapsedMilliseconds = $watch.ElapsedMilliseconds
            Summary = Get-FormatSummary -Format $FormatName -Path $OutputPath
            DiagnosticsSummary = Get-ManagedDiagnosticsSummary -Path $DiagnosticsPath
        }
    }
    catch {
        $watch.Stop()
        if (-not $AllowPartial) {
            throw
        }

        return [ordered]@{
            Supported = $false
            Format = $FormatName
            Path = $OutputPath
            DiagnosticsPath = $DiagnosticsPath
            Error = $_.Exception.Message
            ElapsedMilliseconds = $watch.ElapsedMilliseconds
            Summary = $null
            DiagnosticsSummary = Get-ManagedDiagnosticsSummary -Path $DiagnosticsPath
        }
    }
}

function Get-SvgComparisonSummary {
    param(
        [object]$GraphvizResult,
        [object]$ManagedResult
    )

    if (-not $GraphvizResult.Supported -or -not $ManagedResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = 'One side did not produce SVG output.'
        }
    }

    $graphvizSummary = $GraphvizResult.Summary
    $managedSummary = $ManagedResult.Summary

    return [ordered]@{
        Available = $true
        WidthMatch = $graphvizSummary.Width -eq $managedSummary.Width
        HeightMatch = $graphvizSummary.Height -eq $managedSummary.Height
        ViewBoxMatch = $graphvizSummary.ViewBox -eq $managedSummary.ViewBox
        NodeGroupDelta = $managedSummary.NodeGroupCount - $graphvizSummary.NodeGroupCount
        EdgeGroupDelta = $managedSummary.EdgeGroupCount - $graphvizSummary.EdgeGroupCount
        TitleDelta = $managedSummary.TitleCount - $graphvizSummary.TitleCount
        PathDelta = $managedSummary.PathCount - $graphvizSummary.PathCount
        TextDelta = $managedSummary.TextCount - $graphvizSummary.TextCount
        AnchorDelta = $managedSummary.AnchorCount - $graphvizSummary.AnchorCount
        RectDelta = $managedSummary.RectCount - $graphvizSummary.RectCount
        Graphviz = $graphvizSummary
        Managed = $managedSummary
    }
}

function Invoke-ExportComparisonRun {
    param(
        [Parameter(Mandatory)]$Graph,
        [Parameter(Mandatory)][string]$GraphLabel,
        [Parameter(Mandatory)][string]$OutputDir,
        [Parameter(Mandatory)][string]$GraphvizSfdpPath,
        [Parameter(Mandatory)][int]$SfdpSeed,
        [Parameter(Mandatory)][int]$SfdpOverlapRemovalIterations,
        [switch]$AllowPartial
    )

    Ensure-Directory -Path $OutputDir

    $svgDir = Join-Path $OutputDir 'svg'
    $pngDir = Join-Path $OutputDir 'png'
    $jpgDir = Join-Path $OutputDir 'jpg'
    $logDir = Join-Path $OutputDir 'logs'
    $diffDir = Join-Path $OutputDir 'diff'
    Ensure-Directory -Path $svgDir
    Ensure-Directory -Path $pngDir
    Ensure-Directory -Path $jpgDir
    Ensure-Directory -Path $logDir
    Ensure-Directory -Path $diffDir

    $dotPath = Join-Path $OutputDir "$GraphLabel.gv"
    Export-Graph -Graph $Graph -Format Graphviz -Path $dotPath

    $graphvizNodeIdMap = Get-DotNodeIdMap -Path $dotPath
    Set-DotNodeLabelsEmpty -Path $dotPath

    $originalToGraphvizNodeIdMap = @{}
    foreach ($entry in $graphvizNodeIdMap.GetEnumerator()) {
        $originalToGraphvizNodeIdMap[$entry.Value] = $entry.Key
    }

    $managedGraph = New-GraphWithRemappedIds -Graph $Graph -OriginalIdToRemappedId $originalToGraphvizNodeIdMap

    $graphvizArgs = @(
        '-Nshape=circle',
        '-Nfixedsize=true',
        '-Nwidth=0.02',
        '-Nheight=0.02',
        '-Nlabel=',
        '-Earrowsize=0.08',
        '-Epenwidth=0.2',
        '-Ecolor=#00000018',
        "-Gstart=$SfdpSeed",
        "-Goverlap=prism$SfdpOverlapRemovalIterations",
        '-Gsep=+4',
        '-Goutputorder=edgesfirst'
    )

    $graphvizResults = [ordered]@{
        Svg = Invoke-GraphvizExport `
            -GraphvizSfdpPath $GraphvizSfdpPath `
            -GraphvizArgs $graphvizArgs `
            -DotPath $dotPath `
            -FormatName 'Svg' `
            -TargetSpec 'svg:svg' `
            -OutputPath (Join-Path $svgDir "$GraphLabel-graphviz.svg") `
            -LogPath (Join-Path $logDir "$GraphLabel-graphviz-svg.verbose.log") `
            -AllowPartial:$AllowPartial
        Png = Invoke-GraphvizExport `
            -GraphvizSfdpPath $GraphvizSfdpPath `
            -GraphvizArgs $graphvizArgs `
            -DotPath $dotPath `
            -FormatName 'Png' `
            -TargetSpec 'png:cairo' `
            -OutputPath (Join-Path $pngDir "$GraphLabel-graphviz.png") `
            -LogPath (Join-Path $logDir "$GraphLabel-graphviz-png.verbose.log") `
            -AllowPartial:$AllowPartial
        Jpg = Invoke-GraphvizExport `
            -GraphvizSfdpPath $GraphvizSfdpPath `
            -GraphvizArgs $graphvizArgs `
            -DotPath $dotPath `
            -FormatName 'Jpg' `
            -TargetSpec 'jpg:cairo' `
            -OutputPath (Join-Path $jpgDir "$GraphLabel-graphviz.jpg") `
            -LogPath (Join-Path $logDir "$GraphLabel-graphviz-jpg.verbose.log") `
            -AllowPartial:$AllowPartial
    }

    $managedResults = [ordered]@{
        Svg = Invoke-ManagedExport `
            -Graph $managedGraph `
            -FormatName 'Svg' `
            -OutputPath (Join-Path $svgDir "$GraphLabel-managed.svg") `
            -DiagnosticsPath (Join-Path $logDir "$GraphLabel-managed-svg.diagnostics.jsonl") `
            -SfdpSeed $SfdpSeed `
            -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
            -AllowPartial:$AllowPartial
        Png = Invoke-ManagedExport `
            -Graph $managedGraph `
            -FormatName 'Png' `
            -OutputPath (Join-Path $pngDir "$GraphLabel-managed.png") `
            -DiagnosticsPath (Join-Path $logDir "$GraphLabel-managed-png.diagnostics.jsonl") `
            -SfdpSeed $SfdpSeed `
            -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
            -AllowPartial:$AllowPartial
        Jpg = Invoke-ManagedExport `
            -Graph $managedGraph `
            -FormatName 'Jpg' `
            -OutputPath (Join-Path $jpgDir "$GraphLabel-managed.jpg") `
            -DiagnosticsPath (Join-Path $logDir "$GraphLabel-managed-jpg.diagnostics.jsonl") `
            -SfdpSeed $SfdpSeed `
            -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
            -AllowPartial:$AllowPartial
    }

    return [ordered]@{
        Graph = [ordered]@{
            Label = $GraphLabel
            VertexCount = $Graph.VertexCount
            EdgeCount = $Graph.EdgeCount
            DotPath = $dotPath
        }
        Graphviz = [ordered]@{
            SfdpPath = $GraphvizSfdpPath
            Seed = $SfdpSeed
            OverlapRemovalIterations = $SfdpOverlapRemovalIterations
            Outputs = $graphvizResults
        }
        Managed = [ordered]@{
            Seed = $SfdpSeed
            OverlapRemovalIterations = $SfdpOverlapRemovalIterations
            Outputs = $managedResults
        }
        Comparisons = [ordered]@{
            Svg = Get-SvgComparisonSummary -GraphvizResult $graphvizResults.Svg -ManagedResult $managedResults.Svg
            Diagnostics = Get-DiagnosticsComparisonSummary -GraphvizResult $graphvizResults.Svg -ManagedResult $managedResults.Svg
            Png = [ordered]@{
                Available = $false
                Reason = 'Pixel diff will be added after managed PNG support exists.'
            }
            Jpg = [ordered]@{
                Available = $false
                Reason = 'Pixel diff will be added after managed JPG support exists.'
            }
        }
        OutputDirectories = [ordered]@{
            Root = $OutputDir
            Svg = $svgDir
            Png = $pngDir
            Jpg = $jpgDir
            Logs = $logDir
            Diff = $diffDir
        }
    }
}
