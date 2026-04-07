using System.Reflection;
using System.Runtime.InteropServices;

namespace PSGraphView.Graphviz;

internal static class GraphvizNativeApi
{
    private const string NativeLibraryName = "psgv";
    private const string LibraryPathEnvironmentVariable = "PSGRAPHVIEW_PSGV_LIBRARY_PATH";
    private static int _resolverInitialized;
    private static readonly object PreloadSync = new();
    private static readonly HashSet<string> PreloadedLibraryPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<IntPtr> PreloadedLibraryHandles = [];

    static GraphvizNativeApi()
    {
        EnsureResolverRegistered();
    }

    public static void EnsureLoaded()
    {
        EnsureResolverRegistered();
    }

    [DllImport(NativeLibraryName, EntryPoint = "psgv_session_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr CreateSession();

    [DllImport(NativeLibraryName, EntryPoint = "psgv_session_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DestroySession(IntPtr session);

    [DllImport(NativeLibraryName, EntryPoint = "psgv_layout_dot", CallingConvention = CallingConvention.Cdecl)]
    internal static extern GraphvizNativeStatus LayoutDot(
        IntPtr session,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dot,
        ref PsgvLayoutRequestNative request,
        ref PsgvLayoutResultNative result);

    [DllImport(NativeLibraryName, EntryPoint = "psgv_layout_dot_file", CallingConvention = CallingConvention.Cdecl)]
    internal static extern GraphvizNativeStatus LayoutDotFile(
        IntPtr session,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        ref PsgvLayoutRequestNative request,
        ref PsgvLayoutResultNative result);

    [DllImport(NativeLibraryName, EntryPoint = "psgv_layout_result_dispose", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DisposeLayoutResult(ref PsgvLayoutResultNative result);

    [DllImport(NativeLibraryName, EntryPoint = "psgv_status_message", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr StatusMessageNative(GraphvizNativeStatus status);

    internal static string GetStatusMessage(GraphvizNativeStatus status)
    {
        var pointer = StatusMessageNative(status);
        return Marshal.PtrToStringAnsi(pointer) ?? status.ToString();
    }

    private static void EnsureResolverRegistered()
    {
        if (Interlocked.Exchange(ref _resolverInitialized, 1) != 0)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(GraphvizNativeApi).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, NativeLibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        var explicitPath = Environment.GetEnvironmentVariable(LibraryPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            PreloadBundledLibraries(explicitPath);
            return NativeLibrary.Load(explicitPath);
        }

        foreach (var candidatePath in EnumerateBundledLibraryPaths(assembly))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            PreloadBundledLibraries(candidatePath);
            if (NativeLibrary.TryLoad(candidatePath, out var bundledHandle))
            {
                return bundledHandle;
            }
        }

        if (NativeLibrary.TryLoad("libpsgv.dylib", assembly, searchPath, out var macHandle))
        {
            return macHandle;
        }

        if (NativeLibrary.TryLoad("libpsgv.so", assembly, searchPath, out var linuxHandle))
        {
            return linuxHandle;
        }

        if (NativeLibrary.TryLoad("psgv.dll", assembly, searchPath, out var windowsHandle))
        {
            return windowsHandle;
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateBundledLibraryPaths(Assembly assembly)
    {
        var assemblyLocation = assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyLocation))
        {
            yield break;
        }

        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield break;
        }

        foreach (var fileName in GetCandidateLibraryFileNames())
        {
            yield return Path.Combine(assemblyDirectory, fileName);
        }

        foreach (var rid in GetCurrentRuntimeIdentifiers())
        {
            var nativeRoot = Path.Combine(assemblyDirectory, "runtimes", rid, "native");
            foreach (var fileName in GetCandidateLibraryFileNames())
            {
                yield return Path.Combine(nativeRoot, fileName);
                yield return Path.Combine(nativeRoot, "lib", fileName);
                yield return Path.Combine(nativeRoot, "bin", fileName);
            }
        }
    }

    private static IEnumerable<string> GetCandidateLibraryFileNames()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return "libpsgv.dll";
            yield return "psgv.dll";
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "libpsgv.dylib";
            yield return "libpsgv.0.dylib";
            yield break;
        }

        yield return "libpsgv.so";
        yield return "libpsgv.so.0";
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
            yield return $"osx-{architecture}";
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            yield return $"win-{architecture}";
        }
    }

    private static void PreloadBundledLibraries(string mainLibraryPath)
    {
        var nativeRoot = ResolveNativeRoot(mainLibraryPath);
        if (nativeRoot is null)
        {
            return;
        }

        ConfigureGraphvizEnvironment(nativeRoot);

        foreach (var libraryPath in EnumerateDependencyLibraryPaths(nativeRoot, mainLibraryPath))
        {
            lock (PreloadSync)
            {
                if (!PreloadedLibraryPaths.Add(libraryPath))
                {
                    continue;
                }

                var handle = NativeLibrary.Load(libraryPath);
                PreloadedLibraryHandles.Add(handle);
            }
        }
    }

    private static string? ResolveNativeRoot(string mainLibraryPath)
    {
        var libraryDirectory = Path.GetDirectoryName(mainLibraryPath);
        if (string.IsNullOrWhiteSpace(libraryDirectory))
        {
            return null;
        }

        var leafDirectoryName = Path.GetFileName(libraryDirectory);
        if (string.Equals(leafDirectoryName, "lib", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(leafDirectoryName, "bin", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(libraryDirectory)?.FullName;
        }

        return libraryDirectory;
    }

    private static void ConfigureGraphvizEnvironment(string nativeRoot)
    {
        var pluginDirectory = Path.Combine(nativeRoot, "lib", "graphviz");
        if (Directory.Exists(pluginDirectory) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GVBINDIR")))
        {
            Environment.SetEnvironmentVariable("GVBINDIR", pluginDirectory);
        }
    }

    private static IEnumerable<string> EnumerateDependencyLibraryPaths(string nativeRoot, string mainLibraryPath)
    {
        var searchDirectories = new[]
        {
            Path.Combine(nativeRoot, "lib"),
            Path.Combine(nativeRoot, "bin")
        };

        foreach (var directory in searchDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var patterns = OperatingSystem.IsWindows()
                ? new[] { "*.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "*.dylib" }
                    : new[] { "*.so", "*.so.*" };

            var libraryPaths = patterns
                .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern));

            foreach (var libraryPath in OrderDependencyLibraryPaths(libraryPaths, mainLibraryPath))
            {
                yield return libraryPath;
            }
        }
    }

    private static IEnumerable<string> OrderDependencyLibraryPaths(
        IEnumerable<string> libraryPaths,
        string mainLibraryPath)
    {
        var mainLibraryFullPath = Path.GetFullPath(mainLibraryPath);

        return libraryPaths
            .Select(Path.GetFullPath)
            .Where(path => !string.Equals(path, mainLibraryFullPath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetLibraryPreloadRank)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetLibraryPreloadRank(string libraryPath)
    {
        var fileName = Path.GetFileName(libraryPath);
        if (fileName.Contains("ltdl", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.Contains("cdt", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (fileName.Contains("cgraph", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (fileName.Contains("pathplan", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (fileName.Contains("xdot", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (fileName.Contains("gvc", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (fileName.Contains("gvpr", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (fileName.Contains("gvplugin", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        return 50;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PsgvLayoutRequestNative
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public string? Engine;

        [MarshalAs(UnmanagedType.I1)]
        public bool IncludeXdot;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PsgvLayoutResultNative
    {
        public GraphvizNativeStatus Status;
        public IntPtr XdotJsonData;
        public nuint XdotJsonLength;
        public IntPtr XdotData;
        public nuint XdotLength;
        public IntPtr Diagnostics;
    }
}
