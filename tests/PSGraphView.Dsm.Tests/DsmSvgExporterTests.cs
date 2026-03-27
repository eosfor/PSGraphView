using PSGraph.DesignStructureMatrix;
using PSGraph.Model;
using PSGraphView.Dsm;
using Svg;

namespace PSGraphView.Dsm.Tests;

public class DsmSvgExporterTests
{
    private readonly DsmSvgExporter _exporter = new();

    [Fact]
    public void BuildDocument_RendersMatrixCellsAndAxisLabels()
    {
        var dsm = CreateDsm();

        var document = _exporter.BuildDocument(
            dsm.RowIndex,
            dsm.ColIndex,
            dsm.DsmMatrixView.RowCount,
            dsm.DsmMatrixView.ColumnCount,
            (row, column) => dsm.DsmMatrixView[row, column]);

        var rectangles = document.Children.OfType<SvgRectangle>().ToList();
        var texts = document.Children.OfType<SvgText>().ToList();

        Assert.Equal(9, rectangles.Count);
        Assert.Equal(6, texts.Count);
    }

    [Fact]
    public void ExportString_WithPartitions_AddsBoundaryRectangles()
    {
        var graph = CreateGraph();
        var dsm = new DsmClassic(graph);
        var part1 = graph.Vertices.Where(vertex => vertex.Label is "A" or "B").ToList();
        var part2 = graph.Vertices.Where(vertex => vertex.Label == "C").ToList();
        var partitions = new List<IReadOnlyList<PSVertex>> { part1, part2 };

        var document = _exporter.BuildDocument(
            dsm.RowIndex,
            dsm.ColIndex,
            dsm.DsmMatrixView.RowCount,
            dsm.DsmMatrixView.ColumnCount,
            (row, column) => dsm.DsmMatrixView[row, column],
            partitions);
        var svg = _exporter.ExportString(
            dsm.RowIndex,
            dsm.ColIndex,
            dsm.DsmMatrixView.RowCount,
            dsm.DsmMatrixView.ColumnCount,
            (row, column) => dsm.DsmMatrixView[row, column],
            partitions);

        var rectangles = document.Children.OfType<SvgRectangle>().ToList();

        Assert.Equal(11, rectangles.Count);
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A", svg, StringComparison.Ordinal);
        Assert.Contains("B", svg, StringComparison.Ordinal);
        Assert.Contains("C", svg, StringComparison.Ordinal);
    }

    private static IDsm CreateDsm()
    {
        return new DsmClassic(CreateGraph());
    }

    private static PsBidirectionalGraph CreateGraph()
    {
        var graph = new PsBidirectionalGraph();
        var a = new PSVertex("A");
        var b = new PSVertex("B");
        var c = new PSVertex("C");

        graph.AddVertex(a);
        graph.AddVertex(b);
        graph.AddVertex(c);
        graph.AddEdge(new PSEdge(a, b));
        graph.AddEdge(new PSEdge(b, a));
        graph.AddEdge(new PSEdge(b, c));
        return graph;
    }
}