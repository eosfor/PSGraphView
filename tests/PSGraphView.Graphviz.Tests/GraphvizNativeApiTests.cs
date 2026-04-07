using System.Reflection;

namespace PSGraphView.Graphviz.Tests;

public sealed class GraphvizNativeApiTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"psgraphview-native-api-{Guid.NewGuid():N}");

    public GraphvizNativeApiTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OrderDependencyLibraryPaths_LoadsRegularLibrariesBeforePlugins()
    {
        var libDirectory = Path.Combine(_tempDirectory, "lib");
        Directory.CreateDirectory(libDirectory);

        var mainLibraryPath = CreateEmptyFile(Path.Combine(libDirectory, "libpsgv.so"));
        var gvcLibraryPath = CreateEmptyFile(Path.Combine(libDirectory, "libgvc.so.7"));
        var gtsLibraryPath = CreateEmptyFile(Path.Combine(libDirectory, "libgts-0.7.so.5"));
        var dotPluginPath = CreateEmptyFile(Path.Combine(libDirectory, "libgvplugin_dot_layout.so"));
        var neatoPluginPath = CreateEmptyFile(Path.Combine(libDirectory, "libgvplugin_neato_layout.so"));

        var orderedPaths = InvokeOrderDependencyLibraryPaths(
            new[]
            {
                dotPluginPath,
                gtsLibraryPath,
                mainLibraryPath,
                neatoPluginPath,
                gvcLibraryPath
            },
            mainLibraryPath);

        Assert.DoesNotContain(mainLibraryPath, orderedPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(gvcLibraryPath, orderedPaths[0]);
        Assert.True(
            Array.IndexOf(orderedPaths, gtsLibraryPath) < Array.IndexOf(orderedPaths, dotPluginPath),
            "Regular dependency libraries must be preloaded before Graphviz plugins.");
        Assert.True(
            Array.IndexOf(orderedPaths, gtsLibraryPath) < Array.IndexOf(orderedPaths, neatoPluginPath),
            "Versioned shared libraries must be preloaded before plugin entry points.");
    }

    private static string[] InvokeOrderDependencyLibraryPaths(IEnumerable<string> libraryPaths, string mainLibraryPath)
    {
        var graphvizAssembly = typeof(GraphvizNativeSession).Assembly;
        var nativeApiType = graphvizAssembly.GetType("PSGraphView.Graphviz.GraphvizNativeApi", throwOnError: true)
            ?? throw new InvalidOperationException("GraphvizNativeApi type was not found.");
        var orderMethod = nativeApiType.GetMethod(
            "OrderDependencyLibraryPaths",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OrderDependencyLibraryPaths method was not found.");

        var orderedPaths = (IEnumerable<string>?)orderMethod.Invoke(null, new object[] { libraryPaths, mainLibraryPath })
            ?? throw new InvalidOperationException("OrderDependencyLibraryPaths returned null.");

        return orderedPaths.ToArray();
    }

    private static string CreateEmptyFile(string path)
    {
        File.WriteAllText(path, string.Empty);
        return Path.GetFullPath(path);
    }
}
