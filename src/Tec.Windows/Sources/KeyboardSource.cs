using System.ComponentModel;
using System.Runtime.InteropServices;
using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Core.Services;
using Tec.Windows.Native;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Sources;

public sealed record KeyboardCaptureOptions
{
    /// <summary>Captura atajos (Ctrl/Alt/Win + tecla), teclas de función, Enter, Esc y Supr.</summary>
    public bool CaptureCommands { get; init; } = true;

    /// <summary>Captura el texto escrito en un campo (una evidencia por "ráfaga" de escritura).</summary>
    public bool CaptureTextInput { get; init; } = true;

    /// <summary>Tiempo sin escribir tras el cual se considera terminada la ráfaga.</summary>
    public TimeSpan TextIdleTimeout { get; init; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// Tiempo máximo entre las dos pulsaciones de Shift + Shift, Ctrl + Ctrl o Alt + Alt
    /// (atajos de los IDE de JetBrains, p. ej. "Search Everywhere").
    /// </summary>
    public TimeSpan DoubleTapInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Detecta comandos de teclado y escritura con un hook de teclado de bajo nivel.
/// No reconstruye el texto tecleado: al terminar una ráfaga emite una interacción
/// <see cref="InteractionKind.TextInput"/> y el enriquecedor lee el valor real del campo
/// (enmascarando contraseñas).
/// </summary>
public sealed class KeyboardSource : MessageLoopInteractionSource
{
    /// <summary>Modificadores cuya doble pulsación es un comando. Win queda fuera: solo abre el menú Inicio.</summary>
    private enum TapKey { Shift, Ctrl, Alt }

    private readonly KeyboardCaptureOptions _options;
    private readonly Lock _textLock = new();
    private readonly Timer _idleTimer;
    private readonly HashSet<int> _pressed = [];
    private readonly DoubleTapDetector<TapKey> _doubleTap;

    private LowLevelHookProc? _proc;
    private nint _hook;
    private bool _ctrl, _alt, _shift, _win;

    private nint _textWindow;
    private DateTimeOffset _lastTextKeyAt;

    public KeyboardSource(IClock clock, KeyboardCaptureOptions? options = null) : base("teclado", clock)
    {
        _options = options ?? new KeyboardCaptureOptions();
        _doubleTap = new DoubleTapDetector<TapKey>(_options.DoubleTapInterval);
        _idleTimer = new Timer(_ => FlushText(), null, Timeout.Infinite, Timeout.Infinite);
    }

    protected override void Install()
    {
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    protected override void Uninstall()
    {
        if (_hook != 0) UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    protected override void OnStopped()
    {
        FlushText();
        _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var message = (int)wParam;
                if (message is WM_KEYDOWN or WM_SYSKEYDOWN) OnKeyDown((int)data.vkCode);
                else if (message is WM_KEYUP or WM_SYSKEYUP) OnKeyUp((int)data.vkCode);
            }
            catch (Exception ex)
            {
                // Nunca interrumpir la cadena de hooks.
                Log.Error(Name, "Error al procesar el evento de teclado.", ex);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void OnKeyUp(int vk)
    {
        _pressed.Remove(vk);
        UpdateModifier(vk, false);

        if (_doubleTap.KeyUp(ToTapKey(vk), Clock.Now) && _options.CaptureCommands)
        {
            FlushText();
            var name = ToTapKey(vk)!.Value.ToString();
            RaiseKeyboard(InteractionKind.KeyboardCommand, $"{name} + {name}");
        }
    }

    private static TapKey? ToTapKey(int vk) =>
        KeyNames.IsShift(vk) ? TapKey.Shift
        : KeyNames.IsControl(vk) ? TapKey.Ctrl
        : KeyNames.IsAlt(vk) ? TapKey.Alt
        : null;

    private void OnKeyDown(int vk)
    {
        _doubleTap.KeyDown(ToTapKey(vk), Clock.Now);
        var isRepeat = !_pressed.Add(vk);
        if (KeyNames.IsModifier(vk))
        {
            UpdateModifier(vk, true);
            return;
        }

        var hasCommandModifier = _ctrl || _alt || _win;

        if (hasCommandModifier || IsCommandKey(vk))
        {
            FlushText();
            if (!isRepeat && _options.CaptureCommands)
                RaiseKeyboard(InteractionKind.KeyboardCommand, KeyNames.Format(_ctrl, _alt, _shift, _win, vk));
            return;
        }

        if (KeyNames.IsTextKey(vk))
        {
            if (_options.CaptureTextInput) RegisterTextKey();
            return;
        }

        if (KeyNames.IsNavigationKey(vk))
            FlushText();
    }

    private static bool IsCommandKey(int vk) =>
        vk is KeyNames.VK_RETURN or KeyNames.VK_ESCAPE or KeyNames.VK_DELETE or KeyNames.VK_APPS
        || KeyNames.IsFunctionKey(vk);

    private void UpdateModifier(int vk, bool down)
    {
        if (KeyNames.IsControl(vk)) _ctrl = down;
        else if (KeyNames.IsAlt(vk)) _alt = down;
        else if (KeyNames.IsShift(vk)) _shift = down;
        else if (KeyNames.IsWin(vk)) _win = down;
    }

    private void RegisterTextKey()
    {
        var window = Win32Window.GetRoot(GetForegroundWindow());
        lock (_textLock)
        {
            if (_textWindow != 0 && _textWindow != window) FlushTextLocked();
            _textWindow = window;
            _lastTextKeyAt = Clock.Now;
        }
        _idleTimer.Change(_options.TextIdleTimeout, Timeout.InfiniteTimeSpan);
    }

    private void FlushText()
    {
        lock (_textLock) FlushTextLocked();
    }

    private void FlushTextLocked()
    {
        if (_textWindow == 0) return;
        var window = _textWindow;
        _textWindow = 0;
        _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);

        Raise(new Interaction
        {
            Kind = InteractionKind.TextInput,
            // Momento de la última tecla: el enriquecedor busca el elemento enfocado entonces.
            Timestamp = _lastTextKeyAt,
            WindowHandle = window,
            ProcessId = Win32Window.GetProcessId(window),
        });
    }

    private void RaiseKeyboard(InteractionKind kind, string description)
    {
        var window = Win32Window.GetRoot(GetForegroundWindow());
        Raise(new Interaction
        {
            Kind = kind,
            Timestamp = Clock.Now,
            Description = description,
            WindowHandle = window,
            ProcessId = Win32Window.GetProcessId(window),
        });
    }
}
