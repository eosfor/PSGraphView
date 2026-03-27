using Newtonsoft.Json.Linq;

namespace PSGraphView.Vega;

internal static class VegaTemplateLoader
{
    public static JObject LoadTemplate(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", templateName);
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"Template '{templateName}' was not found at '{assetPath}'.");
        }

        return JObject.Parse(File.ReadAllText(assetPath));
    }
}