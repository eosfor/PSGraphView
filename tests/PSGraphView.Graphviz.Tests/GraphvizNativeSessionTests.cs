using System.Diagnostics;

namespace PSGraphView.Graphviz.Tests;

[Collection(GraphvizNativeCollection.Name)]
public sealed class GraphvizNativeSessionTests
{
    private const string BasicDot = "digraph G { graph [rankdir=LR]; A -> B [label=\"edge\"]; }";
    private static readonly object NativeLibrarySync = new();

    [GraphvizNativeFact]
    public void LayoutDot_ReturnsXdotJson()
    {
        EnsureNativeLibraryAvailable();

        using var session = new GraphvizNativeSession();

        var result = session.LayoutDot(BasicDot, new GraphvizNativeLayoutRequest("dot", IncludeXdot: true));

        Assert.Contains("\"name\": \"G\"", result.XdotJson, StringComparison.Ordinal);
        Assert.Contains("\"directed\": true", result.XdotJson, StringComparison.Ordinal);
        Assert.Contains("\"label\": \"edge\"", result.XdotJson, StringComparison.Ordinal);
        Assert.NotNull(result.Xdot);
        Assert.Contains("_draw_", result.Xdot, StringComparison.Ordinal);
    }

    [GraphvizNativeFact]
    public void LayoutDotFile_ReturnsXdotJson()
    {
        EnsureNativeLibraryAvailable();

        var dotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dot");
        File.WriteAllText(dotPath, BasicDot);

        try
        {
            using var session = new GraphvizNativeSession();

            var result = session.LayoutDotFile(dotPath, new GraphvizNativeLayoutRequest("dot"));

            Assert.Contains("\"edges\"", result.XdotJson, StringComparison.Ordinal);
            Assert.Contains("\"rankdir\": \"LR\"", result.XdotJson, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dotPath);
        }
    }

    [GraphvizNativeFact]
    public void LayoutDot_ThrowsForInvalidDot()
    {
        EnsureNativeLibraryAvailable();

        using var session = new GraphvizNativeSession();

        var ex = Assert.Throws<GraphvizNativeException>(
            () => session.LayoutDot("digraph G { A ->", new GraphvizNativeLayoutRequest("dot")));

        Assert.Equal(GraphvizNativeStatus.GraphError, ex.Status);
    }

    internal static void EnsureNativeLibraryAvailable()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PSGRAPHVIEW_PSGV_LIBRARY_PATH")))
        {
            return;
        }

        lock (NativeLibrarySync)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PSGRAPHVIEW_PSGV_LIBRARY_PATH")))
            {
                return;
            }

            var sourceDirectory = ResolveGraphvizSourceDirectory();
            if (sourceDirectory is null)
            {
                throw new InvalidOperationException("Graphviz source directory was not found for building libpsgv.dylib.");
            }

            var libraryPath = BuildNativeLibrary(sourceDirectory);
            Environment.SetEnvironmentVariable("PSGRAPHVIEW_PSGV_LIBRARY_PATH", libraryPath);
        }
    }

    private static string? ResolveGraphvizSourceDirectory()
    {
        var explicitPath = Environment.GetEnvironmentVariable("PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR");
        if (!string.IsNullOrWhiteSpace(explicitPath) &&
            File.Exists(Path.Combine(explicitPath, "lib", "psgv", "psgv.c")))
        {
            return explicitPath;
        }

        var repositoryRoot = FindRepositoryRoot();
        var siblingGraphviz = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "graphviz"));
        if (File.Exists(Path.Combine(siblingGraphviz, "lib", "psgv", "psgv.c")))
        {
            return siblingGraphviz;
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "PSGraphView.PowerShell")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PSGraphView repository root was not found.");
    }

    private static string BuildNativeLibrary(string graphvizSourceDirectory)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "psgraphview-graphviz-native");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "libpsgv.dylib");

        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var graphvizPrefix = RunProcess("brew", "--prefix graphviz").Trim();
        if (string.IsNullOrWhiteSpace(graphvizPrefix))
        {
            throw new InvalidOperationException("Homebrew graphviz installation was not found.");
        }

        var temporaryOutputPath = Path.Combine(outputDirectory, $"libpsgv.{Guid.NewGuid():N}.dylib");

        try
        {
            var arguments = string.Join(' ', new[]
            {
                "-dynamiclib",
                "-std=c17",
                "-fPIC",
                "-o", Quote(temporaryOutputPath),
                Quote(Path.Combine(graphvizSourceDirectory, "lib", "psgv", "psgv.c")),
                BuildIncludeArguments(graphvizSourceDirectory, graphvizPrefix),
                "-L" + Quote(Path.Combine(graphvizPrefix, "lib")),
                "-lgvc",
                "-lcgraph",
                "-lcdt",
                "-lpathplan",
                "-lxdot",
                "-Wl,-rpath," + Quote(Path.Combine(graphvizPrefix, "lib"))
            });

            RunProcess("clang", arguments);

            if (!File.Exists(outputPath))
            {
                try
                {
                    File.Move(temporaryOutputPath, outputPath);
                    temporaryOutputPath = outputPath;
                }
                catch (IOException) when (File.Exists(outputPath))
                {
                }
            }

            return outputPath;
        }
        finally
        {
            if (!string.Equals(temporaryOutputPath, outputPath, StringComparison.Ordinal) &&
                File.Exists(temporaryOutputPath))
            {
                File.Delete(temporaryOutputPath);
            }
        }
    }

    private static string BuildIncludeArguments(string graphvizSourceDirectory, string graphvizPrefix)
    {
        var includeDirectories = new[]
        {
            Path.Combine(graphvizSourceDirectory, "lib"),
            Path.Combine(graphvizSourceDirectory, "lib", "psgv"),
            Path.Combine(graphvizSourceDirectory, "lib", "common"),
            Path.Combine(graphvizSourceDirectory, "lib", "cdt"),
            Path.Combine(graphvizSourceDirectory, "lib", "cgraph"),
            Path.Combine(graphvizSourceDirectory, "lib", "gvc"),
            Path.Combine(graphvizSourceDirectory, "lib", "pathplan"),
            Path.Combine(graphvizSourceDirectory, "lib", "xdot"),
            Path.Combine(graphvizPrefix, "include"),
            Path.Combine(graphvizPrefix, "include", "graphviz")
        };

        return string.Join(' ',
            includeDirectories
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => "-I" + Quote(path)));
    }

    private static string RunProcess(string fileName, string arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process '{fileName} {arguments}' failed with exit code {process.ExitCode}. {error}".Trim());
        }

        return output;
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}

public sealed class GraphvizNativeFactAttribute : FactAttribute
{
    public GraphvizNativeFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PSGRAPHVIEW_PSGV_LIBRARY_PATH")))
        {
            return;
        }

        if (!OperatingSystem.IsMacOS())
        {
            Skip = "Set PSGRAPHVIEW_PSGV_LIBRARY_PATH to run native Graphviz tests on this OS.";
            return;
        }

        var explicitSourceDirectory = Environment.GetEnvironmentVariable("PSGRAPHVIEW_GRAPHVIZ_SOURCE_DIR");
        if (!string.IsNullOrWhiteSpace(explicitSourceDirectory) &&
            File.Exists(Path.Combine(explicitSourceDirectory, "lib", "psgv", "psgv.c")))
        {
            return;
        }

        var repositoryRoot = FindRepositoryRoot();
        var siblingGraphviz = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "graphviz"));
        if (File.Exists(Path.Combine(siblingGraphviz, "lib", "psgv", "psgv.c")))
        {
            return;
        }

        Skip = "Graphviz source directory was not found for building libpsgv.dylib.";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "PSGraphView.PowerShell")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PSGraphView repository root was not found.");
    }
}
