using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsJsonLinesDiagnosticLogger : IDiagnosticLogger
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private readonly object _sync = new();

    public WindowsJsonLinesDiagnosticLogger(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
    }

    public DiagnosticRunContext StartRun()
    {
        try
        {
            return DiagnosticRunContext.Start();
        }
        catch
        {
            return DiagnosticRunContext.Disabled;
        }
    }

    public void Log(
        DiagnosticRunContext run,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null)
    {
        if (run is null || !run.IsEnabled || string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                var directoryPath = Path.GetDirectoryName(_filePath);

                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var entry = new DiagnosticLogEntry(
                    DateTimeOffset.UtcNow,
                    Stopwatch.GetElapsedTime(_startedTimestamp).TotalMilliseconds,
                    run.RunId,
                    eventName,
                    Environment.CurrentManagedThreadId,
                    fields ?? EmptyFields.Instance);
                var json = JsonSerializer.Serialize(entry, SerializerOptions);

                File.AppendAllText(
                    _filePath,
                    json + Environment.NewLine,
                    Utf8WithoutBom);
            }
        }
        catch
        {
        }
    }

    private sealed record DiagnosticLogEntry(
        DateTimeOffset UtcTimestamp,
        double MonotonicMs,
        string RunId,
        string EventName,
        int ThreadId,
        IReadOnlyDictionary<string, object?> Fields);

    private sealed class EmptyFields : Dictionary<string, object?>
    {
        private EmptyFields()
        {
        }

        public static EmptyFields Instance { get; } = new();
    }
}
