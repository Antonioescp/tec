using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Tec.Core.Abstractions;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Imaging;

internal static class BitmapConversions
{
    public static RasterImage ToRasterImage(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var pixels = new byte[width * height * RasterImage.BytesPerPixel];
            var rowBytes = width * RasterImage.BytesPerPixel;
            for (var y = 0; y < height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * rowBytes, rowBytes);
            return new RasterImage(width, height, pixels);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static Bitmap ToBitmap(RasterImage image)
    {
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            for (var y = 0; y < image.Height; y++)
                Marshal.Copy(image.Pixels, y * image.Stride, data.Scan0 + y * data.Stride, image.Stride);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return bitmap;
    }
}

/// <summary>Screen capture with GDI (includes layered/transparent windows).</summary>
public sealed class GdiScreenCapturer : IScreenCapturer
{
    public CapturedFrame Capture(ScreenRect region)
    {
        if (region.IsEmpty) throw new ArgumentException("La región a capturar está vacía.", nameof(region));

        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Graphics.CopyFromScreen rejects SourceCopy | CaptureBlt (it validates the enum as a single value),
            // so BitBlt is called directly. CAPTUREBLT includes layered windows.
            var screenDc = NativeMethods.GetDC(0);
            if (screenDc == 0) throw new InvalidOperationException("No se pudo obtener el contexto de dispositivo de la pantalla.");
            var bitmapDc = graphics.GetHdc();
            try
            {
                if (!NativeMethods.BitBlt(bitmapDc, 0, 0, region.Width, region.Height,
                        screenDc, region.Left, region.Top, NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"BitBlt falló al capturar {region}.");
            }
            finally
            {
                graphics.ReleaseHdc(bitmapDc);
                _ = NativeMethods.ReleaseDC(0, screenDc);
            }
        }
        return new CapturedFrame(region, BitmapConversions.ToRasterImage(bitmap));
    }
}

public sealed class PngImageEncoder : IImageEncoder
{
    public string FileExtension => ".png";

    public void Encode(RasterImage image, System.IO.Stream output)
    {
        using var bitmap = BitmapConversions.ToBitmap(image);
        bitmap.Save(output, ImageFormat.Png);
    }
}
