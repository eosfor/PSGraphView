using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Miscellaneous;

namespace PSGraphView.Msagl;

internal static class MsaglLayoutRunner
{
    public static void CalculateLayout(
        GeometryGraph geometryGraph,
        LayoutAlgorithmSettings settings,
        CancelToken? cancelToken = null)
    {
        ArgumentNullException.ThrowIfNull(geometryGraph);
        ArgumentNullException.ThrowIfNull(settings);

        cancelToken?.ThrowIfCanceled();
        LayoutHelpers.CalculateLayout(geometryGraph, settings, cancelToken, null);
        cancelToken?.ThrowIfCanceled();
    }
}
