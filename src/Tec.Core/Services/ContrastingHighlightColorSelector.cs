using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed record HighlightColorOptions
{
    /// <summary>Color usado siempre que contraste con el fondo.</summary>
    public RgbColor Preferred { get; init; } = RgbColor.Red;

    /// <summary>Colores alternativos, en orden de preferencia.</summary>
    public IReadOnlyList<RgbColor> Alternatives { get; init; } =
        [RgbColor.Blue, RgbColor.Magenta, RgbColor.Lime, RgbColor.Yellow, RgbColor.Cyan];

    /// <summary>Distancia por debajo de la cual un píxel se considera "parecido" a un color.</summary>
    public double SimilarityThreshold { get; init; } = 150;

    /// <summary>Fracción máxima de píxeles parecidos tolerada para usar el color preferido.</summary>
    public double MaxSimilarFraction { get; init; } = 0.15;

    /// <summary>Grosor (px) de la franja muestreada alrededor del borde del resaltado.</summary>
    public int SampleBand { get; init; } = 6;

    /// <summary>Distancia entre muestras (px).</summary>
    public int SampleStep { get; init; } = 2;
}

/// <summary>
/// Muestrea los píxeles alrededor del borde del resaltado (donde se dibuja el recuadro)
/// y usa el color preferido (rojo) salvo que el fondo se le parezca; en ese caso elige
/// la alternativa que menos se confunda con el fondo.
/// </summary>
public sealed class ContrastingHighlightColorSelector(HighlightColorOptions? options = null) : IHighlightColorSelector
{
    private readonly HighlightColorOptions _options = options ?? new HighlightColorOptions();

    public RgbColor Select(CapturedFrame frame, ScreenRect highlight)
    {
        var samples = SampleBorder(frame, highlight);
        if (samples.Count == 0) return _options.Preferred;

        if (SimilarFraction(samples, _options.Preferred) <= _options.MaxSimilarFraction)
            return _options.Preferred;

        var scored = _options.Alternatives
            .Select(color => (color, similar: SimilarFraction(samples, color)))
            .ToList();

        // La primera alternativa aceptable respeta el orden de preferencia configurado.
        foreach (var (color, similar) in scored)
            if (similar <= _options.MaxSimilarFraction) return color;

        return scored
            .OrderBy(c => c.similar)
            .ThenByDescending(c => samples.Average(s => s.DistanceTo(c.color)))
            .Select(c => c.color)
            .DefaultIfEmpty(_options.Preferred)
            .First();
    }

    private double SimilarFraction(List<RgbColor> samples, RgbColor color) =>
        samples.Count(s => s.DistanceTo(color) < _options.SimilarityThreshold) / (double)samples.Count;

    private List<RgbColor> SampleBorder(CapturedFrame frame, ScreenRect highlight)
    {
        var band = Math.Max(1, _options.SampleBand);
        var step = Math.Max(1, _options.SampleStep);
        var outer = highlight.Inflate(band).Intersect(frame.Bounds);
        var inner = highlight.Inflate(-band);
        var samples = new List<RgbColor>();
        if (outer.IsEmpty) return samples;

        for (var y = outer.Top; y < outer.Bottom; y += step)
        {
            var rowInsideInner = !inner.IsEmpty && y >= inner.Top && y < inner.Bottom;
            for (var x = outer.Left; x < outer.Right; x += step)
            {
                if (rowInsideInner && x >= inner.Left && x < inner.Right)
                {
                    // Saltar el interior del anillo hasta la primera muestra a la derecha.
                    var next = outer.Left + (int)Math.Ceiling((inner.Right - outer.Left) / (double)step) * step;
                    x = next - step;
                    continue;
                }
                if (frame.TryGetPixel(x, y, out var color)) samples.Add(color);
            }
        }
        return samples;
    }
}
