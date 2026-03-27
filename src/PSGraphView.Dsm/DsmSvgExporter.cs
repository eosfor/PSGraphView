using System.Drawing;
using System.Text;
using PSGraph.Model;
using Svg;

namespace PSGraphView.Dsm;

public sealed class DsmSvgExporter
{
    public SvgDocument BuildDocument(
        IReadOnlyDictionary<PSVertex, int> rowIndex,
        IReadOnlyDictionary<PSVertex, int> colIndex,
        int rowCount,
        int columnCount,
        Func<int, int, double> getMatrixValue,
        IReadOnlyList<IReadOnlyList<PSVertex>>? partitions = null,
        int itemSize = 45)
    {
        ArgumentNullException.ThrowIfNull(rowIndex);
        ArgumentNullException.ThrowIfNull(colIndex);
        ArgumentNullException.ThrowIfNull(getMatrixValue);

        var height = columnCount * itemSize + itemSize;
        var width = rowCount * itemSize + itemSize;

        var svgDocument = new SvgDocument
        {
            Width = width,
            Height = height
        };

        GenerateMatrixViewAnnotations(itemSize, rowIndex, colIndex, svgDocument);
        GenerateMatrixView(itemSize, rowCount, columnCount, getMatrixValue, svgDocument);
        if (partitions is not null)
        {
            GeneratePartitionBoundaries(itemSize, rowIndex, partitions, svgDocument);
        }

        return svgDocument;
    }

    public string ExportString(
        IReadOnlyDictionary<PSVertex, int> rowIndex,
        IReadOnlyDictionary<PSVertex, int> colIndex,
        int rowCount,
        int columnCount,
        Func<int, int, double> getMatrixValue,
        IReadOnlyList<IReadOnlyList<PSVertex>>? partitions = null,
        int itemSize = 45)
    {
        var svgDocument = BuildDocument(rowIndex, colIndex, rowCount, columnCount, getMatrixValue, partitions, itemSize);

        using var stream = new MemoryStream();
        svgDocument.Write(stream, useBom: false);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void GeneratePartitionBoundaries(
        int itemSize,
        IReadOnlyDictionary<PSVertex, int> rowIndex,
        IReadOnlyList<IReadOnlyList<PSVertex>> partitions,
        SvgDocument svgDocument)
    {
        foreach (var partition in partitions)
        {
            if (partition.Count == 0)
            {
                continue;
            }

            var width = partition.Count * itemSize;
            var height = partition.Count * itemSize;
            var row = rowIndex[partition[0]] * itemSize + itemSize;

            var rectangle = new SvgRectangle
            {
                Width = width,
                Height = height,
                X = row,
                Y = row,
                StrokeWidth = (float)2.0,
                Stroke = new SvgColourServer(Color.Black),
                FillOpacity = 0
            };

            svgDocument.Children.Add(rectangle);
        }
    }

    private static void GenerateMatrixViewAnnotations(
        int itemSize,
        IReadOnlyDictionary<PSVertex, int> rowIndex,
        IReadOnlyDictionary<PSVertex, int> colIndex,
        SvgDocument svgDocument)
    {
        var y = 0;
        foreach (var item in rowIndex.OrderBy(kvp => kvp.Value))
        {
            var element = new SvgText(item.Key.ToString());
            var x = itemSize - 15;
            element.X.Add(x);
            element.Y.Add((y + 1) * itemSize + itemSize / 2);
            svgDocument.Children.Add(element);
            y++;
        }

        var xIndex = 0;
        foreach (var item in colIndex.OrderBy(kvp => kvp.Value))
        {
            var element = new SvgText(item.Key.ToString());
            var yPosition = itemSize - (int)element.FontSize - 5;
            element.X.Add((xIndex + 1) * itemSize + itemSize / 2);
            element.Y.Add(yPosition);
            svgDocument.Children.Add(element);
            xIndex++;
        }
    }

    private static void GenerateMatrixView(
        int itemSize,
        int rowCount,
        int columnCount,
        Func<int, int, double> getMatrixValue,
        SvgDocument svgDocument)
    {
        for (var column = 0; column < columnCount; column++)
        {
            var x = column * itemSize + itemSize;
            for (var row = 0; row < rowCount; row++)
            {
                var y = row * itemSize + itemSize;
                var rectangle = new SvgRectangle
                {
                    Width = itemSize,
                    Height = itemSize,
                    X = x,
                    Y = y,
                    StrokeWidth = (float)0.5,
                    Stroke = new SvgColourServer(Color.DimGray),
                    Fill = ResolveFillColor(column, row, getMatrixValue)
                };

                svgDocument.Children.Add(rectangle);
            }
        }
    }

    private static SvgColourServer ResolveFillColor(int column, int row, Func<int, int, double> getMatrixValue)
    {
        if (column == row)
        {
            return new SvgColourServer(Color.DarkGray);
        }

        return getMatrixValue(row, column) == 1
            ? new SvgColourServer(Color.SlateBlue)
            : new SvgColourServer(Color.White);
    }
}