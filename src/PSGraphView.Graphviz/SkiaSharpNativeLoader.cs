using System.Reflection;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace PSGraphView.Graphviz;

internal static class SkiaSharpNativeLoader
{
    private static int _resolverRegistered;
    private static readonly object PreloadSync = new();
    private static readonly Dictionary<string, IntPtr> LoadedLibraries = new(StringComparer.OrdinalIgnoreCase);

    public static void EnsureLoaded()
    {
        var assembly = typeof(SKBitmap).Assembly;
        if (Interlocked.Exchange(ref _resolverRegistered, 1) == 0)
        {
            NativeLibrary.SetDllImportResolver(assembly, Resolve);
        }

        foreach (var candidatePath in EnumerateCandidateLibraryPaths(assembly))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            LoadCandidate(candidatePath);
            return;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!IsSkiaLibraryName(libraryName))
        {
            return IntPtr.Zero;
        }

        foreach (var candidatePath in EnumerateCandidateLibraryPaths(assembly))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            return LoadCandidate(candidatePath);
        }

        return IntPtr.Zero;
    }

    private static bool IsSkiaLibraryName(string libraryName)
    {
        return string.Equals(libraryName, "libSkiaSharp", StringComparison.Ordinal) ||
               string.Equals(libraryName, "SkiaSharp", StringComparison.Ordinal) ||
               string.Equals(libraryName, "libSkiaSharp.dll", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(libraryName, "libSkiaSharp.dylib", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(libraryName, "libSkiaSharp.so", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr LoadCandidate(string candidatePath)
    {
        var normalizedPath = Path.GetFullPath(candidatePath);

        lock (PreloadSync)
        {
            if (LoadedLibraries.TryGetValue(normalizedPath, out var existingHandle))
            {
                return existingHandle;
            }

            var handle = NativeLibrary.Load(normalizedPath);
            LoadedLibraries[normalizedPath] = handle;
            return handle;
        }
    }

    private static IEnumerable<string> EnumerateCandidateLibraryPaths(Assembly skiaAssembly)
    {
        foreach (var rootDirectory in EnumerateSearchRoots(skiaAssembly))
        {
            foreach (var fileName in GetCandidateLibraryFileNames())
            {
                yield return Path.Combine(rootDirectory, fileName);
            }

            foreach (var rid in GetCurrentRuntimeIdentifiers())
            {
                var runtimeNativeDirectory = Path.Combine(rootDirectory, "runtimes", rid, "native");
                foreach (var fileName in GetCandidateLibraryFileNames())
                {
                    yield return Path.Combine(runtimeNativeDirectory, fileName);
                    yield return Path.Combine(runtimeNativeDirectory, "lib", fileName);
                    yield return Path.Combine(runtimeNativeDirectory, "bin", fileName);
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSearchRoots(Assembly skiaAssembly)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in new[] { skiaAssembly, typeof(GraphSceneRasterRenderer).Assembly })
        {
            var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                continue;
            }

            var normalizedDirectory = Path.GetFullPath(assemblyDirectory);
            if (seen.Add(normalizedDirectory))
            {
                yield return normalizedDirectory;
            }
        }
    }

    private static IEnumerable<string> GetCandidateLibraryFileNames()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "libSkiaSharp.dll";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "libSkiaSharp.dylib";
            yield break;
        }

        yield return "libSkiaSharp.so";
    }

    private static IEnumerable<string> GetCurrentRuntimeIdentifiers()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        if (architecture is null)
        {
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return $"linux-{architecture}";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "osx";
            yield return $"osx-{architecture}";
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            yield return $"win-{architecture}";
        }
    }
}
