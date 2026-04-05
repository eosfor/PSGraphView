using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PSGraphView.PowerShell;

internal static class GraphvizProcessRenderer
{
    private const string GraphvizDotPathEnvironmentVariable = "PSGRAPHVIEW_GRAPHVIZ_DOT_PATH";

    public static byte[] Render(string dot, GraphvizLayoutEngine engine, ViewOutputKind outputKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dot);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ResolveDotExecutable(),
            Arguments = $"-K{ToCliEngine(engine)} -T{ToCliFormat(outputKind)}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new FileNotFoundException(
                $"Graphviz executable '{processStartInfo.FileName}' was not found. " +
                $"Set {GraphvizDotPathEnvironmentVariable} or install Graphviz.",
                processStartInfo.FileName,
                ex);
        }

        process.StandardInput.Write(dot);
        process.StandardInput.Close();

        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(error) ? "Graphviz render failed." : error.Trim();
            throw new InvalidOperationException(details);
        }

        return output.ToArray();
    }

    private static string ResolveDotExecutable()
    {
        return Environment.GetEnvironmentVariable(GraphvizDotPathEnvironmentVariable) ?? "dot";
    }

    private static string ToCliEngine(GraphvizLayoutEngine engine)
    {
        return engine switch
        {
            GraphvizLayoutEngine.Dot => "dot",
            GraphvizLayoutEngine.Neato => "neato",
            GraphvizLayoutEngine.Fdp => "fdp",
            GraphvizLayoutEngine.Sfdp => "sfdp",
            GraphvizLayoutEngine.Twopi => "twopi",
            GraphvizLayoutEngine.Circo => "circo",
            _ => throw new NotSupportedException($"Graphviz engine '{engine}' is not supported.")
        };
    }

    private static string ToCliFormat(ViewOutputKind outputKind)
    {
        return outputKind switch
        {
            ViewOutputKind.Svg => "svg",
            ViewOutputKind.Png => "png",
            ViewOutputKind.Jpg => "jpg",
            _ => throw new NotSupportedException(
                $"Output kind '{outputKind}' is not supported by Graphviz process rendering.")
        };
    }
}
