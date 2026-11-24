namespace Tec.Core.Model;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Red { get; } = new(255, 0, 0);
    public static RgbColor Blue { get; } = new(0, 102, 255);
    public static RgbColor Magenta { get; } = new(255, 0, 255);
    public static RgbColor Lime { get; } = new(0, 220, 0);
    public static RgbColor Yellow { get; } = new(255, 214, 0);
    public static RgbColor Cyan { get; } = new(0, 220, 255);
    public static RgbColor Black { get; } = new(0, 0, 0);
    public static RgbColor White { get; } = new(255, 255, 255);

    /// <summary>
    /// Distancia perceptual aproximada ("redmean"). 0 = idénticos, ~765 = máximo.
    /// </summary>
    public double DistanceTo(RgbColor other)
    {
        var rMean = (R + other.R) / 2.0;
        double dr = R - other.R, dg = G - other.G, db = B - other.B;
        return Math.Sqrt(
            (2 + rMean / 256) * dr * dr +
            4 * dg * dg +
            (2 + (255 - rMean) / 256) * db * db);
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();
}
