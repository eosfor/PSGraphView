using System.Runtime.InteropServices;
using System.Text;

namespace PSGraphView.Graphviz;

public sealed class GraphvizNativeSession : IDisposable
{
    private IntPtr _session;

    public GraphvizNativeSession()
    {
        GraphvizNativeApi.EnsureLoaded();
        _session = GraphvizNativeApi.CreateSession();
        if (_session == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create native Graphviz session.");
        }
    }

    public GraphvizNativeLayoutResult LayoutDot(string dot, GraphvizNativeLayoutRequest? request = null)
    {
        ObjectDisposedException.ThrowIf(_session == IntPtr.Zero, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(dot);

        var nativeRequest = ToNativeRequest(request);
        var nativeResult = default(GraphvizNativeApi.PsgvLayoutResultNative);

        var status = GraphvizNativeApi.LayoutDot(_session, dot, ref nativeRequest, ref nativeResult);
        return FromNativeResult(status, ref nativeResult);
    }

    public GraphvizNativeLayoutResult LayoutDotFile(string path, GraphvizNativeLayoutRequest? request = null)
    {
        ObjectDisposedException.ThrowIf(_session == IntPtr.Zero, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var nativeRequest = ToNativeRequest(request);
        var nativeResult = default(GraphvizNativeApi.PsgvLayoutResultNative);

        var status = GraphvizNativeApi.LayoutDotFile(_session, path, ref nativeRequest, ref nativeResult);
        return FromNativeResult(status, ref nativeResult);
    }

    public void Dispose()
    {
        if (_session == IntPtr.Zero)
        {
            return;
        }

        GraphvizNativeApi.DestroySession(_session);
        _session = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    private static GraphvizNativeApi.PsgvLayoutRequestNative ToNativeRequest(GraphvizNativeLayoutRequest? request)
    {
        var effectiveRequest = request ?? new GraphvizNativeLayoutRequest();
        return new GraphvizNativeApi.PsgvLayoutRequestNative
        {
            Engine = effectiveRequest.Engine,
            IncludeXdot = effectiveRequest.IncludeXdot
        };
    }

    private static GraphvizNativeLayoutResult FromNativeResult(
        GraphvizNativeStatus status,
        ref GraphvizNativeApi.PsgvLayoutResultNative nativeResult)
    {
        try
        {
            var diagnostics = Marshal.PtrToStringUTF8(nativeResult.Diagnostics);
            var xdotJson = ReadUtf8(nativeResult.XdotJsonData, nativeResult.XdotJsonLength);
            var xdot = ReadUtf8(nativeResult.XdotData, nativeResult.XdotLength);

            if (status != GraphvizNativeStatus.Ok || nativeResult.Status != GraphvizNativeStatus.Ok)
            {
                var effectiveStatus = status != GraphvizNativeStatus.Ok ? status : nativeResult.Status;
                throw new GraphvizNativeException(
                    effectiveStatus,
                    GraphvizNativeApi.GetStatusMessage(effectiveStatus),
                    diagnostics);
            }

            if (string.IsNullOrWhiteSpace(xdotJson))
            {
                throw new GraphvizNativeException(
                    GraphvizNativeStatus.InternalError,
                    "Native Graphviz call returned an empty xdot_json payload.",
                    diagnostics);
            }

            return new GraphvizNativeLayoutResult(xdotJson, xdot, diagnostics);
        }
        finally
        {
            GraphvizNativeApi.DisposeLayoutResult(ref nativeResult);
        }
    }

    private static string? ReadUtf8(IntPtr data, nuint length)
    {
        if (data == IntPtr.Zero || length == 0)
        {
            return null;
        }

        var byteCount = checked((int)length);
        var bytes = new byte[byteCount];
        Marshal.Copy(data, bytes, 0, byteCount);
        return Encoding.UTF8.GetString(bytes);
    }
}
