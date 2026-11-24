using System.Windows.Automation;
using Tec.Core.Abstractions;

namespace Tec.Windows.Uia;

/// <summary>
/// Registra el historial reciente de foco de UI Automation para saber qué elemento
/// tenía el foco en el instante de una interacción de teclado, aunque el foco haya
/// cambiado antes de que se procese.
/// </summary>
public sealed class FocusTracker : IDisposable
{
    private const int Capacity = 64;

    private readonly IClock _clock;
    private readonly LinkedList<(DateTimeOffset At, AutomationElement Element)> _history = new();
    private readonly Lock _lock = new();
    private AutomationFocusChangedEventHandler? _handler;

    public FocusTracker(IClock clock)
    {
        _clock = clock;
    }

    public void Start()
    {
        if (_handler is not null) return;
        _handler = OnFocusChanged;
        // Registrar/quitar manejadores de UIA fuera del hilo de UI evita bloqueos.
        Task.Run(() => Automation.AddAutomationFocusChangedEventHandler(_handler)).Wait();
    }

    public void Stop()
    {
        if (_handler is null) return;
        var handler = _handler;
        try { Task.Run(() => Automation.RemoveAutomationFocusChangedEventHandler(handler)).Wait(TimeSpan.FromSeconds(3)); }
        catch { /* el proceso puede estar cerrándose */ }
        _handler = null;
    }

    /// <summary>Elemento enfocado en <paramref name="at"/> (o el último conocido antes de ese instante).</summary>
    public AutomationElement? GetFocusedAt(DateTimeOffset at)
    {
        lock (_lock)
        {
            for (var node = _history.Last; node is not null; node = node.Previous)
                if (node.Value.At <= at) return node.Value.Element;
            return null;
        }
    }

    public void Dispose() => Stop();

    private void OnFocusChanged(object? sender, AutomationFocusChangedEventArgs e)
    {
        if (sender is not AutomationElement element) return;
        lock (_lock)
        {
            _history.AddLast((_clock.Now, element));
            if (_history.Count > Capacity) _history.RemoveFirst();
        }
    }
}
