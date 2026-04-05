using System.Management.Automation;
using PSGraph;

namespace PSGraphView.PowerShell;

internal static class CmdletOutputHelpers
{
    public static ViewOutputKind ResolveOutputKind(
        IReadOnlyDictionary<string, object?> boundParameters,
        string asParameterName,
        ViewOutputKind asValue,
        string? path,
        ViewOutputKind defaultOutputKind)
    {
        if (boundParameters.ContainsKey(asParameterName))
        {
            return asValue;
        }

        if (TryResolveFromPath(path, out var inferredOutputKind))
        {
            return inferredOutputKind;
        }

        return defaultOutputKind;
    }

    public static void ValidateSupportedOutputs(
        PSCmdlet cmdlet,
        string renderer,
        ViewOutputKind actual,
        params ViewOutputKind[] supported)
    {
        if (supported.Contains(actual))
        {
            return;
        }

        var supportedText = string.Join(", ", supported.Select(static item => item.ToString()));
        cmdlet.ThrowTerminatingError(new ErrorRecord(
            new NotSupportedException($"Renderer '{renderer}' supports only: {supportedText}. Requested: {actual}."),
            "PSGraphView.UnsupportedOutputKind",
            ErrorCategory.InvalidArgument,
            actual));
    }

    public static VegaExportTypes ToVegaExportType(ViewOutputKind outputKind)
    {
        return outputKind switch
        {
            ViewOutputKind.Json => VegaExportTypes.JSON,
            ViewOutputKind.Html => VegaExportTypes.HTML,
            _ => throw new NotSupportedException($"Output kind '{outputKind}' is not supported by Vega exporters.")
        };
    }

    public static void WriteResult(PSCmdlet cmdlet, string result, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            cmdlet.WriteObject(result);
            return;
        }

        File.WriteAllText(path, result);
    }

    public static void WriteResult(PSCmdlet cmdlet, byte[] result, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            cmdlet.WriteObject(result);
            return;
        }

        File.WriteAllBytes(path, result);
    }

    private static bool TryResolveFromPath(string? path, out ViewOutputKind outputKind)
    {
        outputKind = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(path);
        outputKind = extension.ToLowerInvariant() switch
        {
            ".json" => ViewOutputKind.Json,
            ".html" => ViewOutputKind.Html,
            ".htm" => ViewOutputKind.Html,
            ".svg" => ViewOutputKind.Svg,
            ".png" => ViewOutputKind.Png,
            ".jpg" => ViewOutputKind.Jpg,
            ".jpeg" => ViewOutputKind.Jpg,
            _ => default
        };

        return extension is ".json" or ".html" or ".htm" or ".svg" or ".png" or ".jpg" or ".jpeg";
    }
}
