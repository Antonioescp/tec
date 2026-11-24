using System.Windows.Automation;
using Tec.Core.Abstractions;

namespace Tec.Windows.Uia;

/// <summary>
/// Records recent UI Automation focus history to know which element had focus at the
/// moment of a keyboard interaction, even if focus changed before it is processed.
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
        // Adding/removing UIA handlers off the UI thread avoids deadlocks.
        Task.Run(() => Automation.AddAutomationFocusChangedEventHandler(_handler)).Wait();
    }

    public void Stop()
    {
        if (_handler is null) return;
        var handler = _handler;
        try { Task.Run(() => Automation.RemoveAutomationFocusChangedEventHandler(handler)).Wait(TimeSpan.FromSeconds(3)); }
        catch { /* the process may be shutting down */ }
        _handler = null;
    }

    /// <summary>Element focused at <paramref name="at"/> (or the last one known before that moment).</summary>
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
