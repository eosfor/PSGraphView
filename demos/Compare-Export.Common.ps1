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
    $edgePaths = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' edge ')]/*[local-name()='path']")
    $circles = $document.SelectNodes("//*[local-name()='circle']")
    $ellipses = $document.SelectNodes("//*[local-name()='ellipse']")
    $texts = $document.SelectNodes("//*[local-name()='text']")
    $anchors = $document.SelectNodes("//*[local-name()='a']")
    $rectangles = $document.SelectNodes("//*[local-name()='rect']")
    $graphTransformValue = $null
    $graphGroupIdValue = $null
    if ($null -ne $graphGroup -and $null -ne $graphGroup.Attributes['transform']) {
        $graphTransformValue = [string]$graphGroup.Attributes['transform'].Value
    }
    if ($null -ne $graphGroup -and $null -ne $graphGroup.Attributes['id']) {
        $graphGroupIdValue = [string]$graphGroup.Attributes['id'].Value
    }

    $viewBoxRect = Get-SvgViewBoxRect -ViewBox ([string]$svg.GetAttribute('viewBox'))
    $graphTransform = Get-SvgGraphTransform -Transform $graphTransformValue
    $visibleNodeIds = New-Object System.Collections.Generic.List[string]
    $visibleEdgeTitles = New-Object System.Collections.Generic.List[string]
    $presentNodeIds = New-Object System.Collections.Generic.List[string]
    $presentEdgeTitles = New-Object System.Collections.Generic.List[string]
    $nodeLabelEntries = New-Object System.Collections.Generic.List[object]

    foreach ($nodeGroup in $nodeGroups) {
        $titleNode = $nodeGroup.SelectSingleNode("./*[local-name()='title']")
        $ellipseNode = $nodeGroup.SelectSingleNode("./*[local-name()='ellipse']")
        if ($null -eq $titleNode -or $null -eq $ellipseNode) {
            continue
        }

        $title = [string]$titleNode.InnerText
        $presentNodeIds.Add($title)

        if ($null -ne $viewBoxRect -and $null -ne $graphTransform) {
            $bounds = Get-SvgEllipseBounds -EllipseNode $ellipseNode -Transform $graphTransform
            if ($null -ne $bounds -and (Test-SvgRectIntersection -Left $bounds.MinX -Top $bounds.MinY -Right $bounds.MaxX -Bottom $bounds.MaxY -ViewBoxRect $viewBoxRect)) {
                $visibleNodeIds.Add($title)
            }
        }

        $textNode = $nodeGroup.SelectSingleNode("./*[local-name()='text']")
        if ($null -ne $textNode) {
            $nodeLabelEntries.Add((Get-SvgNodeLabelEntry -NodeTitle $title -EllipseNode $ellipseNode -TextNode $textNode))
        }
    }

    foreach ($edgeGroup in $edgeGroups) {
        $titleNode = $edgeGroup.SelectSingleNode("./*[local-name()='title']")
        $pathNode = $edgeGroup.SelectSingleNode("./*[local-name()='path']")
        if ($null -eq $titleNode -or $null -eq $pathNode) {
            continue
        }

        $title = [string]$titleNode.InnerText
        $presentEdgeTitles.Add($title)

        if ($null -ne $viewBoxRect -and $null -ne $graphTransform) {
            $bounds = Get-SvgPathBounds -PathData ([string]$pathNode.Attributes['d'].Value) -Transform $graphTransform
            if ($null -ne $bounds -and (Test-SvgRectIntersection -Left $bounds.MinX -Top $bounds.MinY -Right $bounds.MaxX -Bottom $bounds.MaxY -ViewBoxRect $viewBoxRect)) {
                $visibleEdgeTitles.Add($title)
            }
        }
    }

    $labelTexts = @($nodeLabelEntries | ForEach-Object { [string]$_['Text'] })
    $fontFamilies = @($nodeLabelEntries | ForEach-Object { [string]$_['FontFamily'] } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $textAnchors = @($nodeLabelEntries | ForEach-Object { [string]$_['TextAnchor'] } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $averageNodeLabelOffsetX = Get-AverageOrNull -Values @($nodeLabelEntries | ForEach-Object { $_['OffsetX'] })
    $averageNodeLabelBaselineOffsetY = Get-AverageOrNull -Values @($nodeLabelEntries | ForEach-Object { $_['BaselineOffsetY'] })
    $averageNodeLabelFontSize = Get-AverageOrNull -Values @($nodeLabelEntries | ForEach-Object { $_['FontSize'] })
    $centeredNodeLabelCount = @($nodeLabelEntries | Where-Object {
            $offsetX = $_['OffsetX']
            $offsetY = $_['BaselineOffsetY']
            $offsetX -is [double] -and
            $offsetY -is [double] -and
            [Math]::Abs([double]$offsetX) -le 1.0 -and
            [Math]::Abs([double]$offsetY) -le 1.5
        }).Count

    $summary = [ordered]@{}
    $summary['Width'] = [string]$svg.GetAttribute('width')
    $summary['Height'] = [string]$svg.GetAttribute('height')
    $summary['ViewBox'] = [string]$svg.GetAttribute('viewBox')
    $summary['GraphGroupId'] = $graphGroupIdValue
    $summary['GraphGroupTransform'] = $graphTransformValue
    $summary['GroupCount'] = $groups.Count
    $summary['NodeGroupCount'] = $nodeGroups.Count
    $summary['EdgeGroupCount'] = $edgeGroups.Count
    $summary['ClusterGroupCount'] = $clusterGroups.Count
    $summary['TitleCount'] = $titles.Count
    $summary['PathCount'] = $edgePaths.Count
    $summary['CircleCount'] = $circles.Count
    $summary['EllipseCount'] = $ellipses.Count
    $summary['TextCount'] = $texts.Count
    $summary['AnchorCount'] = $anchors.Count
    $summary['RectCount'] = $rectangles.Count
    $summary['PresentNodeCount'] = $presentNodeIds.Count
    $summary['PresentEdgeCount'] = $presentEdgeTitles.Count
    $summary['VisibleNodeCount'] = $visibleNodeIds.Count
    $summary['VisibleEdgeCount'] = $visibleEdgeTitles.Count
    $summary['NodeLabelCount'] = $nodeLabelEntries.Count
    $summary['NodeLabelTexts'] = $labelTexts
    $summary['FontFamilies'] = $fontFamilies
    $summary['TextAnchors'] = $textAnchors
    $summary['AverageNodeLabelOffsetX'] = $averageNodeLabelOffsetX
    $summary['AverageNodeLabelBaselineOffsetY'] = $averageNodeLabelBaselineOffsetY
    $summary['AverageNodeLabelFontSize'] = $averageNodeLabelFontSize
    $summary['CenteredNodeLabelCount'] = $centeredNodeLabelCount
    $summary['PresentNodeIds'] = @($presentNodeIds)
    $summary['PresentEdgeTitles'] = @($presentEdgeTitles)
    $summary['VisibleNodeIds'] = @($visibleNodeIds)
    $summary['VisibleEdgeTitles'] = @($visibleEdgeTitles)
    return $summary
}

function Get-AverageOrNull {
    param([Parameter(Mandatory)][object[]]$Values)

    $numbers = @($Values | Where-Object { $_ -is [double] -or $_ -is [float] -or $_ -is [int] -or $_ -is [long] } | ForEach-Object { [double]$_ })
    if ($numbers.Count -eq 0) {
        return $null
    }

    return ($numbers | Measure-Object -Average).Average
}

function Get-SvgAttributeDouble {
    param(
        [Parameter(Mandatory)]$Node,
        [Parameter(Mandatory)][string]$AttributeName
    )

    $attribute = $Node.Attributes[$AttributeName]
    if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace([string]$attribute.Value)) {
        return $null
    }

    return [double]::Parse([string]$attribute.Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-SvgFontSizeValue {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $match = [regex]::Match($Value, '-?\d+(?:\.\d+)?', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        return $null
    }

    return [double]::Parse($match.Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-SvgNodeLabelEntry {
    param(
        [Parameter(Mandatory)][string]$NodeTitle,
        [Parameter(Mandatory)]$EllipseNode,
        [Parameter(Mandatory)]$TextNode
    )

    $cx = Get-SvgAttributeDouble -Node $EllipseNode -AttributeName 'cx'
    $cy = Get-SvgAttributeDouble -Node $EllipseNode -AttributeName 'cy'
    $x = Get-SvgAttributeDouble -Node $TextNode -AttributeName 'x'
    $y = Get-SvgAttributeDouble -Node $TextNode -AttributeName 'y'
    $fontSize = Get-SvgFontSizeValue -Value ([string]$TextNode.Attributes['font-size']?.Value)
    $fontFamily = [string]$TextNode.Attributes['font-family']?.Value
    $textAnchor = [string]$TextNode.Attributes['text-anchor']?.Value

    return [ordered]@{
        NodeTitle = $NodeTitle
        Text = [string]$TextNode.InnerText
        X = $x
        Y = $y
        FontSize = $fontSize
        FontFamily = $fontFamily
        TextAnchor = $textAnchor
        OffsetX = if ($x -is [double] -and $cx -is [double]) { [double]$x - [double]$cx } else { $null }
        BaselineOffsetY = if ($y -is [double] -and $cy -is [double]) { [double]$y - [double]$cy } else { $null }
    }
}

function Get-SvgLabelRasterRegions {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    [xml]$document = Get-Content -Path $Path -Raw
    $svg = $document.DocumentElement
    if ($null -eq $svg) {
        return $null
    }

    $graphGroup = $document.SelectSingleNode("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' graph ')]")
    $graphTransformValue = if ($null -ne $graphGroup -and $null -ne $graphGroup.Attributes['transform']) { [string]$graphGroup.Attributes['transform'].Value } else { $null }
    $graphTransform = Get-SvgGraphTransform -Transform $graphTransformValue
    $viewBoxRect = Get-SvgViewBoxRect -ViewBox ([string]$svg.GetAttribute('viewBox'))
    if ($null -eq $graphTransform -or $null -eq $viewBoxRect) {
        return $null
    }

    $nodeGroups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' node ')]")
    $regions = New-Object System.Collections.Generic.List[object]
    foreach ($nodeGroup in $nodeGroups) {
        $titleNode = $nodeGroup.SelectSingleNode("./*[local-name()='title']")
        $ellipseNode = $nodeGroup.SelectSingleNode("./*[local-name()='ellipse']")
        $textNode = $nodeGroup.SelectSingleNode("./*[local-name()='text']")
        if ($null -eq $titleNode -or $null -eq $ellipseNode -or $null -eq $textNode) {
            continue
        }

        $entry = Get-SvgNodeLabelEntry -NodeTitle ([string]$titleNode.InnerText) -EllipseNode $ellipseNode -TextNode $textNode
        if ($null -eq $entry.FontSize -or [string]::IsNullOrWhiteSpace([string]$entry.Text)) {
            continue
        }

        $fontSize = [double]$entry.FontSize
        $labelWidth = [Math]::Max(6.0, ([string]$entry.Text).Length * $fontSize * 0.56)
        $labelLeft = switch ([string]$entry.TextAnchor) {
            'middle' { [double]$entry.X - ($labelWidth * 0.5) }
            'end' { [double]$entry.X - $labelWidth }
            default { [double]$entry.X }
        }
        $labelTop = [double]$entry.Y - $fontSize
        $labelBottom = [double]$entry.Y + ($fontSize * 0.35)
        $labelRight = $labelLeft + $labelWidth

        $corners = @(
            (Transform-SvgPoint -X $labelLeft -Y $labelTop -Transform $graphTransform),
            (Transform-SvgPoint -X $labelRight -Y $labelTop -Transform $graphTransform),
            (Transform-SvgPoint -X $labelLeft -Y $labelBottom -Transform $graphTransform),
            (Transform-SvgPoint -X $labelRight -Y $labelBottom -Transform $graphTransform)
        )
        $cornerXs = @($corners | ForEach-Object { [double]$_['X'] })
        $cornerYs = @($corners | ForEach-Object { [double]$_['Y'] })

        $region = New-Object System.Collections.Hashtable
        $region['NodeTitle'] = $entry.NodeTitle
        $region['Text'] = $entry.Text
        $region['MinX'] = ($cornerXs | Measure-Object -Minimum).Minimum
        $region['MinY'] = ($cornerYs | Measure-Object -Minimum).Minimum
        $region['MaxX'] = ($cornerXs | Measure-Object -Maximum).Maximum
        $region['MaxY'] = ($cornerYs | Measure-Object -Maximum).Maximum
        $regions.Add($region)
    }

    $result = New-Object System.Collections.Hashtable
    $result['ViewBox'] = $viewBoxRect
    $result['Regions'] = $regions.ToArray()
    return $result
}

function Get-SvgViewBoxRect {
    param([string]$ViewBox)

    if ([string]::IsNullOrWhiteSpace($ViewBox)) {
        return $null
    }

    $parts = $ViewBox -split '\s+' | Where-Object { $_ -ne '' }
    if ($parts.Count -ne 4) {
        return $null
    }

    return [ordered]@{
        MinX = [double]::Parse($parts[0], [System.Globalization.CultureInfo]::InvariantCulture)
        MinY = [double]::Parse($parts[1], [System.Globalization.CultureInfo]::InvariantCulture)
        Width = [double]::Parse($parts[2], [System.Globalization.CultureInfo]::InvariantCulture)
        Height = [double]::Parse($parts[3], [System.Globalization.CultureInfo]::InvariantCulture)
    }
}

function Get-SvgGraphTransform {
    param([string]$Transform)

    if ([string]::IsNullOrWhiteSpace($Transform)) {
        return $null
    }

    $match = [regex]::Match(
        $Transform,
        '^scale\((?<scaleX>-?\d+(?:\.\d+)?) (?<scaleY>-?\d+(?:\.\d+)?)\) rotate\((?<rotation>-?\d+(?:\.\d+)?)\) translate\((?<translateX>-?\d+(?:\.\d+)?) (?<translateY>-?\d+(?:\.\d+)?)\)$',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        return $null
    }

    return [ordered]@{
        ScaleX = [double]::Parse($match.Groups['scaleX'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        ScaleY = [double]::Parse($match.Groups['scaleY'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        Rotation = [double]::Parse($match.Groups['rotation'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        TranslateX = [double]::Parse($match.Groups['translateX'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        TranslateY = [double]::Parse($match.Groups['translateY'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }
}

function Transform-SvgPoint {
    param(
        [double]$X,
        [double]$Y,
        [Parameter(Mandatory)]$Transform
    )

    $scaledX = $X * [double]$Transform.ScaleX
    $scaledY = $Y * [double]$Transform.ScaleY
    $rotationRadians = [double]$Transform.Rotation * [Math]::PI / 180.0
    $cos = [Math]::Cos($rotationRadians)
    $sin = [Math]::Sin($rotationRadians)

    $rotatedX = ($scaledX * $cos) - ($scaledY * $sin)
    $rotatedY = ($scaledX * $sin) + ($scaledY * $cos)

    return [ordered]@{
        X = $rotatedX + [double]$Transform.TranslateX
        Y = $rotatedY + [double]$Transform.TranslateY
    }
}

function Get-SvgEllipseBounds {
    param(
        [Parameter(Mandatory)]$EllipseNode,
        [Parameter(Mandatory)]$Transform
    )

    $cx = [double]::Parse([string]$EllipseNode.Attributes['cx'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    $cy = [double]::Parse([string]$EllipseNode.Attributes['cy'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    $rx = [double]::Parse([string]$EllipseNode.Attributes['rx'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    $ry = [double]::Parse([string]$EllipseNode.Attributes['ry'].Value, [System.Globalization.CultureInfo]::InvariantCulture)

    $points = @(
        (Transform-SvgPoint -X ($cx - $rx) -Y ($cy - $ry) -Transform $Transform),
        (Transform-SvgPoint -X ($cx - $rx) -Y ($cy + $ry) -Transform $Transform),
        (Transform-SvgPoint -X ($cx + $rx) -Y ($cy - $ry) -Transform $Transform),
        (Transform-SvgPoint -X ($cx + $rx) -Y ($cy + $ry) -Transform $Transform)
    )

    return [ordered]@{
        MinX = ($points | Measure-Object -Property X -Minimum).Minimum
        MinY = ($points | Measure-Object -Property Y -Minimum).Minimum
        MaxX = ($points | Measure-Object -Property X -Maximum).Maximum
        MaxY = ($points | Measure-Object -Property Y -Maximum).Maximum
    }
}

function Get-SvgPathBounds {
    param(
        [Parameter(Mandatory)][string]$PathData,
        [Parameter(Mandatory)]$Transform
    )

    $matches = [regex]::Matches($PathData, '-?\d+(?:\.\d+)?')
    if ($matches.Count -lt 8) {
        return $null
    }

    $values = foreach ($match in $matches) {
        [double]::Parse($match.Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    $p0x = $values[0]
    $p0y = $values[1]
    $p1x = $values[2]
    $p1y = $values[3]
    $p2x = $values[4]
    $p2y = $values[5]
    $p3x = $values[6]
    $p3y = $values[7]

    $sampleCount = 32
    $points = New-Object System.Collections.Generic.List[object]
    for ($index = 0; $index -le $sampleCount; $index++) {
        $t = $index / [double]$sampleCount
        $point = Get-CubicBezierPoint -P0X $p0x -P0Y $p0y -P1X $p1x -P1Y $p1y -P2X $p2x -P2Y $p2y -P3X $p3x -P3Y $p3y -T $t
        $points.Add((Transform-SvgPoint -X $point.X -Y $point.Y -Transform $Transform))
    }

    return [ordered]@{
        MinX = ($points | Measure-Object -Property X -Minimum).Minimum
        MinY = ($points | Measure-Object -Property Y -Minimum).Minimum
        MaxX = ($points | Measure-Object -Property X -Maximum).Maximum
        MaxY = ($points | Measure-Object -Property Y -Maximum).Maximum
    }
}

function Get-CubicBezierPoint {
    param(
        [double]$P0X,
        [double]$P0Y,
        [double]$P1X,
        [double]$P1Y,
        [double]$P2X,
        [double]$P2Y,
        [double]$P3X,
        [double]$P3Y,
        [double]$T
    )

    $oneMinusT = 1.0 - $T
    $x = ($oneMinusT * $oneMinusT * $oneMinusT * $P0X) +
         (3.0 * $oneMinusT * $oneMinusT * $T * $P1X) +
         (3.0 * $oneMinusT * $T * $T * $P2X) +
         ($T * $T * $T * $P3X)
    $y = ($oneMinusT * $oneMinusT * $oneMinusT * $P0Y) +
         (3.0 * $oneMinusT * $oneMinusT * $T * $P1Y) +
         (3.0 * $oneMinusT * $T * $T * $P2Y) +
         ($T * $T * $T * $P3Y)

    return [ordered]@{
        X = $x
        Y = $y
    }
}

function Test-SvgRectIntersection {
    param(
        [double]$Left,
        [double]$Top,
        [double]$Right,
        [double]$Bottom,
        [Parameter(Mandatory)]$ViewBoxRect
    )

    $viewLeft = [double]$ViewBoxRect.MinX
    $viewTop = [double]$ViewBoxRect.MinY
    $viewRight = $viewLeft + [double]$ViewBoxRect.Width
    $viewBottom = $viewTop + [double]$ViewBoxRect.Height

    return -not ($Right -lt $viewLeft -or $Left -gt $viewRight -or $Bottom -lt $viewTop -or $Top -gt $viewBottom)
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
            $summary = Get-PngMetadata -Path $Path
            $pixels = Get-RasterPixelSummary -Path $Path
            if ($null -ne $pixels) {
                foreach ($entry in $pixels.GetEnumerator()) {
                    $summary[$entry.Key] = $entry.Value
                }
            }

            return $summary
        }
        'Jpg' {
            $summary = Get-JpegMetadata -Path $Path
            $pixels = Get-RasterPixelSummary -Path $Path
            if ($null -ne $pixels) {
                foreach ($entry in $pixels.GetEnumerator()) {
                    $summary[$entry.Key] = $entry.Value
                }
            }

            return $summary
        }
        default {
            throw "Unsupported format '$Format'."
        }
    }
}

function Get-RasterPixelSummary {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path $Path)) {
        return $null
    }

    Ensure-SkiaSharpAssemblyLoaded

    $skDataType = 'SkiaSharp.SKData' -as [type]
    $skImageType = 'SkiaSharp.SKImage' -as [type]
    $skBitmapType = 'SkiaSharp.SKBitmap' -as [type]
    $skImageInfoType = 'SkiaSharp.SKImageInfo' -as [type]
    if ($null -eq $skDataType -or $null -eq $skImageType -or $null -eq $skBitmapType -or $null -eq $skImageInfoType) {
        return $null
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $data = [SkiaSharp.SKData]::CreateCopy($bytes)
    try {
        $image = [SkiaSharp.SKImage]::FromEncodedData($data)
        if ($null -eq $image) {
            return $null
        }

        try {
            $info = [SkiaSharp.SKImageInfo]::new(
                $image.Width,
                $image.Height,
                [SkiaSharp.SKColorType]::Rgba8888,
                [SkiaSharp.SKAlphaType]::Unpremul)
            $bitmap = [SkiaSharp.SKBitmap]::new($info)
            try {
                $ok = $image.ReadPixels($info, $bitmap.GetPixels(), $info.RowBytes, 0, 0)
                if (-not $ok) {
                    return $null
                }

                $pixels = New-Object byte[] ($info.RowBytes * $info.Height)
                [System.Runtime.InteropServices.Marshal]::Copy($bitmap.GetPixels(), $pixels, 0, $pixels.Length)

                $opaquePixelCount = 0
                $transparentPixelCount = 0
                $darkPixelCount = 0
                $nonWhitePixelCount = 0
                $nearFallbackPixelCount = 0

                for ($index = 0; $index -lt $pixels.Length; $index += 4) {
                    $r = [int]$pixels[$index]
                    $g = [int]$pixels[$index + 1]
                    $b = [int]$pixels[$index + 2]
                    $a = [int]$pixels[$index + 3]

                    if ($a -eq 255) {
                        $opaquePixelCount++
                    }
                    elseif ($a -eq 0) {
                        $transparentPixelCount++
                    }

                    if ($r -lt 250 -or $g -lt 250 -or $b -lt 250) {
                        $nonWhitePixelCount++
                    }

                    if ((($r + $g + $b) / 3.0) -lt 250) {
                        $darkPixelCount++
                    }

                    if ([Math]::Abs($r - 255) -le 6 -and [Math]::Abs($g - 255) -le 6 -and [Math]::Abs($b - 254) -le 6) {
                        $nearFallbackPixelCount++
                    }
                }

                return [ordered]@{
                    OpaquePixelCount = $opaquePixelCount
                    TransparentPixelCount = $transparentPixelCount
                    DarkPixelCount = $darkPixelCount
                    NonWhitePixelCount = $nonWhitePixelCount
                    NearFallbackPixelCount = $nearFallbackPixelCount
                }
            }
            finally {
                if ($null -ne $bitmap) {
                    $bitmap.Dispose()
                }
            }
        }
        finally {
            $image.Dispose()
        }
    }
    finally {
        $data.Dispose()
    }
}

function Get-RasterLabelRegionMetrics {
    param(
        [Parameter(Mandatory)][string]$RasterPath,
        [Parameter(Mandatory)]$SvgLabelRegions
    )

    if ($null -eq $SvgLabelRegions -or $SvgLabelRegions.Regions.Count -eq 0) {
        return $null
    }

    Ensure-SkiaSharpAssemblyLoaded

    $skDataType = 'SkiaSharp.SKData' -as [type]
    $skImageType = 'SkiaSharp.SKImage' -as [type]
    $skBitmapType = 'SkiaSharp.SKBitmap' -as [type]
    $skImageInfoType = 'SkiaSharp.SKImageInfo' -as [type]
    if ($null -eq $skDataType -or $null -eq $skImageType -or $null -eq $skBitmapType -or $null -eq $skImageInfoType) {
        return $null
    }

    $viewBox = $SvgLabelRegions.ViewBox
    $regions = $SvgLabelRegions.Regions
    $bytes = [System.IO.File]::ReadAllBytes($RasterPath)
    $data = [SkiaSharp.SKData]::CreateCopy($bytes)
    try {
        $image = [SkiaSharp.SKImage]::FromEncodedData($data)
        if ($null -eq $image) {
            return $null
        }

        try {
            $info = [SkiaSharp.SKImageInfo]::new(
                $image.Width,
                $image.Height,
                [SkiaSharp.SKColorType]::Rgba8888,
                [SkiaSharp.SKAlphaType]::Unpremul)
            $bitmap = [SkiaSharp.SKBitmap]::new($info)
            try {
                $ok = $image.ReadPixels($info, $bitmap.GetPixels(), $info.RowBytes, 0, 0)
                if (-not $ok) {
                    return $null
                }

                $pixels = New-Object byte[] ($info.RowBytes * $info.Height)
                [System.Runtime.InteropServices.Marshal]::Copy($bitmap.GetPixels(), $pixels, 0, $pixels.Length)

                $regionPixelCount = 0
                $opaquePixelCount = 0
                $darkPixelCount = 0
                $nonWhitePixelCount = 0

                foreach ($region in $regions) {
                    $minX = [Math]::Max(0, [int][Math]::Floor((([double]$region.MinX - [double]$viewBox.MinX) / [double]$viewBox.Width) * $image.Width))
                    $maxX = [Math]::Min($image.Width, [int][Math]::Ceiling((([double]$region.MaxX - [double]$viewBox.MinX) / [double]$viewBox.Width) * $image.Width))
                    $minY = [Math]::Max(0, [int][Math]::Floor((([double]$region.MinY - [double]$viewBox.MinY) / [double]$viewBox.Height) * $image.Height))
                    $maxY = [Math]::Min($image.Height, [int][Math]::Ceiling((([double]$region.MaxY - [double]$viewBox.MinY) / [double]$viewBox.Height) * $image.Height))

                    for ($y = $minY; $y -lt $maxY; $y++) {
                        for ($x = $minX; $x -lt $maxX; $x++) {
                            $index = ($y * $info.RowBytes) + ($x * 4)
                            $r = [int]$pixels[$index]
                            $g = [int]$pixels[$index + 1]
                            $b = [int]$pixels[$index + 2]
                            $a = [int]$pixels[$index + 3]

                            $regionPixelCount++
                            if ($a -eq 255) {
                                $opaquePixelCount++
                            }

                            if ($r -lt 250 -or $g -lt 250 -or $b -lt 250) {
                                $nonWhitePixelCount++
                            }

                            if ((($r + $g + $b) / 3.0) -lt 250) {
                                $darkPixelCount++
                            }
                        }
                    }
                }

                return [ordered]@{
                    RegionCount = $regions.Count
                    RegionPixelCount = $regionPixelCount
                    OpaquePixelCount = $opaquePixelCount
                    DarkPixelCount = $darkPixelCount
                    NonWhitePixelCount = $nonWhitePixelCount
                    DarkPixelDensity = Get-NullableRatio -Numerator $darkPixelCount -Denominator $opaquePixelCount
                    NonWhitePixelDensity = Get-NullableRatio -Numerator $nonWhitePixelCount -Denominator $opaquePixelCount
                }
            }
            finally {
                if ($null -ne $bitmap) {
                    $bitmap.Dispose()
                }
            }
        }
        finally {
            $image.Dispose()
        }
    }
    finally {
        $data.Dispose()
    }
}

function Ensure-SkiaSharpAssemblyLoaded {
    $loaded = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'SkiaSharp' } | Select-Object -First 1
    if ($null -ne $loaded) {
        return
    }

    $psGraphViewModule = Get-Module PSGraphView | Select-Object -First 1
    if ($null -eq $psGraphViewModule) {
        return
    }

    $moduleDir = Split-Path -Parent $psGraphViewModule.Path
    $candidate = Join-Path $moduleDir 'SkiaSharp.dll'
    if (Test-Path $candidate) {
        [System.Reflection.Assembly]::LoadFrom($candidate) | Out-Null
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

function Convert-StructuredLogFields {
    param([Parameter(Mandatory)][string]$Text)

    $tokens = $Text -split '\s+'
    $entry = [ordered]@{}

    foreach ($token in $tokens) {
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
        FontResolution = @()
        ResolvedFontFamilies = @()
        PreOverlapGeometry = @()
        OverlapGeometry = @()
    }

    foreach ($line in Get-Content -Path $Path) {
        if ($line.StartsWith('fontname: "', [System.StringComparison]::Ordinal)) {
            $requested = $null
            $resolved = $null
            $resolvedFamily = $null
            if ($line -match '^fontname:\s+"(?<requested>[^"]+)"\s+resolved to:\s+(?<resolved>.+)$') {
                $requested = $matches['requested']
                $resolved = $matches['resolved']
                if ($resolved -match '"(?<family>[^",]+),\s*\d+(?:\.\d+)?"') {
                    $resolvedFamily = $matches['family']
                }
            }
            elseif ($line -match '^fontname:\s+unable to resolve\s+"(?<requested>[^"]+)"$') {
                $requested = $matches['requested']
            }

            $entry = [ordered]@{
                Requested = $requested
                Resolved = $resolved
                ResolvedFamily = $resolvedFamily
            }
            $summary.FontResolution += $entry
            if (-not [string]::IsNullOrWhiteSpace($resolvedFamily) -and $resolvedFamily -notin $summary.ResolvedFontFamilies) {
                $summary.ResolvedFontFamilies += $resolvedFamily
            }

            continue
        }

        if ($line.StartsWith('pre overlap geometry ')) {
            $summary.PreOverlapGeometry += Convert-StructuredLogFields -Text $line.Substring('pre overlap geometry '.Length)
            continue
        }

        if ($line.StartsWith('overlap geometry ')) {
            $summary.OverlapGeometry += Convert-StructuredLogFields -Text $line.Substring('overlap geometry '.Length)
            continue
        }

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
        Labels = $null
        SvgStructure = $null
        Raster = @()
        SvgGeometry = @()
        PostprocessGeometry = @()
        OverlapGeometry = @()
        LayoutGeometry = @()
        ComponentGeometry = @()
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

        if ($phase -eq 'render' -and $name -eq 'labels') {
            $summary.Labels = $data
            continue
        }

        if ($phase -eq 'svg' -and $name -eq 'structure') {
            $summary.SvgStructure = $data
            continue
        }

        if ($phase -eq 'svg' -and $name -eq 'geometry') {
            $summary.SvgGeometry += $data
            continue
        }

        if ($phase -eq 'postprocess' -and $name -eq 'geometry') {
            $summary.PostprocessGeometry += $data
            continue
        }

        if ($phase -eq 'overlap' -and $name -eq 'geometry') {
            $summary.OverlapGeometry += $data
            continue
        }

        if ($phase -eq 'layout' -and $name -eq 'geometry') {
            $summary.LayoutGeometry += $data
            continue
        }

        if ($phase -eq 'component' -and $name -eq 'geometry') {
            $summary.ComponentGeometry += $data
            continue
        }

        if ($phase -eq 'render' -and $name -eq 'raster') {
            $summary.Raster += $data
        }
    }

    return $summary
}

function Get-GeometryStageEntry {
    param(
        [Parameter(Mandatory)]$Entries,
        [Parameter(Mandatory)][string]$Stage
    )

    $matched = @($Entries | Where-Object { [string]$_['stage'] -eq $Stage })
    if ($matched.Count -eq 0) {
        return $null
    }

    return $matched[-1]
}

function Get-GraphvizLayoutGeometry {
    param([Parameter(Mandatory)]$VerboseSummary)

    $afterOverlapRemoval = Get-GeometryStageEntry -Entries $VerboseSummary.PreOverlapGeometry -Stage 'after_overlap_removal'
    if ($null -ne $afterOverlapRemoval) {
        return $afterOverlapRemoval
    }

    $finish = Get-GeometryStageEntry -Entries $VerboseSummary.OverlapGeometry -Stage 'finish'
    if ($null -ne $finish) {
        return $finish
    }

    return $null
}

function Get-ManagedLayoutGeometry {
    param([Parameter(Mandatory)]$DiagnosticsSummary)

    $afterOverlapRemoval = Get-GeometryStageEntry -Entries $DiagnosticsSummary.PostprocessGeometry -Stage 'after_overlap_removal'
    if ($null -ne $afterOverlapRemoval) {
        return $afterOverlapRemoval
    }

    $finish = Get-GeometryStageEntry -Entries $DiagnosticsSummary.OverlapGeometry -Stage 'finish'
    if ($null -ne $finish) {
        return $finish
    }

    return $null
}

function Get-LayoutResidualClassification {
    param(
        [double]$LayoutWidthDelta,
        [double]$LayoutHeightDelta,
        [double]$ExportWidthDelta,
        [double]$ExportHeightDelta
    )

    $layoutMagnitude = [Math]::Max([Math]::Abs($LayoutWidthDelta), [Math]::Abs($LayoutHeightDelta))
    $exportMagnitude = [Math]::Max([Math]::Abs($ExportWidthDelta), [Math]::Abs($ExportHeightDelta))

    if ($exportMagnitude -le 2.0 -and $layoutMagnitude -ge 4.0) {
        return 'layout_limited'
    }

    if ($layoutMagnitude -le 2.0 -and $exportMagnitude -ge 4.0) {
        return 'export_limited'
    }

    return 'mixed'
}

function Get-LayoutResidualDimensionSummary {
    param(
        [double]$GraphvizOutput,
        [double]$ManagedOutput,
        [double]$GraphvizPad,
        [double]$ManagedPad,
        [double]$GraphvizLayout,
        [double]$ManagedLayout
    )

    $outputDelta = $ManagedOutput - $GraphvizOutput

    if ($GraphvizLayout -le 0 -and $ManagedLayout -le 0) {
        return [ordered]@{
            Available = $true
            OutputDelta = $outputDelta
            LayoutDelta = 0.0
            ExportDelta = $outputDelta
            GraphvizCoreScale = $null
            ManagedCoreScale = $null
            CoreScaleDelta = $null
            ExpectedManagedOutputAtGraphvizScale = $GraphvizOutput
        }
    }

    if ($GraphvizLayout -le 0 -or $ManagedLayout -le 0) {
        return [ordered]@{
            Available = $true
            OutputDelta = $outputDelta
            LayoutDelta = $outputDelta
            ExportDelta = 0.0
            GraphvizCoreScale = $null
            ManagedCoreScale = $null
            CoreScaleDelta = $null
            ExpectedManagedOutputAtGraphvizScale = $ManagedOutput
        }
    }

    $graphvizCoreScale = ($GraphvizOutput - (2.0 * $GraphvizPad)) / $GraphvizLayout
    $managedCoreScale = ($ManagedOutput - (2.0 * $ManagedPad)) / $ManagedLayout
    $expectedManagedOutputAtGraphvizScale = ($ManagedLayout * $graphvizCoreScale) + (2.0 * $GraphvizPad)
    $layoutDelta = $expectedManagedOutputAtGraphvizScale - $GraphvizOutput
    $exportDelta = $ManagedOutput - $expectedManagedOutputAtGraphvizScale

    return [ordered]@{
        Available = $true
        OutputDelta = $outputDelta
        LayoutDelta = $layoutDelta
        ExportDelta = $exportDelta
        GraphvizCoreScale = $graphvizCoreScale
        ManagedCoreScale = $managedCoreScale
        CoreScaleDelta = $managedCoreScale - $graphvizCoreScale
        ExpectedManagedOutputAtGraphvizScale = $expectedManagedOutputAtGraphvizScale
    }
}

function Get-LayoutResidualSummary {
    param(
        [Parameter(Mandatory)]$GraphvizVerbose,
        [Parameter(Mandatory)]$ManagedDiagnostics
    )

    $graphvizViewport = $GraphvizVerbose.Viewport
    $managedViewport = $ManagedDiagnostics.Viewport
    $graphvizGeometry = Get-GraphvizLayoutGeometry -VerboseSummary $GraphvizVerbose
    $managedGeometry = Get-ManagedLayoutGeometry -DiagnosticsSummary $ManagedDiagnostics

    if ($null -eq $graphvizViewport -or $null -eq $managedViewport -or $null -eq $graphvizGeometry -or $null -eq $managedGeometry) {
        return [ordered]@{
            Available = $false
            Reason = 'Viewport or layout geometry summary is missing.'
            Graphviz = $graphvizGeometry
            Managed = $managedGeometry
        }
    }

    $graphvizPadX = [double]$graphvizViewport['pad_x']
    $graphvizPadY = [double]$graphvizViewport['pad_y']
    $managedPadX = [double]$managedViewport['padX']
    $managedPadY = [double]$managedViewport['padY']
    $graphvizOutputWidth = [double]$graphvizViewport['width']
    $graphvizOutputHeight = [double]$graphvizViewport['height']
    $managedOutputWidth = [double]$managedViewport['outputWidth']
    $managedOutputHeight = [double]$managedViewport['outputHeight']
    $graphvizLayoutWidth = [double]$graphvizGeometry['width']
    $graphvizLayoutHeight = [double]$graphvizGeometry['height']
    $managedLayoutWidth = [double]$managedGeometry['width']
    $managedLayoutHeight = [double]$managedGeometry['height']

    $widthSummary = Get-LayoutResidualDimensionSummary `
        -GraphvizOutput $graphvizOutputWidth `
        -ManagedOutput $managedOutputWidth `
        -GraphvizPad $graphvizPadX `
        -ManagedPad $managedPadX `
        -GraphvizLayout $graphvizLayoutWidth `
        -ManagedLayout $managedLayoutWidth
    $heightSummary = Get-LayoutResidualDimensionSummary `
        -GraphvizOutput $graphvizOutputHeight `
        -ManagedOutput $managedOutputHeight `
        -GraphvizPad $graphvizPadY `
        -ManagedPad $managedPadY `
        -GraphvizLayout $graphvizLayoutHeight `
        -ManagedLayout $managedLayoutHeight

    return [ordered]@{
        Available = $true
        Classification = Get-LayoutResidualClassification `
            -LayoutWidthDelta ([double]$widthSummary.LayoutDelta) `
            -LayoutHeightDelta ([double]$heightSummary.LayoutDelta) `
            -ExportWidthDelta ([double]$widthSummary.ExportDelta) `
            -ExportHeightDelta ([double]$heightSummary.ExportDelta)
        OutputWidthDelta = $widthSummary.OutputDelta
        OutputHeightDelta = $heightSummary.OutputDelta
        LayoutWidthDelta = $widthSummary.LayoutDelta
        LayoutHeightDelta = $heightSummary.LayoutDelta
        ExportWidthDelta = $widthSummary.ExportDelta
        ExportHeightDelta = $heightSummary.ExportDelta
        GraphvizCoreWidthScale = $widthSummary.GraphvizCoreScale
        GraphvizCoreHeightScale = $heightSummary.GraphvizCoreScale
        ManagedCoreWidthScale = $widthSummary.ManagedCoreScale
        ManagedCoreHeightScale = $heightSummary.ManagedCoreScale
        CoreWidthScaleDelta = $widthSummary.CoreScaleDelta
        CoreHeightScaleDelta = $heightSummary.CoreScaleDelta
        Width = $widthSummary
        Height = $heightSummary
        Graphviz = [ordered]@{
            Viewport = $graphvizViewport
            LayoutGeometry = $graphvizGeometry
        }
        Managed = [ordered]@{
            Viewport = $managedViewport
            LayoutGeometry = $managedGeometry
        }
    }
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
    $managedLabels = $managedDiagnostics.Labels
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
        Labels = [ordered]@{
            Available = ($null -ne $managedLabels)
            Managed = $managedLabels
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
        LayoutResiduals = Get-LayoutResidualSummary -GraphvizVerbose $graphvizVerbose -ManagedDiagnostics $managedDiagnostics
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
        [switch]$IncludeLabels,
        [double]$LabelFontSize = 14.0,
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
            ShowLabels = $IncludeLabels.IsPresent
            LabelFontSize = $LabelFontSize
            ShowArrows = $true
            ArrowSize = 0.08
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
    $visibleNodeIdsMissingInManaged = @($graphvizSummary.VisibleNodeIds | Where-Object { $_ -notin $managedSummary.VisibleNodeIds })
    $visibleEdgeTitlesMissingInManaged = @($graphvizSummary.VisibleEdgeTitles | Where-Object { $_ -notin $managedSummary.VisibleEdgeTitles })

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
        VisibleNodeCountDelta = $managedSummary.VisibleNodeCount - $graphvizSummary.VisibleNodeCount
        VisibleEdgeCountDelta = $managedSummary.VisibleEdgeCount - $graphvizSummary.VisibleEdgeCount
        VisibleNodeCoverageRatio = if ($graphvizSummary.VisibleNodeCount -gt 0) { [double]$managedSummary.VisibleNodeCount / [double]$graphvizSummary.VisibleNodeCount } else { $null }
        VisibleEdgeCoverageRatio = if ($graphvizSummary.VisibleEdgeCount -gt 0) { [double]$managedSummary.VisibleEdgeCount / [double]$graphvizSummary.VisibleEdgeCount } else { $null }
        VisibleNodeIdsMissingInManaged = $visibleNodeIdsMissingInManaged
        VisibleEdgeTitlesMissingInManaged = $visibleEdgeTitlesMissingInManaged
        Graphviz = $graphvizSummary
        Managed = $managedSummary
    }
}

function Get-TextComparisonSummary {
    param(
        [object]$GraphvizResult,
        [object]$ManagedResult
    )

    if (-not $GraphvizResult.Supported -or -not $ManagedResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = 'One side did not produce SVG output for text comparison.'
        }
    }

    $graphvizSummary = $GraphvizResult.Summary
    $managedSummary = $ManagedResult.Summary
    $missingNodeLabelsInManaged = @($graphvizSummary.NodeLabelTexts | Where-Object { $_ -notin $managedSummary.NodeLabelTexts })
    $unexpectedNodeLabelsInManaged = @($managedSummary.NodeLabelTexts | Where-Object { $_ -notin $graphvizSummary.NodeLabelTexts })

    return [ordered]@{
        Available = $true
        NodeLabelCountDelta = $managedSummary.NodeLabelCount - $graphvizSummary.NodeLabelCount
        MissingNodeLabelsInManaged = $missingNodeLabelsInManaged
        UnexpectedNodeLabelsInManaged = $unexpectedNodeLabelsInManaged
        GraphvizFontFamilies = $graphvizSummary.FontFamilies
        ManagedFontFamilies = $managedSummary.FontFamilies
        GraphvizTextAnchors = $graphvizSummary.TextAnchors
        ManagedTextAnchors = $managedSummary.TextAnchors
        AverageFontSizeDelta = if ($null -ne $graphvizSummary.AverageNodeLabelFontSize -and $null -ne $managedSummary.AverageNodeLabelFontSize) { [double]$managedSummary.AverageNodeLabelFontSize - [double]$graphvizSummary.AverageNodeLabelFontSize } else { $null }
        AverageOffsetXDelta = if ($null -ne $graphvizSummary.AverageNodeLabelOffsetX -and $null -ne $managedSummary.AverageNodeLabelOffsetX) { [double]$managedSummary.AverageNodeLabelOffsetX - [double]$graphvizSummary.AverageNodeLabelOffsetX } else { $null }
        AverageBaselineOffsetYDelta = if ($null -ne $graphvizSummary.AverageNodeLabelBaselineOffsetY -and $null -ne $managedSummary.AverageNodeLabelBaselineOffsetY) { [double]$managedSummary.AverageNodeLabelBaselineOffsetY - [double]$graphvizSummary.AverageNodeLabelBaselineOffsetY } else { $null }
        CenteredNodeLabelCountDelta = $managedSummary.CenteredNodeLabelCount - $graphvizSummary.CenteredNodeLabelCount
        Graphviz = [ordered]@{
            NodeLabelCount = $graphvizSummary.NodeLabelCount
            AverageNodeLabelOffsetX = $graphvizSummary.AverageNodeLabelOffsetX
            AverageNodeLabelBaselineOffsetY = $graphvizSummary.AverageNodeLabelBaselineOffsetY
            AverageNodeLabelFontSize = $graphvizSummary.AverageNodeLabelFontSize
            CenteredNodeLabelCount = $graphvizSummary.CenteredNodeLabelCount
            NodeLabelTexts = $graphvizSummary.NodeLabelTexts
        }
        Managed = [ordered]@{
            NodeLabelCount = $managedSummary.NodeLabelCount
            AverageNodeLabelOffsetX = $managedSummary.AverageNodeLabelOffsetX
            AverageNodeLabelBaselineOffsetY = $managedSummary.AverageNodeLabelBaselineOffsetY
            AverageNodeLabelFontSize = $managedSummary.AverageNodeLabelFontSize
            CenteredNodeLabelCount = $managedSummary.CenteredNodeLabelCount
            NodeLabelTexts = $managedSummary.NodeLabelTexts
        }
    }
}

function Get-FontResolutionComparisonSummary {
    param(
        [object]$GraphvizResult,
        [object]$ManagedResult
    )

    if (-not $GraphvizResult.Supported -or -not $ManagedResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = 'One side did not produce raster output for font resolution comparison.'
        }
    }

    $graphvizVerbose = $GraphvizResult.VerboseSummary
    $managedDiagnostics = $ManagedResult.DiagnosticsSummary
    $managedRaster = if ($null -ne $managedDiagnostics -and $managedDiagnostics.Raster.Count -gt 0) { $managedDiagnostics.Raster[-1] } else { $null }
    if ($null -eq $graphvizVerbose -or $null -eq $managedRaster) {
        return [ordered]@{
            Available = $false
            Reason = 'Graphviz verbose or managed raster diagnostics summary is missing.'
        }
    }

    $graphvizResolvedFamilies = @($graphvizVerbose.ResolvedFontFamilies | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $managedResolvedFamilies = @()
    if (-not [string]::IsNullOrWhiteSpace([string]$managedRaster['resolvedLabelFontFamilies'])) {
        $managedResolvedFamilies = @(([string]$managedRaster['resolvedLabelFontFamilies']).Split(',', [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries) | Sort-Object -Unique)
    }

    return [ordered]@{
        Available = $true
        GraphvizResolvedFamilies = $graphvizResolvedFamilies
        ManagedResolvedFamilies = $managedResolvedFamilies
        MissingResolvedFamiliesInManaged = @($graphvizResolvedFamilies | Where-Object { $_ -notin $managedResolvedFamilies })
        UnexpectedResolvedFamiliesInManaged = @($managedResolvedFamilies | Where-Object { $_ -notin $graphvizResolvedFamilies })
        GraphvizFontResolution = $graphvizVerbose.FontResolution
        ManagedRasterDiagnostics = $managedRaster
    }
}

function Get-LabelRasterComparisonSummary {
    param(
        [Parameter(Mandatory)][string]$FormatName,
        [object]$GraphvizSvgResult,
        [object]$ManagedSvgResult,
        [object]$GraphvizRasterResult,
        [object]$ManagedRasterResult
    )

    if (-not $GraphvizSvgResult.Supported -or -not $ManagedSvgResult.Supported -or -not $GraphvizRasterResult.Supported -or -not $ManagedRasterResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = "One side did not produce both SVG and $FormatName output for label ROI comparison."
        }
    }

    $graphvizRegions = Get-SvgLabelRasterRegions -Path $GraphvizSvgResult.Path
    $managedRegions = Get-SvgLabelRasterRegions -Path $ManagedSvgResult.Path
    $graphvizMetrics = Get-RasterLabelRegionMetrics -RasterPath $GraphvizRasterResult.Path -SvgLabelRegions $graphvizRegions
    $managedMetrics = Get-RasterLabelRegionMetrics -RasterPath $ManagedRasterResult.Path -SvgLabelRegions $managedRegions
    if ($null -eq $graphvizMetrics -or $null -eq $managedMetrics) {
        return [ordered]@{
            Available = $false
            Reason = 'Label ROI metrics could not be computed.'
        }
    }

    return [ordered]@{
        Available = $true
        RegionCountDelta = $managedMetrics.RegionCount - $graphvizMetrics.RegionCount
        RegionPixelDelta = $managedMetrics.RegionPixelCount - $graphvizMetrics.RegionPixelCount
        DarkPixelDelta = $managedMetrics.DarkPixelCount - $graphvizMetrics.DarkPixelCount
        NonWhitePixelDelta = $managedMetrics.NonWhitePixelCount - $graphvizMetrics.NonWhitePixelCount
        DarkPixelDensityDelta = if ($null -ne $graphvizMetrics.DarkPixelDensity -and $null -ne $managedMetrics.DarkPixelDensity) { [double]$managedMetrics.DarkPixelDensity - [double]$graphvizMetrics.DarkPixelDensity } else { $null }
        NonWhitePixelDensityDelta = if ($null -ne $graphvizMetrics.NonWhitePixelDensity -and $null -ne $managedMetrics.NonWhitePixelDensity) { [double]$managedMetrics.NonWhitePixelDensity - [double]$graphvizMetrics.NonWhitePixelDensity } else { $null }
        Graphviz = $graphvizMetrics
        Managed = $managedMetrics
    }
}

function Get-RasterComparisonSummary {
    param(
        [Parameter(Mandatory)][string]$FormatName,
        [object]$GraphvizResult,
        [object]$ManagedResult
    )

    if (-not $GraphvizResult.Supported -or -not $ManagedResult.Supported) {
        return [ordered]@{
            Available = $false
            Reason = "One side did not produce $FormatName output."
        }
    }

    $graphvizSummary = $GraphvizResult.Summary
    $managedSummary = $ManagedResult.Summary
    $graphvizOpaqueCoverage = Get-NullableRatio -Numerator $graphvizSummary.OpaquePixelCount -Denominator ($graphvizSummary.Width * $graphvizSummary.Height)
    $managedOpaqueCoverage = Get-NullableRatio -Numerator $managedSummary.OpaquePixelCount -Denominator ($managedSummary.Width * $managedSummary.Height)
    $graphvizDarkDensity = Get-NullableRatio -Numerator $graphvizSummary.DarkPixelCount -Denominator $graphvizSummary.OpaquePixelCount
    $managedDarkDensity = Get-NullableRatio -Numerator $managedSummary.DarkPixelCount -Denominator $managedSummary.OpaquePixelCount
    $graphvizNonWhiteDensity = Get-NullableRatio -Numerator $graphvizSummary.NonWhitePixelCount -Denominator $graphvizSummary.OpaquePixelCount
    $managedNonWhiteDensity = Get-NullableRatio -Numerator $managedSummary.NonWhitePixelCount -Denominator $managedSummary.OpaquePixelCount
    $graphvizNearFallbackDensity = Get-NullableRatio -Numerator $graphvizSummary.NearFallbackPixelCount -Denominator $graphvizSummary.OpaquePixelCount
    $managedNearFallbackDensity = Get-NullableRatio -Numerator $managedSummary.NearFallbackPixelCount -Denominator $managedSummary.OpaquePixelCount

    return [ordered]@{
        Available = $true
        WidthMatch = $graphvizSummary.Width -eq $managedSummary.Width
        HeightMatch = $graphvizSummary.Height -eq $managedSummary.Height
        WidthDelta = $managedSummary.Width - $graphvizSummary.Width
        HeightDelta = $managedSummary.Height - $graphvizSummary.Height
        ByteCountDelta = $managedSummary.ByteCount - $graphvizSummary.ByteCount
        OpaquePixelDelta = if ($graphvizSummary.Contains('OpaquePixelCount') -and $managedSummary.Contains('OpaquePixelCount')) { $managedSummary.OpaquePixelCount - $graphvizSummary.OpaquePixelCount } else { $null }
        TransparentPixelDelta = if ($graphvizSummary.Contains('TransparentPixelCount') -and $managedSummary.Contains('TransparentPixelCount')) { $managedSummary.TransparentPixelCount - $graphvizSummary.TransparentPixelCount } else { $null }
        DarkPixelDelta = if ($graphvizSummary.Contains('DarkPixelCount') -and $managedSummary.Contains('DarkPixelCount')) { $managedSummary.DarkPixelCount - $graphvizSummary.DarkPixelCount } else { $null }
        NonWhitePixelDelta = if ($graphvizSummary.Contains('NonWhitePixelCount') -and $managedSummary.Contains('NonWhitePixelCount')) { $managedSummary.NonWhitePixelCount - $graphvizSummary.NonWhitePixelCount } else { $null }
        NearFallbackPixelDelta = if ($graphvizSummary.Contains('NearFallbackPixelCount') -and $managedSummary.Contains('NearFallbackPixelCount')) { $managedSummary.NearFallbackPixelCount - $graphvizSummary.NearFallbackPixelCount } else { $null }
        OpaqueCoverageDelta = if ($null -ne $graphvizOpaqueCoverage -and $null -ne $managedOpaqueCoverage) { $managedOpaqueCoverage - $graphvizOpaqueCoverage } else { $null }
        DarkPixelDensityDelta = if ($null -ne $graphvizDarkDensity -and $null -ne $managedDarkDensity) { $managedDarkDensity - $graphvizDarkDensity } else { $null }
        NonWhitePixelDensityDelta = if ($null -ne $graphvizNonWhiteDensity -and $null -ne $managedNonWhiteDensity) { $managedNonWhiteDensity - $graphvizNonWhiteDensity } else { $null }
        NearFallbackPixelDensityDelta = if ($null -ne $graphvizNearFallbackDensity -and $null -ne $managedNearFallbackDensity) { $managedNearFallbackDensity - $graphvizNearFallbackDensity } else { $null }
        Graphviz = $graphvizSummary
        Managed = $managedSummary
        ManagedDiagnostics = if ($ManagedResult.DiagnosticsSummary.Raster.Count -gt 0) { $ManagedResult.DiagnosticsSummary.Raster[-1] } else { $null }
    }
}

function Get-NullableRatio {
    param(
        [object]$Numerator,
        [object]$Denominator
    )

    if ($null -eq $Numerator -or $null -eq $Denominator) {
        return $null
    }

    $denominatorValue = [double]$Denominator
    if ($denominatorValue -eq 0.0) {
        return $null
    }

    return ([double]$Numerator) / $denominatorValue
}

function Invoke-ExportComparisonRun {
    param(
        [Parameter(Mandatory)]$Graph,
        [Parameter(Mandatory)][string]$GraphLabel,
        [Parameter(Mandatory)][string]$OutputDir,
        [Parameter(Mandatory)][string]$GraphvizSfdpPath,
        [Parameter(Mandatory)][int]$SfdpSeed,
        [Parameter(Mandatory)][int]$SfdpOverlapRemovalIterations,
        [switch]$IncludeLabels,
        [double]$LabelFontSize = 14.0,
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
    $managedGraph = $Graph
    if (-not $IncludeLabels.IsPresent) {
        Set-DotNodeLabelsEmpty -Path $dotPath

        $originalToGraphvizNodeIdMap = @{}
        foreach ($entry in $graphvizNodeIdMap.GetEnumerator()) {
            $originalToGraphvizNodeIdMap[$entry.Value] = $entry.Key
        }

        $managedGraph = New-GraphWithRemappedIds -Graph $Graph -OriginalIdToRemappedId $originalToGraphvizNodeIdMap
    }

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
    if (-not $IncludeLabels.IsPresent) {
        $graphvizArgs = @('-Nlabel=') + $graphvizArgs
    }

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
            -IncludeLabels:$IncludeLabels `
            -LabelFontSize $LabelFontSize `
            -AllowPartial:$AllowPartial
        Png = Invoke-ManagedExport `
            -Graph $managedGraph `
            -FormatName 'Png' `
            -OutputPath (Join-Path $pngDir "$GraphLabel-managed.png") `
            -DiagnosticsPath (Join-Path $logDir "$GraphLabel-managed-png.diagnostics.jsonl") `
            -SfdpSeed $SfdpSeed `
            -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
            -IncludeLabels:$IncludeLabels `
            -LabelFontSize $LabelFontSize `
            -AllowPartial:$AllowPartial
        Jpg = Invoke-ManagedExport `
            -Graph $managedGraph `
            -FormatName 'Jpg' `
            -OutputPath (Join-Path $jpgDir "$GraphLabel-managed.jpg") `
            -DiagnosticsPath (Join-Path $logDir "$GraphLabel-managed-jpg.diagnostics.jsonl") `
            -SfdpSeed $SfdpSeed `
            -SfdpOverlapRemovalIterations $SfdpOverlapRemovalIterations `
            -IncludeLabels:$IncludeLabels `
            -LabelFontSize $LabelFontSize `
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
            Text = Get-TextComparisonSummary -GraphvizResult $graphvizResults.Svg -ManagedResult $managedResults.Svg
            FontResolution = Get-FontResolutionComparisonSummary -GraphvizResult $graphvizResults.Png -ManagedResult $managedResults.Png
            Diagnostics = Get-DiagnosticsComparisonSummary -GraphvizResult $graphvizResults.Svg -ManagedResult $managedResults.Svg
            Png = Get-RasterComparisonSummary -FormatName 'PNG' -GraphvizResult $graphvizResults.Png -ManagedResult $managedResults.Png
            Jpg = Get-RasterComparisonSummary -FormatName 'JPG' -GraphvizResult $graphvizResults.Jpg -ManagedResult $managedResults.Jpg
            PngLabelRaster = Get-LabelRasterComparisonSummary -FormatName 'PNG' -GraphvizSvgResult $graphvizResults.Svg -ManagedSvgResult $managedResults.Svg -GraphvizRasterResult $graphvizResults.Png -ManagedRasterResult $managedResults.Png
            JpgLabelRaster = Get-LabelRasterComparisonSummary -FormatName 'JPG' -GraphvizSvgResult $graphvizResults.Svg -ManagedSvgResult $managedResults.Svg -GraphvizRasterResult $graphvizResults.Jpg -ManagedRasterResult $managedResults.Jpg
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
