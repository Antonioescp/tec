using System.Drawing;
using System.Drawing.Imaging;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Imaging;

public interface IWindowContentCapturer
{
    /// <summary>
    /// Captures the window's own content, without whatever is on top of it (tooltips,
    /// dialogs, other applications) or the cursor.
    /// </summary>
    bool TryCapture(nint hwnd, out RasterImage? image, out ScreenRect windowBounds);
}

/// <summary>Capture with <c>PrintWindow(PW_RENDERFULLCONTENT)</c>, works with WPF/DirectX.</summary>
public sealed class PrintWindowContentCapturer : IWindowContentCapturer
{
    public bool TryCapture(nint hwnd, out RasterImage? image, out ScreenRect windowBounds)
    {
        image = null;
        windowBounds = Win32Window.GetFullRect(hwnd);
        if (windowBounds.IsEmpty || Win32Window.IsMinimized(hwnd)) return false;

        using var bitmap = new Bitmap(windowBounds.Width, windowBounds.Height, PixelFormat.Format32bppRgb);
        bool printed;
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            try
            {
                printed = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        if (!printed) return false;
        image = BitmapConversions.ToRasterImage(bitmap);
        return true;
    }
}
