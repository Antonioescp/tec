using System.Globalization;
using System.Text;
using Tec.Core.Abstractions;

namespace Tec.Core.Diagnostics;

/// <summary>
/// Writes logs to one text file per day (<c>tec-YYYY-MM-DD.log</c>) and deletes files
/// older than <c>retentionDays</c> days. Each line is written and flushed to disk
/// immediately so the last message is not lost if the application exits abruptly.
/// </summary>
public sealed class FileLogWriter : ILogWriter, IDisposable
{
    private const string FilePrefix = "tec-";
    private const string FileExtension = ".log";

    private readonly IClock _clock;
    private readonly LogLevel _minimumLevel;
    private readonly Lock _lock = new();
    private StreamWriter? _stream;
    private DateOnly _streamDate;

    public FileLogWriter(string directory, IClock clock, LogLevel minimumLevel = LogLevel.Debug, int retentionDays = 14)
    {
        Directory = directory;
        _clock = clock;
        _minimumLevel = minimumLevel;
        System.IO.Directory.CreateDirectory(directory);
        DeleteOldFiles(retentionDays);
    }

    public string Directory { get; }

    /// <summary>Current day's file.</summary>
    public string CurrentFilePath => GetFilePath(DateOnly.FromDateTime(_clock.Now.DateTime));

    public void Write(LogLevel level, string category, string message, Exception? exception)
    {
        if (level < _minimumLevel) return;

        var now = _clock.Now;
        var text = Format(now, level, category, message, exception);
        lock (_lock)
        {
            try
            {
                GetStream(DateOnly.FromDateTime(now.DateTime)).Write(text);
            }
            catch
            {
                // Disk full, file locked...: the message is lost, but the app is not interrupted.
                _stream?.Dispose();
                _stream = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    internal static string Format(DateTimeOffset time, LogLevel level, string category, string message, Exception? exception)
    {
        var builder = new StringBuilder()
            .Append(time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ').Append(LevelName(level))
            .Append(" [").Append(Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append("] ")
            .Append(category).Append(": ").Append(message)
            .AppendLine();
        if (exception is not null) builder.AppendLine(exception.ToString());
        return builder.ToString();
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        _ => "ERR",
    };

    private StreamWriter GetStream(DateOnly date)
    {
        if (_stream is not null && _streamDate == date) return _stream;

        _stream?.Dispose();
        var file = new FileStream(GetFilePath(date), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _stream = new StreamWriter(file, new UTF8Encoding(false)) { AutoFlush = true };
        _streamDate = date;
        return _stream;
    }

    private string GetFilePath(DateOnly date) =>
        Path.Combine(Directory, $"{FilePrefix}{date:yyyy-MM-dd}{FileExtension}");

    private void DeleteOldFiles(int retentionDays)
    {
        var limit = _clock.Now.DateTime.AddDays(-retentionDays);
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, $"{FilePrefix}*{FileExtension}"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < limit) File.Delete(file);
            }
            catch
            {
                // Another process may have it open; it will be retried next time.
            }
        }
    }
}
