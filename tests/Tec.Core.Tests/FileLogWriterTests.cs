using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;

namespace Tec.Core.Tests;

public sealed class FileLogWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tec-log-tests-" + Guid.NewGuid().ToString("N"));

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;
    }

    [Fact]
    public void Writes_message_and_full_exception_to_daily_file()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 19, 10, 30, 0, TimeSpan.Zero));
        using (var writer = new FileLogWriter(_directory, clock))
        {
            writer.Write(LogLevel.Info, "UI", "Sesión iniciada", null);
            writer.Write(LogLevel.Error, "Recolector", "Falló la captura", new InvalidOperationException("sin ventana"));
        }

        var text = File.ReadAllText(Path.Combine(_directory, "tec-2026-11-19.log"));
        Assert.Contains("INF", text);
        Assert.Contains("UI: Sesión iniciada", text);
        Assert.Contains("ERR", text);
        Assert.Contains("Recolector: Falló la captura", text);
        Assert.Contains("System.InvalidOperationException: sin ventana", text);
    }

    [Fact]
    public void Skips_messages_below_minimum_level()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 19, 10, 30, 0, TimeSpan.Zero));
        using (var writer = new FileLogWriter(_directory, clock, LogLevel.Info))
        {
            writer.Write(LogLevel.Debug, "X", "detalle", null);
            writer.Write(LogLevel.Info, "X", "importante", null);
        }

        var text = File.ReadAllText(Path.Combine(_directory, "tec-2026-11-19.log"));
        Assert.DoesNotContain("detalle", text);
        Assert.Contains("importante", text);
    }

    [Fact]
    public void Starts_a_new_file_when_the_day_changes()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 19, 23, 59, 0, TimeSpan.Zero));
        using (var writer = new FileLogWriter(_directory, clock))
        {
            writer.Write(LogLevel.Info, "X", "antes", null);
            clock.Now = clock.Now.AddMinutes(2);
            writer.Write(LogLevel.Info, "X", "después", null);
        }

        Assert.Contains("antes", File.ReadAllText(Path.Combine(_directory, "tec-2026-11-19.log")));
        Assert.Contains("después", File.ReadAllText(Path.Combine(_directory, "tec-2026-11-20.log")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }
}
