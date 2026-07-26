@{
    RootModule = 'PSGraphView.PowerShell.dll'
    ModuleVersion = '0.2.0'
    GUID = '0ef8d550-6c15-4e60-9e6d-74c06fb149c9'
    Author = 'PSGraphView'
    CompanyName = 'PSGraphView'
    Copyright = '(c) PSGraphView'
    Description = 'Visualization-oriented PowerShell cmdlets for PSGraphView.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')
    CmdletsToExport = @('Export-GraphView', 'Export-GraphvizView', 'Export-DSMView')
    FunctionsToExport = @()
    AliasesToExport = @()
    VariablesToExport = '*'
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta1'
            Tags = @('Graph', 'Visualization', 'DSM', 'Vega', 'MSAGL', 'PSGraph')
            ProjectUri = 'https://github.com/eosfor/PSGraphView'
            ReleaseNotes = 'Adds .NET 8 and PowerShell 7.4-7.6 compatibility without bundling the PowerShell runtime.'
        }
    }
}
