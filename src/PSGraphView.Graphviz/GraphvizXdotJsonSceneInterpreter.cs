using System.Globalization;
using System.Text.Json;

namespace PSGraphView.Graphviz;

public sealed class GraphvizXdotJsonSceneInterpreter
{
    private static readonly string[] GraphDrawAttributeNames = ["_draw_", "_ldraw_"];
    private static readonly string[] NodeAndSubgraphDrawAttributeNames = ["_draw_", "_ldraw_"];
    private static readonly string[] EdgeDrawAttributeNames = ["_draw_", "_hdraw_", "_tdraw_", "_ldraw_", "_hldraw_", "_tldraw_"];

    public GraphScene Interpret(string xdotJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xdotJson);

        using var document = JsonDocument.Parse(xdotJson);
        var root = document.RootElement;
        EnsureObject(root, "The xdot_json payload root must be a JSON object.");

        var name = GetRequiredString(root, "name");
        var subgraphCount = GetOptionalInt32(root, "_subgraph_cnt") ?? 0;
        var bounds = ParseBounds(root);
        var commands = ParseDrawCommands(root, GraphDrawAttributeNames);
        var rawObjects = ReadElementArray(root, "objects");
        var rawEdges = ReadElementArray(root, "edges");
        var objects = ParseSceneObjects(rawObjects, rawEdges, subgraphCount);

        return new GraphScene(name, bounds, commands, objects);
    }

    private static IReadOnlyList<SceneObject> ParseSceneObjects(
        IReadOnlyList<JsonElement> rawObjects,
        IReadOnlyList<JsonElement> rawEdges,
        int subgraphCount)
    {
        if (subgraphCount > rawObjects.Count)
        {
            throw new InvalidDataException("The '_subgraph_cnt' value exceeds the number of objects.");
        }

        var state = new DocumentTraversalState(rawObjects, rawEdges, subgraphCount);

        for (var index = 0; index < subgraphCount; index++)
        {
            VisitSubgraph(index, state);
        }

        for (var index = subgraphCount; index < rawObjects.Count; index++)
        {
            VisitNode(index, state);
        }

        for (var index = 0; index < rawEdges.Count; index++)
        {
            VisitEdge(index, state);
        }

        return state.SceneObjects;
    }

    private static void VisitSubgraph(int index, DocumentTraversalState state)
    {
        state.ValidateSubgraphIndex(index);
        if (!state.VisitedObjectIndices.Add(index))
        {
            return;
        }

        var element = state.RawObjects[index];
        state.SceneObjects.Add(ParseMetaObject(element, SceneObjectKind.Subgraph));

        foreach (var childSubgraphIndex in ReadIndexList(element, "subgraphs"))
        {
            VisitSubgraph(childSubgraphIndex, state);
        }

        foreach (var nodeIndex in ReadIndexList(element, "nodes"))
        {
            VisitNode(nodeIndex, state);
        }

        foreach (var edgeIndex in ReadIndexList(element, "edges"))
        {
            VisitEdge(edgeIndex, state);
        }
    }

    private static void VisitNode(int index, DocumentTraversalState state)
    {
        state.ValidateNodeIndex(index);
        if (!state.VisitedObjectIndices.Add(index))
        {
            return;
        }

        state.SceneObjects.Add(ParseMetaObject(state.RawObjects[index], SceneObjectKind.Node));
    }

    private static void VisitEdge(int index, DocumentTraversalState state)
    {
        state.ValidateEdgeIndex(index);
        if (!state.VisitedEdgeIndices.Add(index))
        {
            return;
        }

        state.SceneObjects.Add(ParseEdgeObject(state.RawEdges[index]));
    }

    private static SceneObject ParseMetaObject(JsonElement element, SceneObjectKind kind)
    {
        EnsureObject(element, $"A {kind} entry must be a JSON object.");

        var name = GetRequiredString(element, "name");
        var bounds = ParseBounds(element);
        var commands = ParseDrawCommands(element, NodeAndSubgraphDrawAttributeNames);
        return new SceneObject(kind, name, bounds, commands);
    }

    private static SceneObject ParseEdgeObject(JsonElement element)
    {
        EnsureObject(element, "An edge entry must be a JSON object.");

        var gvid = GetOptionalInt32(element, "_gvid");
        var name = gvid is int id ? $"edge:{id.ToString(CultureInfo.InvariantCulture)}" : null;
        var bounds = ParseBounds(element);
        var commands = ParseDrawCommands(element, EdgeDrawAttributeNames);
        return new SceneObject(SceneObjectKind.Edge, name, bounds, commands);
    }

    private static IReadOnlyList<SceneCommand> ParseDrawCommands(JsonElement element, IReadOnlyList<string> attributeNames)
    {
        var result = new List<SceneCommand>();

        foreach (var attributeName in attributeNames)
        {
            if (!element.TryGetProperty(attributeName, out var drawArray))
            {
                continue;
            }

            if (drawArray.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"The '{attributeName}' property must be a JSON array.");
            }

            var state = DrawState.CreateDefault();
            foreach (var drawOperation in drawArray.EnumerateArray())
            {
                ParseDrawOperation(drawOperation, state, result);
            }
        }

        return result;
    }

    private static void ParseDrawOperation(JsonElement operation, DrawState state, ICollection<SceneCommand> result)
    {
        EnsureObject(operation, "A draw operation must be a JSON object.");

        var op = GetRequiredString(operation, "op");
        switch (op)
        {
            case "c":
                state.Stroke = ParsePaint(operation);
                return;
            case "C":
                state.Fill = ParsePaint(operation);
                return;
            case "F":
                state.Font = ParseFont(operation);
                return;
            case "t":
                state.Font = ApplyFontStyle(state.Font, operation);
                return;
            case "S":
                state.StrokeStyle = ApplyStyle(state.StrokeStyle, GetRequiredString(operation, "style"));
                return;
            case "E":
            case "e":
                result.Add(ParseEllipseCommand(operation, state, isFilled: op == "E"));
                return;
            case "P":
            case "p":
                result.Add(ParsePolygonCommand(operation, state, isFilled: op == "P"));
                return;
            case "B":
            case "b":
                result.Add(ParseBezierCommand(operation, state, isFilled: op == "B"));
                return;
            case "L":
                result.Add(ParsePolylineCommand(operation, state));
                return;
            case "T":
                result.Add(ParseTextCommand(operation, state));
                return;
            default:
                throw new NotSupportedException($"The xdot_json operation '{op}' is not supported.");
        }
    }

    private static EllipseCommand ParseEllipseCommand(JsonElement operation, DrawState state, bool isFilled)
    {
        var rectangle = GetRequiredArray(operation, "rect");
        if (rectangle.GetArrayLength() != 4)
        {
            throw new InvalidDataException("The 'rect' array must contain 4 numeric values.");
        }

        return new EllipseCommand(
            new ScenePoint(
                rectangle[0].GetDouble(),
                rectangle[1].GetDouble()),
            rectangle[2].GetDouble(),
            rectangle[3].GetDouble(),
            state.Stroke,
            isFilled ? state.Fill : null,
            state.StrokeStyle);
    }

    private static PolygonCommand ParsePolygonCommand(JsonElement operation, DrawState state, bool isFilled)
    {
        return new PolygonCommand(
            ParsePointList(GetRequiredArray(operation, "points")),
            state.Stroke,
            isFilled ? state.Fill : null,
            state.StrokeStyle);
    }

    private static BezierCommand ParseBezierCommand(JsonElement operation, DrawState state, bool isFilled)
    {
        return new BezierCommand(
            ParsePointList(GetRequiredArray(operation, "points")),
            state.Stroke,
            isFilled ? state.Fill : null,
            state.StrokeStyle);
    }

    private static PolylineCommand ParsePolylineCommand(JsonElement operation, DrawState state)
    {
        return new PolylineCommand(
            ParsePointList(GetRequiredArray(operation, "points")),
            state.Stroke,
            state.StrokeStyle);
    }

    private static TextCommand ParseTextCommand(JsonElement operation, DrawState state)
    {
        var point = ParsePoint(GetRequiredArray(operation, "pt"), "pt");
        var alignment = GetRequiredString(operation, "align") switch
        {
            "l" => SceneTextAlignment.Left,
            "c" => SceneTextAlignment.Center,
            "r" => SceneTextAlignment.Right,
            var value => throw new InvalidDataException($"The text alignment '{value}' is not supported.")
        };

        return new TextCommand(
            point,
            GetRequiredString(operation, "text"),
            state.Font,
            RequireColor(state.Stroke, "Text commands require a solid stroke color."),
            GetRequiredDouble(operation, "width"),
            alignment);
    }

    private static SceneColor RequireColor(ScenePaint paint, string message)
    {
        return paint as SceneColor
            ?? throw new InvalidDataException(message);
    }

    private static SceneFont ParseFont(JsonElement operation)
    {
        return new SceneFont(
            GetRequiredString(operation, "face"),
            GetRequiredDouble(operation, "size"));
    }

    private static SceneFont ApplyFontStyle(SceneFont current, JsonElement operation)
    {
        if (!operation.TryGetProperty("fontchar", out var fontchar) || fontchar.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException("The 'fontchar' property is required and must be numeric.");
        }

        return new SceneFont(
            current.Family,
            current.Size,
            (SceneFontStyle)fontchar.GetInt32());
    }

    private static ScenePaint ParsePaint(JsonElement operation)
    {
        var gradientKind = GetRequiredString(operation, "grad");
        return gradientKind switch
        {
            "none" => ParseColor(GetRequiredString(operation, "color")),
            "linear" => ParseGradient(operation, SceneGradientKind.Linear),
            "radial" => ParseGradient(operation, SceneGradientKind.Radial),
            _ => throw new InvalidDataException($"The gradient kind '{gradientKind}' is not supported.")
        };
    }

    private static SceneGradient ParseGradient(JsonElement operation, SceneGradientKind gradientKind)
    {
        var start = GetRequiredArray(operation, "p0");
        var end = GetRequiredArray(operation, "p1");
        var stopsElement = GetRequiredArray(operation, "stops");
        var stops = new List<SceneGradientStop>();

        foreach (var stop in stopsElement.EnumerateArray())
        {
            EnsureObject(stop, "A gradient stop must be a JSON object.");
            stops.Add(new SceneGradientStop(
                GetRequiredDouble(stop, "frac"),
                ParseColor(GetRequiredString(stop, "color"))));
        }

        var startPoint = ParsePoint(start, "p0");
        var endPoint = ParsePoint(end, "p1");
        return new SceneGradient(
            gradientKind,
            startPoint,
            endPoint,
            stops,
            ParseOptionalRadius(start),
            ParseOptionalRadius(end));
    }

    private static SceneStrokeStyle ApplyStyle(SceneStrokeStyle current, string rawStyle)
    {
        var width = current.Width;
        var pattern = current.Pattern;

        foreach (var token in rawStyle.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("solid", StringComparison.OrdinalIgnoreCase))
            {
                pattern = SceneLinePattern.Solid;
                continue;
            }

            if (token.Equals("dashed", StringComparison.OrdinalIgnoreCase))
            {
                pattern = SceneLinePattern.Dashed;
                continue;
            }

            if (token.Equals("dotted", StringComparison.OrdinalIgnoreCase))
            {
                pattern = SceneLinePattern.Dotted;
                continue;
            }

            if (token.StartsWith("setlinewidth(", StringComparison.OrdinalIgnoreCase) &&
                token.EndsWith(')'))
            {
                var rawWidth = token["setlinewidth(".Length..^1];
                if (double.TryParse(rawWidth, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) &&
                    parsedWidth > 0)
                {
                    width = parsedWidth;
                }
            }
        }

        return new SceneStrokeStyle(width, pattern);
    }

    private static SceneColor ParseColor(string rawColor)
    {
        if (!rawColor.StartsWith('#'))
        {
            throw new InvalidDataException($"The color '{rawColor}' is not supported.");
        }

        var hex = rawColor[1..];
        return hex.Length switch
        {
            6 => new SceneColor(
                ParseHexByte(hex, 0),
                ParseHexByte(hex, 2),
                ParseHexByte(hex, 4)),
            8 => new SceneColor(
                ParseHexByte(hex, 0),
                ParseHexByte(hex, 2),
                ParseHexByte(hex, 4),
                ParseHexByte(hex, 6)),
            _ => throw new InvalidDataException($"The color '{rawColor}' is not supported.")
        };
    }

    private static byte ParseHexByte(string hex, int startIndex)
    {
        return byte.Parse(hex.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<ScenePoint> ParsePointList(JsonElement pointsArray)
    {
        var points = new List<ScenePoint>();
        foreach (var pointElement in pointsArray.EnumerateArray())
        {
            points.Add(ParsePoint(pointElement, "point"));
        }

        return points;
    }

    private static ScenePoint ParsePoint(JsonElement pointArray, string propertyName)
    {
        if (pointArray.ValueKind != JsonValueKind.Array || pointArray.GetArrayLength() < 2)
        {
            throw new InvalidDataException($"The '{propertyName}' array must contain at least 2 numeric values.");
        }

        return new ScenePoint(
            pointArray[0].GetDouble(),
            pointArray[1].GetDouble());
    }

    private static double? ParseOptionalRadius(JsonElement pointArray)
    {
        if (pointArray.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("A gradient point must be an array.");
        }

        return pointArray.GetArrayLength() >= 3
            ? pointArray[2].GetDouble()
            : null;
    }

    private static SceneRect? ParseBounds(JsonElement element)
    {
        if (!element.TryGetProperty("bb", out var rawBounds))
        {
            return null;
        }

        var rawValue = rawBounds.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var parts = rawValue.Split(
            [',', ' ', '\t', '\r', '\n'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            throw new InvalidDataException($"The bounding box '{rawValue}' is invalid.");
        }

        var minX = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var minY = double.Parse(parts[1], CultureInfo.InvariantCulture);
        var maxX = double.Parse(parts[2], CultureInfo.InvariantCulture);
        var maxY = double.Parse(parts[3], CultureInfo.InvariantCulture);
        return new SceneRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static IReadOnlyList<JsonElement> ReadElementArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return Array.Empty<JsonElement>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The '{propertyName}' property must be a JSON array.");
        }

        return property.EnumerateArray().ToArray();
    }

    private static IReadOnlyList<int> ReadIndexList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return Array.Empty<int>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The '{propertyName}' property must be a JSON array of integers.");
        }

        var result = new List<int>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidDataException($"The '{propertyName}' property must be a JSON array of integers.");
            }

            result.Add(item.GetInt32());
        }

        return result;
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"The '{propertyName}' property is required and must be a JSON array.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The '{propertyName}' property is required and must be a JSON string.");
        }

        return property.GetString()
            ?? throw new InvalidDataException($"The '{propertyName}' property cannot be null.");
    }

    private static double GetRequiredDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"The '{propertyName}' property is required and must be numeric.");
        }

        return property.GetDouble();
    }

    private static int? GetOptionalInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"The '{propertyName}' property must be numeric.");
        }

        return property.GetInt32();
    }

    private static void EnsureObject(JsonElement element, string message)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class DrawState
    {
        public static DrawState CreateDefault()
        {
            return new DrawState
            {
                Stroke = new SceneColor(0, 0, 0),
                Fill = null,
                Font = new SceneFont("Times-Roman", 14),
                StrokeStyle = new SceneStrokeStyle()
            };
        }

        public required ScenePaint Stroke { get; set; }

        public ScenePaint? Fill { get; set; }

        public required SceneFont Font { get; set; }

        public required SceneStrokeStyle StrokeStyle { get; set; }
    }

    private sealed class DocumentTraversalState
    {
        public DocumentTraversalState(
            IReadOnlyList<JsonElement> rawObjects,
            IReadOnlyList<JsonElement> rawEdges,
            int subgraphCount)
        {
            RawObjects = rawObjects;
            RawEdges = rawEdges;
            SubgraphCount = subgraphCount;
        }

        public IReadOnlyList<JsonElement> RawObjects { get; }

        public IReadOnlyList<JsonElement> RawEdges { get; }

        public int SubgraphCount { get; }

        public HashSet<int> VisitedObjectIndices { get; } = [];

        public HashSet<int> VisitedEdgeIndices { get; } = [];

        public List<SceneObject> SceneObjects { get; } = [];

        public void ValidateSubgraphIndex(int index)
        {
            if (index < 0 || index >= SubgraphCount || index >= RawObjects.Count)
            {
                throw new InvalidDataException($"The subgraph index '{index}' is out of range.");
            }
        }

        public void ValidateNodeIndex(int index)
        {
            if (index < SubgraphCount || index >= RawObjects.Count)
            {
                throw new InvalidDataException($"The node index '{index}' is out of range.");
            }
        }

        public void ValidateEdgeIndex(int index)
        {
            if (index < 0 || index >= RawEdges.Count)
            {
                throw new InvalidDataException($"The edge index '{index}' is out of range.");
            }
        }
    }
}
