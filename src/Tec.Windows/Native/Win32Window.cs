using System.Text;
using Tec.Core.Model;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Native;

/// <summary>Consultas de ventanas y pantalla (Win32) en un solo lugar.</summary>
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
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : (int)pid;
    }

    public static string GetTitle(nint hwnd)
    {
        if (hwnd == 0) return string.Empty;
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool Exists(nint hwnd) => hwnd != 0 && IsWindow(hwnd);

    public static bool IsVisible(nint hwnd) => hwnd != 0 && IsWindowVisible(hwnd);

    public static bool IsMinimized(nint hwnd) => IsIconic(hwnd);

    public static nint GetForeground() => GetRoot(GetForegroundWindow());

    /// <summary>Rectángulo completo de la ventana (el que usa PrintWindow), incluidos bordes invisibles.</summary>
    public static ScreenRect GetFullRect(nint hwnd) =>
        GetWindowRect(hwnd, out var r) ? ToScreenRect(r) : ScreenRect.Empty;

    public static bool IsToolWindow(nint hwnd) =>
        (GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0;

    /// <summary>Límites visibles de la ventana (sin la sombra de DWM).</summary>
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
