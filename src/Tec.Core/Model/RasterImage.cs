namespace Tec.Core.Model;

/// <summary>
/// Imagen en memoria independiente de la plataforma: 32 bits por píxel, orden BGRA,
/// de arriba hacia abajo, sin relleno entre filas. El canal alfa se ignora.
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

/// <summary>Captura de una región de la pantalla.</summary>
/// <param name="Bounds">Región de pantalla capturada (coordenadas físicas).</param>
/// <param name="Image">Píxeles capturados; mismas dimensiones que <paramref name="Bounds"/>.</param>
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
