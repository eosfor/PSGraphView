# Compare-WikiVote-Sfdp.ps1
# Runs the managed PSGraphView Sfdp renderer and the Graphviz sfdp CLI on the
# same wiki-Vote graph, then records basic layout metrics for both outputs.

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraphView-sfdp-compare'),
    [switch]$UseSubgraph,
    [int]$SubgraphSeedCount = 30,
    [switch]$UseLocalModules,
    [string]$PSQuickGraphManifestPath,
    [string]$PSGraphViewManifestPath,
    [string]$GraphvizSfdpPath = 'sfdp',
    [int]$SfdpSeed = 42,
    [ValidateRange(0, 100000)]
    [int]$SfdpOverlapRemovalIterations = 1000,
    [ValidateRange(1, 10000)]
    [int]$ImageWidth = 800,
    [ValidateRange(1, 10000)]
    [int]$ImageHeight = 800,
    [double]$ManagedOverlapHalfWidth = [double]::NaN,
    [double]$ManagedOverlapHalfHeight = [double]::NaN,
    [switch]$UseSuggestedManagedOverlapHalfSize
)

$ErrorActionPreference = 'Stop'

function Get-WikiVoteGraph {
    param(
        [switch]$UseSubgraph,
        [int]$SubgraphSeedCount
    )

    $dataDir = Join-Path ([System.IO.Path]::GetTempPath()) 'PSGraph-datasets'
    if (-not (Test-Path $dataDir)) {
        New-Item -ItemType Directory -Path $dataDir | Out-Null
    }

    $wikiVoteUrl = 'https://snap.stanford.edu/data/wiki-Vote.txt.gz'
    $wikiVoteGz = Join-Path $dataDir 'wiki-Vote.txt.gz'
    $wikiVoteTxt = Join-Path $dataDir 'wiki-Vote.txt'

    if (-not (Test-Path $wikiVoteTxt)) {
        Write-Host 'Downloading wiki-Vote dataset...'
        Invoke-WebRequest -Uri $wikiVoteUrl -OutFile $wikiVoteGz

        $inStream = [System.IO.File]::OpenRead($wikiVoteGz)
        $gzStream = [System.IO.Compression.GZipStream]::new($inStream, [System.IO.Compression.CompressionMode]::Decompress)
        $outStream = [System.IO.File]::Create($wikiVoteTxt)
        $gzStream.CopyTo($outStream)
        $outStream.Close()
        $gzStream.Close()
        $inStream.Close()
        Remove-Item $wikiVoteGz -ErrorAction SilentlyContinue
    }
    else {
        Write-Host "Using cached $wikiVoteTxt"
    }

    $fullGraph = Import-Graph -Path $wikiVoteTxt -Format Csv -Delimiter "`t" -NoHeader
    Write-Host "Full graph: $($fullGraph.VertexCount) vertices, $($fullGraph.EdgeCount) edges"

    if (-not $UseSubgraph) {
        return $fullGraph
    }

    Write-Host "Extracting subgraph (seed=$SubgraphSeedCount highest-degree vertices)..."

    $seedVertices = $fullGraph.Vertices |
        Sort-Object { $fullGraph.OutDegree($_) } -Descending |
        Select-Object -First $SubgraphSeedCount

    $subgraph = New-Graph
    $included = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($vertex in $seedVertices) {
        [void]$included.Add($vertex.Label)
    }

    foreach ($vertex in $seedVertices) {
        $outEdges = Get-OutEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($outEdges) {
            foreach ($edge in $outEdges) {
                if ($included.Contains($edge.Target.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }
        }

        $inEdges = Get-InEdge -Graph $fullGraph -Vertex $vertex.Label
        if ($inEdges) {
            foreach ($edge in $inEdges) {
                if ($included.Contains($edge.Source.Label)) {
                    Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
                }
            }
        }
    }

    foreach ($vertex in $seedVertices) {
        Add-Vertex -Graph $subgraph -Vertex $vertex.Label -ErrorAction SilentlyContinue | Out-Null
    }

    Write-Host "Subgraph: $($subgraph.VertexCount) vertices, $($subgraph.EdgeCount) edges"
    return $subgraph
}

function Get-GraphvizPlainCoordinates {
    param(
        [string]$Path,
        [hashtable]$NodeIdMap
    )

    $coordinates = @{}
    foreach ($line in Get-Content -Path $Path) {
        if (-not $line.StartsWith('node ')) {
            continue
        }

        $parts = $line -split '\s+'
        if ($parts.Length -lt 4) {
            continue
        }

        $nodeId = $parts[1]
        $nodeKey = if ($null -ne $NodeIdMap -and $NodeIdMap.ContainsKey($nodeId)) { $NodeIdMap[$nodeId] } else { $nodeId }

        $coordinates[$nodeKey] = [pscustomobject]@{
            X = [double]::Parse($parts[2], [System.Globalization.CultureInfo]::InvariantCulture)
            Y = [double]::Parse($parts[3], [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $coordinates
}

function Get-DotNodeIdMap {
    param([string]$Path)

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
    param([string]$Path)

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

function Get-ManagedSvgCoordinates {
    param(
        [string]$Path,
        [hashtable]$NodeIdMap
    )

    [xml]$document = Get-Content -Path $Path -Raw
    $coordinates = @{}
    $groups = $document.SelectNodes("//*[local-name()='g' and @data-node-id]")
    foreach ($group in $groups) {
        $circle = $group.SelectSingleNode("./*[local-name()='circle']")
        $shape = $circle
        if ($null -eq $shape) {
            $shape = $group.SelectSingleNode("./*[local-name()='ellipse']")
        }

        if ($null -eq $shape) {
            continue
        }

        $nodeId = $group.GetAttribute('data-node-id')
        $nodeKey = if ($null -ne $NodeIdMap -and $NodeIdMap.ContainsKey($nodeId)) { $NodeIdMap[$nodeId] } else { $nodeId }

        $coordinates[$nodeKey] = [pscustomobject]@{
            X = [double]::Parse($shape.GetAttribute('cx'), [System.Globalization.CultureInfo]::InvariantCulture)
            Y = [double]::Parse($shape.GetAttribute('cy'), [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $coordinates
}

function New-GraphWithRemappedIds {
    param(
        $Graph,
        [hashtable]$OriginalIdToRemappedId
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

function Set-ManagedSvgNodeStyleForComparison {
    param(
        [string]$Path,
        [double]$NodeRadius
    )

    [xml]$document = Get-Content -Path $Path -Raw
    $groups = $document.SelectNodes("//*[local-name()='g' and contains(concat(' ', normalize-space(@class), ' '), ' node ')]")
    foreach ($group in $groups) {
        $circle = $group.SelectSingleNode("./*[local-name()='circle']")
        if ($null -eq $circle) {
            continue
        }

        $ellipse = $document.CreateElement('ellipse', $document.DocumentElement.NamespaceURI)
        $ellipse.SetAttribute('fill', 'none')
        $ellipse.SetAttribute('stroke', 'black')
        $ellipse.SetAttribute('cx', $circle.GetAttribute('cx'))
        $ellipse.SetAttribute('cy', $circle.GetAttribute('cy'))
        $ellipse.SetAttribute('rx', $NodeRadius.ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture))
        $ellipse.SetAttribute('ry', $NodeRadius.ToString('0.###', [System.Globalization.CultureInfo]::InvariantCulture))

        [void]$group.ReplaceChild($ellipse, $circle)
    }

    $document.Save($Path)
}

function Set-ManagedSvgPresentationForComparison {
    param(
        [string]$ManagedSvgPath,
        [string]$GraphvizSvgPath
    )

    [xml]$managed = Get-Content -Path $ManagedSvgPath -Raw
    [xml]$graphviz = Get-Content -Path $GraphvizSvgPath -Raw

    $managedSvg = $managed.DocumentElement
    $graphvizSvg = $graphviz.DocumentElement
    if ($null -eq $managedSvg -or $null -eq $graphvizSvg) {
        return
    }

    $graphvizGraph = $graphviz.SelectSingleNode("//*[local-name()='g' and @id='graph0']")
    if ($null -eq $graphvizGraph) {
        return
    }

    $managedViewBoxParts = $managedSvg.GetAttribute('viewBox').Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($managedViewBoxParts.Length -ne 4) {
        return
    }

    $minX = [double]::Parse($managedViewBoxParts[0], [System.Globalization.CultureInfo]::InvariantCulture)
    $minY = [double]::Parse($managedViewBoxParts[1], [System.Globalization.CultureInfo]::InvariantCulture)
    $width = [double]::Parse($managedViewBoxParts[2], [System.Globalization.CultureInfo]::InvariantCulture)
    $height = [double]::Parse($managedViewBoxParts[3], [System.Globalization.CultureInfo]::InvariantCulture)

    $graphvizViewBoxParts = $graphvizSvg.GetAttribute('viewBox').Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($graphvizViewBoxParts.Length -ne 4) {
        return
    }

    $graphvizWidth = [double]::Parse($graphvizViewBoxParts[2], [System.Globalization.CultureInfo]::InvariantCulture)
    $graphvizHeight = [double]::Parse($graphvizViewBoxParts[3], [System.Globalization.CultureInfo]::InvariantCulture)
    $usableWidth = [Math]::Max(1.0, $graphvizWidth - 8.0)
    $usableHeight = [Math]::Max(1.0, $graphvizHeight - 8.0)
    $scale = [Math]::Min($usableWidth / [Math]::Max($width, 1.0), $usableHeight / [Math]::Max($height, 1.0))

    $nsUri = $managedSvg.NamespaceURI
    $workingGroup = $managed.CreateElement('g', $nsUri)
    foreach ($child in @($managedSvg.ChildNodes)) {
        if ($child -is [System.Xml.XmlElement] -and $child.LocalName -eq 'rect') {
            continue
        }

        [void]$workingGroup.AppendChild($child.CloneNode($true))
    }

    Transform-SvgCoordinates `
        -Node $workingGroup `
        -MinX $minX `
        -MinY $minY `
        -Scale $scale `
        -Culture ([System.Globalization.CultureInfo]::InvariantCulture)

    while ($managedSvg.HasChildNodes) {
        [void]$managedSvg.RemoveChild($managedSvg.FirstChild)
    }

    $managedSvg.SetAttribute('xmlns', 'http://www.w3.org/2000/svg')
    $managedSvg.SetAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink')
    $managedSvg.SetAttribute('width', $graphvizSvg.GetAttribute('width'))
    $managedSvg.SetAttribute('height', $graphvizSvg.GetAttribute('height'))
    $managedSvg.SetAttribute('viewBox', $graphvizSvg.GetAttribute('viewBox'))

    $graphGroup = $managed.CreateElement('g', $nsUri)
    $graphGroup.SetAttribute('id', 'graph0')
    $graphGroup.SetAttribute('class', 'graph')
    $graphGroup.SetAttribute('transform', $graphvizGraph.GetAttribute('transform'))
    [void]$graphGroup.AppendChild($workingGroup)
    [void]$managedSvg.AppendChild($graphGroup)

    $managed.Save($ManagedSvgPath)
}

function Get-GraphvizAverageLabelSize {
    param([string]$Path)

    foreach ($line in Get-Content -Path $Path) {
        if ($line -match 'avg_label-size=\s*([0-9eE\+\-\.]+)') {
            return [double]::Parse($matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $null
}

function Get-GraphvizBaseLabelSize {
    param([string]$Path)

    foreach ($line in Get-Content -Path $Path) {
        if ($line -match 'pre overlap geometry stage=after_pcp_rotate .* avg_label_size=([0-9eE\+\-\.]+)') {
            return [double]::Parse($matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $null
}

function Get-GraphvizGeometryCheckpoint {
    param(
        [string]$Path,
        [ValidateSet('pre', 'overlap')]
        [string]$Kind,
        [string]$Stage
    )

    $escapedStage = [System.Text.RegularExpressions.Regex]::Escape($Stage)
    $pattern = if ($Kind -eq 'pre') {
        "pre overlap geometry stage=$escapedStage .* width=([0-9eE\\+\\-\\.]+) height=([0-9eE\\+\\-\\.]+) avg_edge_len=([0-9eE\\+\\-\\.]+) avg_label_size=([0-9eE\\+\\-\\.]+)"
    }
    else {
        "overlap geometry stage=$escapedStage iter=([-0-9]+) .* width=([0-9eE\\+\\-\\.]+) height=([0-9eE\\+\\-\\.]+) avg_edge_len=([0-9eE\\+\\-\\.]+) avg_label_size=([0-9eE\\+\\-\\.]+)"
    }

    foreach ($line in Get-Content -Path $Path) {
        if ($line -notmatch $pattern) {
            continue
        }

        if ($Kind -eq 'pre') {
            return [ordered]@{
                Width = [double]::Parse($matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
                Height = [double]::Parse($matches[2], [System.Globalization.CultureInfo]::InvariantCulture)
                AverageEdgeLength = [double]::Parse($matches[3], [System.Globalization.CultureInfo]::InvariantCulture)
                AverageLabelSize = [double]::Parse($matches[4], [System.Globalization.CultureInfo]::InvariantCulture)
            }
        }

        return [ordered]@{
            Iteration = [int]::Parse($matches[1], [System.Globalization.CultureInfo]::InvariantCulture)
            Width = [double]::Parse($matches[2], [System.Globalization.CultureInfo]::InvariantCulture)
            Height = [double]::Parse($matches[3], [System.Globalization.CultureInfo]::InvariantCulture)
            AverageEdgeLength = [double]::Parse($matches[4], [System.Globalization.CultureInfo]::InvariantCulture)
            AverageLabelSize = [double]::Parse($matches[5], [System.Globalization.CultureInfo]::InvariantCulture)
        }
    }

    return $null
}

function Get-ManagedMainComponentGeometry {
    param([string]$Path)

    $events = foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $line | ConvertFrom-Json
    }

    function Find-ManagedEvent {
        param(
            [object[]]$Events,
            [string]$Phase,
            [string]$Name,
            [string]$Stage
        )

        foreach ($event in $Events) {
            if ($event.ComponentId -ne 0 -or $event.Phase -ne $Phase -or $event.Name -ne $Name) {
                continue
            }

            if ($null -eq $Stage) {
                return $event
            }

            if ($null -ne $event.Data -and $event.Data.stage -eq $Stage) {
                return $event
            }
        }

        return $null
    }

    function Convert-ManagedGeometryEvent {
        param([object]$Event)

        if ($null -eq $Event) {
            return $null
        }

        $result = [ordered]@{}
        if ($null -ne $Event.Iteration) {
            $result.Iteration = [int]$Event.Iteration
        }

        foreach ($name in 'width', 'height', 'averageEdgeLength', 'averageLabelSize') {
            if ($null -ne $Event.Data.$name) {
                $key = switch ($name) {
                    'averageEdgeLength' { 'AverageEdgeLength' }
                    'averageLabelSize' { 'AverageLabelSize' }
                    default { $name.Substring(0, 1).ToUpperInvariant() + $name.Substring(1) }
                }

                $result[$key] = [double]$Event.Data.$name
            }
        }

        return $result
    }

    function Convert-ManagedFinishEvent {
        param([object]$Event)

        if ($null -eq $Event) {
            return $null
        }

        $result = [ordered]@{}
        foreach ($name in 'iterations', 'finishWidth', 'finishHeight', 'finishAverageEdgeLength', 'finishAverageLabelSize') {
            if ($null -eq $Event.Data.$name) {
                continue
            }

            switch ($name) {
                'iterations' { $result.Iterations = [int]$Event.Data.$name }
                'finishWidth' { $result.Width = [double]$Event.Data.$name }
                'finishHeight' { $result.Height = [double]$Event.Data.$name }
                'finishAverageEdgeLength' { $result.AverageEdgeLength = [double]$Event.Data.$name }
                'finishAverageLabelSize' { $result.AverageLabelSize = [double]$Event.Data.$name }
            }
        }

        return $result
    }

    $overlapFinishEvent = Find-ManagedEvent -Events $events -Phase 'overlap' -Name 'finish' -Stage $null

    return [ordered]@{
        BeforeOverlapRemoval = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'postprocess' -Name 'geometry' -Stage 'before_overlap_removal')
        PreScale = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'overlap' -Name 'geometry' -Stage 'pre_scale')
        PostScale = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'overlap' -Name 'geometry' -Stage 'post_scale')
        ModeSwitch = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'overlap' -Name 'geometry' -Stage 'mode_switch')
        Finish = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'overlap' -Name 'geometry' -Stage 'finish')
        FinishEvent = Convert-ManagedFinishEvent $overlapFinishEvent
        AfterOverlapRemoval = Convert-ManagedGeometryEvent (Find-ManagedEvent -Events $events -Phase 'postprocess' -Name 'geometry' -Stage 'after_overlap_removal')
    }
}

function Get-ManagedLayoutGeometry {
    param([string]$Path)

    $events = foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $line | ConvertFrom-Json
    }

    function Find-LayoutEvent {
        param(
            [object[]]$Events,
            [string]$Phase,
            [string]$Name,
            [object]$Stage,
            [object]$ComponentId = $null,
            [switch]$GlobalOnly
        )

        foreach ($event in $Events) {
            if ($event.Phase -ne $Phase -or $event.Name -ne $Name) {
                continue
            }

            if ($GlobalOnly) {
                if ($null -ne $event.ComponentId) {
                    continue
                }
            }
            elseif ($null -ne $ComponentId) {
                if ($event.ComponentId -ne $ComponentId) {
                    continue
                }
            }

            if ($null -ne $Stage -and $Stage -ne '' -and ($null -eq $event.Data -or $event.Data.stage -ne $Stage)) {
                continue
            }

            return $event
        }

        return $null
    }

    function Convert-GeometryEvent {
        param([object]$Event)

        if ($null -eq $Event) {
            return $null
        }

        $result = [ordered]@{}
        foreach ($name in 'minX', 'minY', 'maxX', 'maxY', 'width', 'height', 'diagonal', 'averageEdgeLength') {
            if ($null -ne $Event.Data.$name) {
                $key = switch ($name) {
                    'averageEdgeLength' { 'AverageEdgeLength' }
                    default { $name.Substring(0, 1).ToUpperInvariant() + $name.Substring(1) }
                }

                $result[$key] = [double]$Event.Data.$name
            }
        }

        return $result
    }

    return [ordered]@{
        ComponentReturn = Convert-GeometryEvent (Find-LayoutEvent -Events $events -Phase 'component' -Name 'geometry' -Stage 'layout_component_return' -ComponentId 0)
        AfterPacking = Convert-GeometryEvent (Find-LayoutEvent -Events $events -Phase 'layout' -Name 'geometry' -Stage 'after_packing' -GlobalOnly)
        Finish = Convert-GeometryEvent (($events | Where-Object { $_.Phase -eq 'layout' -and $_.Name -eq 'finish' } | Select-Object -First 1))
    }
}

function Get-ManagedSvgGeometry {
    param([string]$Path)

    $events = foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $line | ConvertFrom-Json
    }

    function Find-SvgEvent {
        param(
            [object[]]$Events,
            [string]$Stage
        )

        foreach ($event in $Events) {
            if ($event.Phase -eq 'svg' -and $event.Name -eq 'geometry' -and $event.Data.stage -eq $Stage) {
                return $event
            }
        }

        return $null
    }

    function Convert-SvgEvent {
        param([object]$Event)

        if ($null -eq $Event) {
            return $null
        }

        $result = [ordered]@{}
        foreach ($name in 'minX', 'minY', 'maxX', 'maxY', 'width', 'height', 'diagonal', 'averageEdgeLength', 'averageLabelSize', 'padding', 'contentMinX', 'contentMinY', 'contentMaxX', 'contentMaxY', 'contentWidth', 'contentHeight', 'outputWidth', 'outputHeight', 'viewBox') {
            if ($null -eq $Event.Data.$name) {
                continue
            }

            $key = switch ($name) {
                'averageEdgeLength' { 'AverageEdgeLength' }
                'averageLabelSize' { 'AverageLabelSize' }
                'contentMinX' { 'ContentMinX' }
                'contentMinY' { 'ContentMinY' }
                'contentMaxX' { 'ContentMaxX' }
                'contentMaxY' { 'ContentMaxY' }
                'contentWidth' { 'ContentWidth' }
                'contentHeight' { 'ContentHeight' }
                'outputWidth' { 'OutputWidth' }
                'outputHeight' { 'OutputHeight' }
                'viewBox' { 'ViewBox' }
                default { $name.Substring(0, 1).ToUpperInvariant() + $name.Substring(1) }
            }

            if ($name -eq 'viewBox') {
                $result[$key] = [string]$Event.Data.$name
            }
            else {
                $result[$key] = [double]$Event.Data.$name
            }
        }

        return $result
    }

    return [ordered]@{
        ExportInput = Convert-SvgEvent (Find-SvgEvent -Events $events -Stage 'export_input')
        ViewBox = Convert-SvgEvent (Find-SvgEvent -Events $events -Stage 'viewbox')
    }
}

function Get-ManagedPackingSummary {
    param([string]$Path)

    $start = $null
    $finish = $null
    $components = New-Object System.Collections.Generic.List[hashtable]

    foreach ($line in Get-Content -Path $Path) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $event = $line | ConvertFrom-Json -AsHashtable
        if ($event.Phase -ne 'packing') {
            continue
        }

        switch ($event.Name) {
            'start' {
                $start = [ordered]@{}
                foreach ($key in $event.Data.Keys) {
                    $start[$key] = $event.Data[$key]
                }
            }

            'component' {
                $component = [ordered]@{
                    ComponentId = $event.ComponentId
                }

                foreach ($key in $event.Data.Keys) {
                    $component[$key] = $event.Data[$key]
                }

                $components.Add($component)
            }

            'finish' {
                $finish = [ordered]@{}
                foreach ($key in $event.Data.Keys) {
                    $finish[$key] = $event.Data[$key]
                }
            }
        }
    }

    return [ordered]@{
        Start = $start
        Components = $components
        Finish = $finish
    }
}

function Transform-SvgCoordinates {
    param(
        [System.Xml.XmlNode]$Node,
        [double]$MinX,
        [double]$MinY,
        [double]$Scale,
        [System.Globalization.CultureInfo]$Culture
    )

    if ($Node -is [System.Xml.XmlElement]) {
        $element = [System.Xml.XmlElement]$Node

        foreach ($attributeName in 'cx', 'x', 'x1', 'x2') {
            if ($element.HasAttribute($attributeName)) {
                $value = [double]::Parse($element.GetAttribute($attributeName), $Culture)
                $element.SetAttribute($attributeName, (($value - $MinX) * $Scale).ToString('0.###', $Culture))
            }
        }

        foreach ($attributeName in 'cy', 'y', 'y1', 'y2') {
            if ($element.HasAttribute($attributeName)) {
                $value = [double]::Parse($element.GetAttribute($attributeName), $Culture)
                $element.SetAttribute($attributeName, ((-($value - $MinY)) * $Scale).ToString('0.###', $Culture))
            }
        }

        foreach ($attributeName in 'width', 'height') {
            if ($element.HasAttribute($attributeName)) {
                $value = [double]::Parse($element.GetAttribute($attributeName), $Culture)
                $element.SetAttribute($attributeName, ($value * $Scale).ToString('0.###', $Culture))
            }
        }

        if ($element.HasAttribute('d')) {
            $element.SetAttribute('d', (Convert-PathDataToGraphvizCoordinates -PathData $element.GetAttribute('d') -MinX $MinX -MinY $MinY -Scale $Scale -Culture $Culture))
        }
    }

    foreach ($child in $Node.ChildNodes) {
        Transform-SvgCoordinates -Node $child -MinX $MinX -MinY $MinY -Scale $Scale -Culture $Culture
    }
}

function Convert-PathDataToGraphvizCoordinates {
    param(
        [string]$PathData,
        [double]$MinX,
        [double]$MinY,
        [double]$Scale,
        [System.Globalization.CultureInfo]$Culture
    )

    $tokens = [System.Text.RegularExpressions.Regex]::Matches($PathData, '[A-Za-z]|-?\d+(?:\.\d+)?')
    $builder = New-Object System.Text.StringBuilder
    $expectX = $true

    foreach ($token in $tokens) {
        $value = $token.Value
        if ($value -match '^[A-Za-z]$') {
            if ($builder.Length -gt 0) {
                [void]$builder.Append(' ')
            }

            [void]$builder.Append($value)
            $expectX = $true
            continue
        }

        $number = [double]::Parse($value, $Culture)
        if ($expectX) {
            $number = ($number - $MinX) * $Scale
        }
        else {
            $number = (-(($number - $MinY))) * $Scale
        }

        [void]$builder.Append(' ')
        [void]$builder.Append($number.ToString('0.###', $Culture))
        $expectX = -not $expectX
    }

    return $builder.ToString().Trim()
}

function Get-LayoutMetrics {
    param(
        $Graph,
        [hashtable]$Coordinates
    )

    $xs = New-Object System.Collections.Generic.List[double]
    $ys = New-Object System.Collections.Generic.List[double]
    foreach ($vertex in $Graph.Vertices) {
        if (-not $Coordinates.ContainsKey($vertex.Label)) {
            continue
        }

        $coordinate = $Coordinates[$vertex.Label]
        $xs.Add($coordinate.X)
        $ys.Add($coordinate.Y)
    }

    $edgeLengths = New-Object System.Collections.Generic.List[double]
    foreach ($edge in $Graph.Edges) {
        $sourceId = $edge.Source.Label
        $targetId = $edge.Target.Label
        if (-not $Coordinates.ContainsKey($sourceId) -or -not $Coordinates.ContainsKey($targetId)) {
            continue
        }

        $source = $Coordinates[$sourceId]
        $target = $Coordinates[$targetId]
        $dx = $source.X - $target.X
        $dy = $source.Y - $target.Y
        $edgeLengths.Add([Math]::Sqrt(($dx * $dx) + ($dy * $dy)))
    }

    $meanEdgeLength = if ($edgeLengths.Count -gt 0) {
        ($edgeLengths | Measure-Object -Average).Average
    }
    else {
        0.0
    }

    $variance = 0.0
    if ($edgeLengths.Count -gt 0) {
        foreach ($length in $edgeLengths) {
            $delta = $length - $meanEdgeLength
            $variance += $delta * $delta
        }

        $variance /= $edgeLengths.Count
    }

    $stdDev = [Math]::Sqrt($variance)
    $width = if ($xs.Count -gt 0) { ($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum } else { 0.0 }
    $height = if ($ys.Count -gt 0) { ($ys | Measure-Object -Maximum).Maximum - ($ys | Measure-Object -Minimum).Minimum } else { 0.0 }
    $diagonal = [Math]::Sqrt(($width * $width) + ($height * $height))

    return [ordered]@{
        NodeCount = $xs.Count
        EdgeCount = $edgeLengths.Count
        Width = [Math]::Round($width, 6)
        Height = [Math]::Round($height, 6)
        Diagonal = [Math]::Round($diagonal, 6)
        MeanEdgeLength = [Math]::Round($meanEdgeLength, 6)
        EdgeLengthStdDev = [Math]::Round($stdDev, 6)
        EdgeLengthCv = if ($meanEdgeLength -gt 0) { [Math]::Round($stdDev / $meanEdgeLength, 6) } else { 0.0 }
        MeanEdgeToDiagonal = if ($diagonal -gt 0) { [Math]::Round($meanEdgeLength / $diagonal, 6) } else { 0.0 }
    }
}

function Get-LargestWeaklyConnectedComponentGraph {
    param($Graph)

    $adjacency = @{}
    foreach ($vertex in $Graph.Vertices) {
        $adjacency[$vertex.Label] = [System.Collections.Generic.HashSet[string]]::new()
    }

    foreach ($edge in $Graph.Edges) {
        [void]$adjacency[$edge.Source.Label].Add($edge.Target.Label)
        [void]$adjacency[$edge.Target.Label].Add($edge.Source.Label)
    }

    $visited = [System.Collections.Generic.HashSet[string]]::new()
    $largestComponent = [System.Collections.Generic.List[string]]::new()

    foreach ($vertex in $Graph.Vertices) {
        if ($visited.Contains($vertex.Label)) {
            continue
        }

        $queue = [System.Collections.Generic.Queue[string]]::new()
        $component = [System.Collections.Generic.List[string]]::new()
        $queue.Enqueue($vertex.Label)
        [void]$visited.Add($vertex.Label)

        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            $component.Add($current)

            foreach ($neighbor in $adjacency[$current]) {
                if ($visited.Add($neighbor)) {
                    $queue.Enqueue($neighbor)
                }
            }
        }

        if ($component.Count -gt $largestComponent.Count) {
            $largestComponent = $component
        }
    }

    $included = [System.Collections.Generic.HashSet[string]]::new($largestComponent)
    $subgraph = New-Graph

    foreach ($label in $largestComponent) {
        Add-Vertex -Graph $subgraph -Vertex $label | Out-Null
    }

    foreach ($edge in $Graph.Edges) {
        if ($included.Contains($edge.Source.Label) -and $included.Contains($edge.Target.Label)) {
            Add-Edge -From $edge.Source.Label -To $edge.Target.Label -Graph $subgraph | Out-Null
        }
    }

    return $subgraph
}

. (Join-Path $PSScriptRoot 'Import-DemoModules.ps1')
Import-PSGraphViewDemoModules `
    -UseLocalModules:$UseLocalModules `
    -PSQuickGraphManifestPath $PSQuickGraphManifestPath `
    -PSGraphViewManifestPath $PSGraphViewManifestPath `
    -Verbose:($VerbosePreference -eq 'Continue')

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$graphToRender = Get-WikiVoteGraph -UseSubgraph:$UseSubgraph -SubgraphSeedCount $SubgraphSeedCount
$mainComponentGraph = Get-LargestWeaklyConnectedComponentGraph -Graph $graphToRender
$graphLabel = if ($UseSubgraph) { "subgraph-$SubgraphSeedCount" } else { 'full' }

$dotPath = Join-Path $OutputDir "wiki-vote-$graphLabel.gv"
$graphvizPlainPath = Join-Path $OutputDir "wiki-vote-$graphLabel-graphviz.plain"
$graphvizSvgPath = Join-Path $OutputDir "wiki-vote-$graphLabel-graphviz.svg"
$graphvizVerbosePath = Join-Path $OutputDir "wiki-vote-$graphLabel-graphviz.verbose.log"
$managedSvgPath = Join-Path $OutputDir "wiki-vote-$graphLabel-managed.svg"
$managedDiagnosticsPath = Join-Path $OutputDir "wiki-vote-$graphLabel-managed.diagnostics.jsonl"
$summaryPath = Join-Path $OutputDir "wiki-vote-$graphLabel-comparison.json"

Write-Host "Exporting DOT: $dotPath"
Export-Graph -Graph $graphToRender -Format Graphviz -Path $dotPath
$graphvizNodeIdMap = Get-DotNodeIdMap -Path $dotPath
Set-DotNodeLabelsEmpty -Path $dotPath
$originalToGraphvizNodeIdMap = @{}
foreach ($entry in $graphvizNodeIdMap.GetEnumerator()) {
    $originalToGraphvizNodeIdMap[$entry.Value] = $entry.Key
}
$graphToRenderManaged = New-GraphWithRemappedIds -Graph $graphToRender -OriginalIdToRemappedId $originalToGraphvizNodeIdMap

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

$graphvizWatch = [System.Diagnostics.Stopwatch]::StartNew()
& $GraphvizSfdpPath '-v' @graphvizArgs '-Tplain' '-o' $graphvizPlainPath $dotPath 2> $graphvizVerbosePath | Out-Null
& $GraphvizSfdpPath @graphvizArgs '-Tsvg' '-o' $graphvizSvgPath $dotPath | Out-Null
$graphvizWatch.Stop()

$graphvizTargetEdgeLength = Get-GraphvizAverageLabelSize -Path $graphvizVerbosePath
$graphvizBaseLabelSize = Get-GraphvizBaseLabelSize -Path $graphvizVerbosePath
$managedOverlapHalfSize = if ($null -ne $graphvizBaseLabelSize) { $graphvizBaseLabelSize / 2.0 } else { $null }
$graphvizMainComponentGeometry = [ordered]@{
    AfterPcpRotate = Get-GraphvizGeometryCheckpoint -Path $graphvizVerbosePath -Kind 'pre' -Stage 'after_pcp_rotate'
    PostScale = Get-GraphvizGeometryCheckpoint -Path $graphvizVerbosePath -Kind 'overlap' -Stage 'post_scale'
    ModeSwitch = Get-GraphvizGeometryCheckpoint -Path $graphvizVerbosePath -Kind 'overlap' -Stage 'mode_switch'
    Finish = Get-GraphvizGeometryCheckpoint -Path $graphvizVerbosePath -Kind 'overlap' -Stage 'finish'
    AfterOverlapRemoval = Get-GraphvizGeometryCheckpoint -Path $graphvizVerbosePath -Kind 'pre' -Stage 'after_overlap_removal'
}
$effectiveManagedOverlapHalfWidth = $null
$effectiveManagedOverlapHalfHeight = $null
if ($UseSuggestedManagedOverlapHalfSize -and $null -ne $managedOverlapHalfSize) {
    $effectiveManagedOverlapHalfWidth = $managedOverlapHalfSize
    $effectiveManagedOverlapHalfHeight = $managedOverlapHalfSize
}

if (-not [double]::IsNaN($ManagedOverlapHalfWidth)) {
    $effectiveManagedOverlapHalfWidth = $ManagedOverlapHalfWidth
}

if (-not [double]::IsNaN($ManagedOverlapHalfHeight)) {
    $effectiveManagedOverlapHalfHeight = $ManagedOverlapHalfHeight
}

if ($null -eq $effectiveManagedOverlapHalfHeight -and $null -ne $effectiveManagedOverlapHalfWidth) {
    $effectiveManagedOverlapHalfHeight = $effectiveManagedOverlapHalfWidth
}

$managedWatch = [System.Diagnostics.Stopwatch]::StartNew()
$managedParameters = @{
    Graph = $graphToRenderManaged
    Renderer = 'Sfdp'
    As = 'Svg'
    Path = $managedSvgPath
    DisableGroupColors = $true
    NodeRadius = 0.72
    EdgeLineWidth = 0.2
    EdgeColor = '#00000018'
    Width = $ImageWidth
    Height = $ImageHeight
    SfdpSeed = $SfdpSeed
    SfdpOverlapRemovalIterations = $SfdpOverlapRemovalIterations
    SfdpDiagnosticsPath = $managedDiagnosticsPath
    SfdpOverlapRemovalPadding = 4
    SfdpOverlapRemovalBoxUnits = 'GraphvizPoints'
}

if ($null -ne $effectiveManagedOverlapHalfWidth) {
    $managedParameters.SfdpOverlapRemovalHalfWidth = $effectiveManagedOverlapHalfWidth
}

if ($null -ne $effectiveManagedOverlapHalfHeight) {
    $managedParameters.SfdpOverlapRemovalHalfHeight = $effectiveManagedOverlapHalfHeight
}

Export-GraphView @managedParameters | Out-Null
Set-ManagedSvgNodeStyleForComparison -Path $managedSvgPath -NodeRadius 0.72
$managedWatch.Stop()
$managedMainComponentGeometry = Get-ManagedMainComponentGeometry -Path $managedDiagnosticsPath
$managedLayoutGeometry = Get-ManagedLayoutGeometry -Path $managedDiagnosticsPath
$managedSvgGeometry = Get-ManagedSvgGeometry -Path $managedDiagnosticsPath
$managedPackingSummary = Get-ManagedPackingSummary -Path $managedDiagnosticsPath

$graphvizCoordinates = Get-GraphvizPlainCoordinates -Path $graphvizPlainPath -NodeIdMap $graphvizNodeIdMap
$managedCoordinates = Get-ManagedSvgCoordinates -Path $managedSvgPath -NodeIdMap $graphvizNodeIdMap

$graphvizMetrics = Get-LayoutMetrics -Graph $graphToRender -Coordinates $graphvizCoordinates
$managedMetrics = Get-LayoutMetrics -Graph $graphToRender -Coordinates $managedCoordinates
$graphvizMainComponentMetrics = Get-LayoutMetrics -Graph $mainComponentGraph -Coordinates $graphvizCoordinates
$managedMainComponentMetrics = Get-LayoutMetrics -Graph $mainComponentGraph -Coordinates $managedCoordinates

Set-ManagedSvgPresentationForComparison -ManagedSvgPath $managedSvgPath -GraphvizSvgPath $graphvizSvgPath

$summary = [ordered]@{
    Graph = [ordered]@{
        Kind = $graphLabel
        VertexCount = $graphToRender.VertexCount
        EdgeCount = $graphToRender.EdgeCount
        MainComponentVertexCount = $mainComponentGraph.VertexCount
        MainComponentEdgeCount = $mainComponentGraph.EdgeCount
    }
    Graphviz = [ordered]@{
        SfdpPath = $GraphvizSfdpPath
        Seed = $SfdpSeed
        OverlapRemovalIterations = $SfdpOverlapRemovalIterations
        BaseLabelSize = $graphvizBaseLabelSize
        TargetEdgeLength = $graphvizTargetEdgeLength
        PlainPath = $graphvizPlainPath
        SvgPath = $graphvizSvgPath
        VerbosePath = $graphvizVerbosePath
        ElapsedMilliseconds = $graphvizWatch.ElapsedMilliseconds
        Metrics = $graphvizMetrics
        MainComponentMetrics = $graphvizMainComponentMetrics
        MainComponentGeometry = $graphvizMainComponentGeometry
    }
    Managed = [ordered]@{
        Seed = $SfdpSeed
        OverlapRemovalIterations = $SfdpOverlapRemovalIterations
        SuggestedOverlapHalfWidth = $managedOverlapHalfSize
        SuggestedOverlapHalfHeight = $managedOverlapHalfSize
        EffectiveOverlapHalfWidth = $effectiveManagedOverlapHalfWidth
        EffectiveOverlapHalfHeight = $effectiveManagedOverlapHalfHeight
        SvgPath = $managedSvgPath
        DiagnosticsPath = $managedDiagnosticsPath
        ElapsedMilliseconds = $managedWatch.ElapsedMilliseconds
        Metrics = $managedMetrics
        MainComponentMetrics = $managedMainComponentMetrics
        MainComponentGeometry = $managedMainComponentGeometry
        LayoutGeometry = $managedLayoutGeometry
        SvgGeometry = $managedSvgGeometry
        Packing = $managedPackingSummary
    }
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath

Write-Host ''
Write-Host '=== Sfdp Comparison Summary ===' -ForegroundColor Cyan
Write-Host "Graphviz time : $($graphvizWatch.ElapsedMilliseconds) ms"
Write-Host "Managed time  : $($managedWatch.ElapsedMilliseconds) ms"
Write-Host "Graphviz mean edge length / diagonal : $($graphvizMetrics.MeanEdgeToDiagonal)"
Write-Host "Managed  mean edge length / diagonal : $($managedMetrics.MeanEdgeToDiagonal)"
Write-Host "Graphviz edge length CV              : $($graphvizMetrics.EdgeLengthCv)"
Write-Host "Managed  edge length CV              : $($managedMetrics.EdgeLengthCv)"
Write-Host "Graphviz main component / diagonal   : $($graphvizMainComponentMetrics.MeanEdgeToDiagonal)"
Write-Host "Managed  main component / diagonal   : $($managedMainComponentMetrics.MeanEdgeToDiagonal)"
Write-Host "Graphviz main component edge CV      : $($graphvizMainComponentMetrics.EdgeLengthCv)"
Write-Host "Managed  main component edge CV      : $($managedMainComponentMetrics.EdgeLengthCv)"
Write-Host ''
Write-Host "DOT       : $dotPath"
Write-Host "Graphviz  : $graphvizSvgPath"
Write-Host "Graphviz verbose : $graphvizVerbosePath"
Write-Host "Managed   : $managedSvgPath"
Write-Host "Managed diagnostics : $managedDiagnosticsPath"
Write-Host "Summary   : $summaryPath"
