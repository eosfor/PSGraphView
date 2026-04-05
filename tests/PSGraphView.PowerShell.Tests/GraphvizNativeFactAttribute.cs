namespace PSGraphView.PowerShell.Tests;

public sealed class GraphvizNativeFactAttribute : FactAttribute
{
    public GraphvizNativeFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PSGRAPHVIEW_PSGV_LIBRARY_PATH")))
        {
            return;
        }

        Skip = "Set PSGRAPHVIEW_PSGV_LIBRARY_PATH to run native Graphviz cmdlet tests.";
    }
}
