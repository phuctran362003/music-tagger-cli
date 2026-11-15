using System.Diagnostics;
using MusicTagger.Core.Contracts;

namespace MusicTagger.Infrastructure.Logging;

/// <summary>
/// Appends diagnostic log lines to a daily file inside the application data directory.
/// The UI only shows friendly messages; exception detail ends up here (SRS FR-015).
/// </summary>
public sealed class FileLogger : IAppLog
{
    private readonly object _gate = new();
    private readonly string _directory;
    private bool _failureReported;

    public FileLogger(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _directory = logDirectory;
    }

    public string LogDirectory => _directory;

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, $"musictagger-{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (_gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Diagnostics must never break the application. Report once on the debug channel.
            Debug.WriteLine($"MusicTagger: could not write to the log file: {ex.Message}");
            if (!_failureReported)
            {
                _failureReported = true;
                Debug.WriteLine($"MusicTagger: log directory '{_directory}' is not writable; further log entries are dropped.");
            }
        }
    }
}
