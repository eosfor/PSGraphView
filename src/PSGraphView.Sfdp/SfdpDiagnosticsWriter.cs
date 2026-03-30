using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PSGraphView.Sfdp;

internal sealed class SfdpDiagnosticsWriter : IDisposable
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyData = new Dictionary<string, object?>();
    private static readonly SfdpDiagnosticsWriter DisabledInstance = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly SfdpDiagnosticsOptions _options;
    private readonly TextWriter? _writer;

    private SfdpDiagnosticsWriter()
    {
        _options = new SfdpDiagnosticsOptions();
    }

    private SfdpDiagnosticsWriter(SfdpDiagnosticsOptions options, TextWriter writer)
    {
        _options = options;
        _writer = writer;
    }

    public static SfdpDiagnosticsWriter Disabled => DisabledInstance;

    public bool IsEnabled => _writer is not null;

    public bool IncludeIterations => IsEnabled && _options.IncludeIterations;

    public bool IncludeCoordinates => IsEnabled && _options.IncludeCoordinates;

    public static SfdpDiagnosticsWriter Create(SfdpDiagnosticsOptions? options)
        => Create(options, append: false);

    internal static SfdpDiagnosticsWriter Create(SfdpDiagnosticsOptions? options, bool append)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.Path))
        {
            return Disabled;
        }

        var path = options.Path;
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var fileMode = append ? FileMode.Append : FileMode.Create;
        var stream = new FileStream(path, fileMode, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, new UTF8Encoding(false))
        {
            AutoFlush = true
        };

        return new SfdpDiagnosticsWriter(options, writer);
    }

    public void Write(
        string phase,
        string name,
        params (string Key, object? Value)[] data)
        => Write(phase, name, null, null, null, data);

    public void Write(
        string phase,
        string name,
        int? componentId,
        params (string Key, object? Value)[] data)
        => Write(phase, name, componentId, null, null, data);

    public void Write(
        string phase,
        string name,
        int? componentId,
        int? level,
        params (string Key, object? Value)[] data)
        => Write(phase, name, componentId, level, null, data);

    public void Write(
        string phase,
        string name,
        int? componentId = null,
        int? level = null,
        int? iteration = null,
        params (string Key, object? Value)[] data)
    {
        IReadOnlyDictionary<string, object?> payload = data.Length == 0
            ? EmptyData
            : data.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);

        WriteCore(new SfdpDiagnosticEvent(phase, name, componentId, level, iteration, payload));
    }

    public void WriteWithCoordinates(
        string phase,
        string name,
        double[] x,
        double[] y,
        params (string Key, object? Value)[] data)
        => WriteWithCoordinates(phase, name, x, y, null, null, null, data);

    public void WriteWithCoordinates(
        string phase,
        string name,
        double[] x,
        double[] y,
        int? componentId,
        params (string Key, object? Value)[] data)
        => WriteWithCoordinates(phase, name, x, y, componentId, null, null, data);

    public void WriteWithCoordinates(
        string phase,
        string name,
        double[] x,
        double[] y,
        int? componentId,
        int? level,
        params (string Key, object? Value)[] data)
        => WriteWithCoordinates(phase, name, x, y, componentId, level, null, data);

    public void WriteWithCoordinates(
        string phase,
        string name,
        double[] x,
        double[] y,
        int? componentId = null,
        int? level = null,
        int? iteration = null,
        params (string Key, object? Value)[] data)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (x.Length != y.Length)
        {
            throw new ArgumentException("Coordinate arrays must have the same length.");
        }

        var entries = data.Length == 0
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : data.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);

        if (IncludeCoordinates)
        {
            entries["x"] = x.ToArray();
            entries["y"] = y.ToArray();
        }

        WriteCore(new SfdpDiagnosticEvent(phase, name, componentId, level, iteration, entries));
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private void WriteCore(SfdpDiagnosticEvent diagnosticEvent)
    {
        if (!IsEnabled)
        {
            return;
        }

        switch (_options.Format)
        {
            case SfdpDiagnosticFormat.JsonLines:
                _writer!.WriteLine(JsonSerializer.Serialize(diagnosticEvent, JsonOptions));
                break;
            case SfdpDiagnosticFormat.GraphvizLikeText:
                _writer!.WriteLine(FormatGraphvizLikeText(diagnosticEvent));
                break;
            default:
                throw new NotSupportedException($"Diagnostic format '{_options.Format}' is not supported.");
        }
    }

    private static string FormatGraphvizLikeText(SfdpDiagnosticEvent diagnosticEvent)
    {
        var builder = new StringBuilder();
        builder.Append(diagnosticEvent.Phase);
        builder.Append('.');
        builder.Append(diagnosticEvent.Name);

        if (diagnosticEvent.ComponentId.HasValue)
        {
            builder.Append(" component=");
            builder.Append(diagnosticEvent.ComponentId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (diagnosticEvent.Level.HasValue)
        {
            builder.Append(" level=");
            builder.Append(diagnosticEvent.Level.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (diagnosticEvent.Iteration.HasValue)
        {
            builder.Append(" iteration=");
            builder.Append(diagnosticEvent.Iteration.Value.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var entry in diagnosticEvent.Data)
        {
            builder.Append(' ');
            builder.Append(entry.Key);
            builder.Append('=');
            builder.Append(FormatValue(entry.Value));
        }

        return builder.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => text.Contains(' ') ? $"\"{text}\"" : text,
            bool boolean => boolean ? "true" : "false",
            Enum enumValue => enumValue.ToString(),
            Array array => $"[{array.Length}]",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
