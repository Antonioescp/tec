namespace Tec.Core.Model;

/// <summary>
/// Platform-independent in-memory image: 32 bits per pixel, BGRA order,
/// top-down, no padding between rows. The alpha channel is ignored.
/// </summary>
public sealed class RasterImage
{
    public const int BytesPerPixel = 4;

    public RasterImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != width * height * BytesPerPixel)
            throw new ArgumentException("El tamaño del buffer no coincide con las dimensiones.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public int Stride => Width * BytesPerPixel;

    public RgbColor GetPixel(int x, int y)
    {
        var i = y * Stride + x * BytesPerPixel;
        return new RgbColor(Pixels[i + 2], Pixels[i + 1], Pixels[i]);
    }

    public static RasterImage Solid(int width, int height, RgbColor color)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 255;
        }
        return new RasterImage(width, height, pixels);
    }
}

/// <summary>Capture of a screen region.</summary>
/// <param name="Bounds">Captured screen region (physical coordinates).</param>
/// <param name="Image">Captured pixels; same size as <paramref name="Bounds"/>.</param>
public sealed record CapturedFrame(ScreenRect Bounds, RasterImage Image)
{
    public bool TryGetPixel(int screenX, int screenY, out RgbColor color)
    {
        if (!Bounds.Contains(screenX, screenY))
        {
            color = default;
            return false;
        }
        color = Image.GetPixel(screenX - Bounds.Left, screenY - Bounds.Top);
        return true;
    }
}
