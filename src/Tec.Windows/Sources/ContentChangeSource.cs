using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Core.Services;
using Tec.Windows.Imaging;
using Tec.Windows.Native;

namespace Tec.Windows.Sources;

/// <summary>Decides which window is observed at each moment (0 = none).</summary>
public interface IWatchedWindowProvider
{
    nint GetWatchedWindow();
}

/// <summary>Observes the active window only if it belongs to one of the target processes.</summary>
public sealed class TargetForegroundWindowProvider(IEnumerable<int> processIds) : IWatchedWindowProvider
{
    private readonly HashSet<int> _processIds = [.. processIds];

    public nint GetWatchedWindow()
    {
        var hwnd = Win32Window.GetForeground();
        return hwnd != 0 && Win32Window.GetProcessId(hwnd) is { } pid && _processIds.Contains(pid) ? hwnd : 0;
    }
}

public sealed record ContentChangeOptions
{
    /// <summary>How often the window is compared.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public VisualChangeOptions Detection { get; init; } = new();
}

/// <summary>
/// Detects visual changes inside a window that do not open new windows:
/// "loading" overlays, panels that appear, tabs or results that are shown.
/// </summary>
/// <remarks>
/// Periodically compares the content of the target process's active window, so it works
/// with any UI technology. It keeps one detector per window: when returning to a window
/// (e.g. after closing a dialog) the changes made while it was in the background are
/// detected.
/// </remarks>
public sealed class ContentChangeSource(
    IClock clock,
    IWatchedWindowProvider windows,
    IWindowContentCapturer capturer,
    ContentChangeOptions? options = null) : IInteractionSource
{
    private readonly ContentChangeOptions _options = options ?? new ContentChangeOptions();
    private readonly Dictionary<nint, VisualChangeDetector> _detectors = [];
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private string? _lastError;

    public string Name => "cambios en pantalla";

    public event EventHandler<Interaction>? InteractionDetected;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        if (_loop is null) return;
        _cts!.Cancel();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { /* canceled */ }
        _cts.Dispose();
        _cts = null;
        _loop = null;
        _detectors.Clear();
    }

    public void Dispose() => Stop();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    Tick();
                    _lastError = null;
                }
                catch (Exception ex)
                {
                    // A one-off capture failure must not stop the observation.
                    // Each distinct error is logged once so the log is not flooded (4 per second).
                    if (ex.Message != _lastError)
                    {
                        _lastError = ex.Message;
                        Log.Warning(Name, "Falló la comparación de la ventana; se reintentará.", ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Tick()
    {
        RemoveClosedWindows();

        var hwnd = windows.GetWatchedWindow();
        if (hwnd == 0) return;

        if (!_detectors.TryGetValue(hwnd, out var detector))
        {
            detector = new VisualChangeDetector(_options.Detection);
            _detectors[hwnd] = detector;
        }

        if (!capturer.TryCapture(hwnd, out var image, out var windowBounds) || image is null)
        {
            detector.Reset();
            return;
        }

        var change = detector.Process(image, clock.Now);
        if (change is null) return;

        var interaction = new Interaction
        {
            Kind = InteractionKind.ContentChanged,
            Timestamp = clock.Now,
            ChangedFraction = change.ChangedFraction,
            WindowHandle = hwnd,
            ProcessId = Win32Window.GetProcessId(hwnd),
            HighlightBounds = change.Bounds.Offset(windowBounds.Left, windowBounds.Top),
        };
        if (!change.Settled)
            interaction = interaction.WithNote(InteractionNote.ScreenStillChanging);

        InteractionDetected?.Invoke(this, interaction);
    }

    private void RemoveClosedWindows()
    {
        foreach (var hwnd in _detectors.Keys.Where(h => !Win32Window.Exists(h)).ToList())
            _detectors.Remove(hwnd);
    }
}
