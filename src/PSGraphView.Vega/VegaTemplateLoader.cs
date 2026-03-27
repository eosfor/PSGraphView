using System.Reflection;
using Newtonsoft.Json.Linq;

namespace PSGraphView.Vega;

internal static class VegaTemplateLoader
{
    public static JObject LoadTemplate(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        // Resolve Assets relative to this assembly's location first (works when
        // loaded as a PowerShell module), then fall back to AppContext.BaseDirectory
        // (works in test runners and self-contained apps).
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var assetPath = assemblyDir is not null
            ? Path.Combine(assemblyDir, "Assets", templateName)
            : null;

        if (assetPath is null || !File.Exists(assetPath))
        {
            assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", templateName);
        }

        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"Template '{templateName}' was not found at '{assetPath}'.");
        }

        return JObject.Parse(File.ReadAllText(assetPath));
    }
}