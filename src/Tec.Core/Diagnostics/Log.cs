namespace Tec.Core.Diagnostics;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>Destino de los mensajes de diagnóstico. Una implementación nunca debe lanzar excepciones.</summary>
public interface ILogWriter
{
    void Write(LogLevel level, string category, string message, Exception? exception);
}

/// <summary>
/// Punto de acceso a los logs de diagnóstico. Es un punto de acceso global para no tener
/// que pasar un logger a cada fuente, enriquecedor o hook nativo; por defecto no escribe
/// nada y el composition root (Tec.App) configura el destino real.
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
            // Registrar un error nunca debe provocar otro.
        }
    }

    private sealed class NullLogWriter : ILogWriter
    {
        public static readonly NullLogWriter Instance = new();
        public void Write(LogLevel level, string category, string message, Exception? exception) { }
    }
}
