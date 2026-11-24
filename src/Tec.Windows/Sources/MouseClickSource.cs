using System.ComponentModel;
using System.Runtime.InteropServices;
using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Windows.Native;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Sources;

/// <summary>
/// Detects clicks with a low-level mouse hook. It reacts to the button press
/// (not the release) to capture the screen before the application responds.
/// </summary>
public sealed class MouseClickSource(IClock clock) : MessageLoopInteractionSource("mouse", clock)
{
    private LowLevelHookProc? _proc;
    private nint _hook;
    private uint _lastLeftClickTime;
    private POINT _lastLeftClickPoint;

    protected override void Install()
    {
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    protected override void Uninstall()
    {
        if (_hook != 0) UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                Handle((int)wParam, Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam));
            }
            catch (Exception ex)
            {
                // Never break the hook chain.
                Log.Error(Name, "Error al procesar el evento de mouse.", ex);
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void Handle(int message, MSLLHOOKSTRUCT data)
    {
        var kind = message switch
        {
            WM_LBUTTONDOWN => IsDoubleClick(data) ? InteractionKind.DoubleClick : InteractionKind.LeftClick,
            WM_RBUTTONDOWN => InteractionKind.RightClick,
            WM_MBUTTONDOWN => InteractionKind.MiddleClick,
            _ => null,
        };
        if (kind is null) return;

        var point = new ScreenPoint(data.pt.X, data.pt.Y);
        var root = Win32Window.GetRoot(Win32Window.FromPoint(point));

        Raise(new Interaction
        {
            Kind = kind,
            Timestamp = Clock.Now,
            Point = point,
            WindowHandle = root,
            ProcessId = Win32Window.GetProcessId(root),
        });
    }

    private bool IsDoubleClick(MSLLHOOKSTRUCT data)
    {
        var elapsed = unchecked(data.time - _lastLeftClickTime);
        var isDouble = _lastLeftClickTime != 0
            && elapsed <= GetDoubleClickTime()
            && Math.Abs(data.pt.X - _lastLeftClickPoint.X) <= GetSystemMetrics(SM_CXDOUBLECLK) / 2
            && Math.Abs(data.pt.Y - _lastLeftClickPoint.Y) <= GetSystemMetrics(SM_CYDOUBLECLK) / 2;

        // Reset after a double click so a third click does not count as another double.
        _lastLeftClickTime = isDouble ? 0 : data.time;
        _lastLeftClickPoint = data.pt;
        return isDouble;
    }
}
