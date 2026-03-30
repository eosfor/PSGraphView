---
external help file: PSGraphView.PowerShell.dll-Help.xml
Module Name: PSGraphView
online version:
schema: 2.0.0
---

# Export-DSMView

## SYNOPSIS
Exports a DSM view with one of the PSGraphView DSM renderers.

## SYNTAX

### PlainDsm (Default)
```
Export-DSMView [-Dsm] <IDsm> -Renderer <DsmViewRenderer> [-As <ViewOutputKind>] [-Path <String>]
 [-ItemSize <Int32>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### PartitionedDsm
```
Export-DSMView [-Result] <IDsmPartitionResult> -Renderer <DsmViewRenderer> [-As <ViewOutputKind>]
 [-Path <String>] [-ItemSize <Int32>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### SequencedDsm
```
Export-DSMView [-SequencedDsm] <IDsm> -Renderer <DsmViewRenderer> [-As <ViewOutputKind>] [-Path <String>]
 [-ItemSize <Int32>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to render a plain, partitioned, or sequenced DSM through the SVG or Vega DSM renderers.

## EXAMPLES

### Example 1
```powershell
PS C:\> Export-DSMView -Dsm $dsm -Renderer DsmMatrixSvg -As Svg -Path ./matrix.svg
```

Exports a plain DSM to an SVG matrix.

## PARAMETERS

### -As
Selects the output format for the DSM renderer.

```yaml
Type: ViewOutputKind
Parameter Sets: (All)
Aliases:
Accepted values: Json, Html, Svg

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Dsm
Specifies a plain DSM instance to export.

```yaml
Type: IDsm
Parameter Sets: PlainDsm
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ItemSize
Sets the rendered item size used by DSM renderers.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Writes the rendered output to the specified file path.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Renderer
Selects the DSM renderer to use.

```yaml
Type: DsmViewRenderer
Parameter Sets: (All)
Aliases:
Accepted values: DsmMatrixSvg, DsmVegaMatrix

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Result
Specifies a clustered or partitioned DSM result to export.

```yaml
Type: IDsmPartitionResult
Parameter Sets: PartitionedDsm
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SequencedDsm
Specifies a sequenced DSM to export.

```yaml
Type: IDsm
Parameter Sets: SequencedDsm
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
Controls how PowerShell progress records are handled.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
