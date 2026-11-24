using Tec.Core.Model;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Native;

/// <summary>Window and screen queries (Win32) in one place.</summary>
internal static class Win32Window
{
    public static nint FromPoint(ScreenPoint point) =>
        WindowFromPoint(new POINT { X = point.X, Y = point.Y });

    public static nint GetRoot(nint hwnd)
    {
        if (hwnd == 0) return 0;
        var root = GetAncestor(hwnd, GA_ROOT);
        return root == 0 ? hwnd : root;
    }

    public static nint GetOwner(nint hwnd) => hwnd == 0 ? 0 : GetWindow(hwnd, GW_OWNER);

    public static int? GetProcessId(nint hwnd)
    {
        if (hwnd == 0) return null;
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : (int)pid;
    }

    public static string GetTitle(nint hwnd)
    {
        if (hwnd == 0) return string.Empty;
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var buffer = new char[length + 1];
        var copied = GetWindowText(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(copied, 0));
    }

    public static string GetClassName(nint hwnd)
    {
        var buffer = new char[256];
        var copied = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(copied, 0));
    }

    public static bool Exists(nint hwnd) => hwnd != 0 && IsWindow(hwnd);

    public static bool IsVisible(nint hwnd) => hwnd != 0 && IsWindowVisible(hwnd);

    public static bool IsMinimized(nint hwnd) => IsIconic(hwnd);

    public static nint GetForeground() => GetRoot(GetForegroundWindow());

    /// <summary>Full window rectangle (the one PrintWindow uses), including invisible borders.</summary>
    public static ScreenRect GetFullRect(nint hwnd) =>
        GetWindowRect(hwnd, out var r) ? ToScreenRect(r) : ScreenRect.Empty;

    public static bool IsToolWindow(nint hwnd) =>
        (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0;

    /// <summary>Visible window bounds (without the DWM shadow).</summary>
    public static ScreenRect GetBounds(nint hwnd)
    {
        if (!Exists(hwnd)) return ScreenRect.Empty;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) == 0)
            return ToScreenRect(rect);
        return GetWindowRect(hwnd, out rect) ? ToScreenRect(rect) : ScreenRect.Empty;
    }

    public static ScreenRect GetVirtualScreen() => new(
        GetSystemMetrics(SM_XVIRTUALSCREEN),
        GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN),
        GetSystemMetrics(SM_CYVIRTUALSCREEN));

    public static ScreenRect GetMonitorBounds(nint hwnd, ScreenPoint? fallbackPoint)
    {
        var monitor = hwnd != 0
            ? MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
            : MonitorFromPoint(new POINT { X = fallbackPoint?.X ?? 0, Y = fallbackPoint?.Y ?? 0 }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        return monitor != 0 && GetMonitorInfo(monitor, ref info) ? ToScreenRect(info.rcMonitor) : GetVirtualScreen();
    }

    private static ScreenRect ToScreenRect(RECT r) => ScreenRect.FromEdges(r.Left, r.Top, r.Right, r.Bottom);
}
