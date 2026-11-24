using System.Globalization;
using System.Text;
using Tec.Core.Abstractions;

namespace Tec.Core.Diagnostics;

/// <summary>
/// Escribe los logs en un archivo de texto por día (<c>tec-AAAA-MM-DD.log</c>) y borra los
/// que tengan más de <c>retentionDays</c> días. Cada línea se escribe y se vacía
/// al disco de inmediato para no perder el último mensaje si la aplicación se cierra de golpe.
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

    /// <summary>Archivo del día actual.</summary>
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
                // Disco lleno, archivo bloqueado...: se pierde el mensaje, pero no se interrumpe la app.
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
                // Otro proceso puede tenerlo abierto; se intentará la próxima vez.
            }
        }
    }
}
