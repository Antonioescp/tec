namespace Tec.Core.Diagnostics;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>Destination of diagnostic messages. Implementations must never throw.</summary>
public interface ILogWriter
{
    void Write(LogLevel level, string category, string message, Exception? exception);
}

/// <summary>
/// Entry point for diagnostic logging. It is global so a logger does not have to be
/// passed to every source, enricher or native hook; by default it writes nothing and
/// the composition root (Tec.App) configures the real destination.
/// </summary>
public static class Log
{
    private static volatile ILogWriter _writer = NullLogWriter.Instance;

    public static void Configure(ILogWriter writer) => _writer = writer;

    public static void Debug(string category, string message) => Write(LogLevel.Debug, category, message, null);

    public static void Info(string category, string message) => Write(LogLevel.Info, category, message, null);

    public static void Warning(string category, string message, Exception? exception = null) =>
        Write(LogLevel.Warning, category, message, exception);

    public static void Error(string category, string message, Exception? exception = null) =>
        Write(LogLevel.Error, category, message, exception);

    private static void Write(LogLevel level, string category, string message, Exception? exception)
    {
        try
        {
            _writer.Write(level, category, message, exception);
        }
        catch
        {
            // Logging an error must never cause another one.
        }
    }

    private sealed class NullLogWriter : ILogWriter
    {
        public static readonly NullLogWriter Instance = new();
        public void Write(LogLevel level, string category, string message, Exception? exception) { }
    }
}
