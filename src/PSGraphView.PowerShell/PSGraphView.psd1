@{
    RootModule = 'PSGraphView.PowerShell.dll'
    ModuleVersion = '0.1.0'
    GUID = '0ef8d550-6c15-4e60-9e6d-74c06fb149c9'
    Author = 'PSGraphView'
    CompanyName = 'PSGraphView'
    Copyright = '(c) PSGraphView'
    Description = 'Visualization-oriented PowerShell cmdlets for PSGraphView.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')
    CmdletsToExport = @('Export-GraphView', 'Export-DSMView')
    FunctionsToExport = @()
    AliasesToExport = @()
    VariablesToExport = '*'
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta1'
            Tags = @('Graph', 'Visualization', 'DSM', 'Vega', 'MSAGL', 'PSGraph')
            ProjectUri = 'https://github.com/eosfor/PSGraphView'
            ReleaseNotes = 'First beta release of the extracted PSGraphView PowerShell module.'
        }
    }
}