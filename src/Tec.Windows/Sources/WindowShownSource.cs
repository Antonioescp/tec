using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Windows.Native;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Sources;

public sealed record WindowCaptureOptions
{
    /// <summary>Espera antes de capturar una ventana recién mostrada, para que termine de dibujarse.</summary>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Clases de ventana que nunca generan evidencia (tooltips, menús, ...).</summary>
    public IReadOnlySet<string> IgnoredClassNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "tooltips_class32", "#32768", "IME", "MSCTFIME UI", "SysShadow",
    };
}

/// <summary>
/// Detecta ventanas de nivel superior que se muestran (ventanas de "espere",
/// diálogos, ventanas de resultado, MessageBox) mediante un WinEvent hook.
/// </summary>
public sealed class WindowShownSource(IClock clock, WindowCaptureOptions? options = null)
    : MessageLoopInteractionSource("ventanas", clock)
{
    private readonly WindowCaptureOptions _options = options ?? new WindowCaptureOptions();
    private WinEventProc? _proc;
    private nint _hook;

    protected override void Install()
    {
        _proc = OnWinEvent;
        _hook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, 0, _proc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (_hook == 0) throw new InvalidOperationException("SetWinEventHook falló.");
    }

    protected override void Uninstall()
    {
        if (_hook != 0) UnhookWinEvent(_hook);
        _hook = 0;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF || hwnd == 0) return;
            if (!IsCandidate(hwnd)) return;

            var title = Win32Window.GetTitle(hwnd);
            Raise(new Interaction
            {
                Kind = InteractionKind.WindowOpened,
                Timestamp = Clock.Now,
                Description = title,
                WindowHandle = hwnd,
                WindowTitle = title,
                ProcessId = Win32Window.GetProcessId(hwnd),
                CaptureDelay = _options.SettleDelay,
            });
        }
        catch (Exception ex)
        {
            // Nunca propagar excepciones a un callback nativo.
            Log.Error(Name, $"Error al procesar la ventana 0x{hwnd:X}.", ex);
        }
    }

    private bool IsCandidate(nint hwnd)
    {
        if (Win32Window.GetRoot(hwnd) != hwnd) return false;         // solo nivel superior
        if (!Win32Window.IsVisible(hwnd)) return false;
        if (Win32Window.IsToolWindow(hwnd) && string.IsNullOrEmpty(Win32Window.GetTitle(hwnd))) return false; // popups/tooltips
        if (string.IsNullOrWhiteSpace(Win32Window.GetTitle(hwnd))) return false;
        return !_options.IgnoredClassNames.Contains(Win32Window.GetClassName(hwnd));
    }
}
