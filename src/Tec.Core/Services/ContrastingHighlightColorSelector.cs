using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed record HighlightColorOptions
{
    /// <summary>Color used whenever it contrasts with the background.</summary>
    public RgbColor Preferred { get; init; } = RgbColor.Red;

    /// <summary>Fallback colors, in order of preference.</summary>
    public IReadOnlyList<RgbColor> Alternatives { get; init; } =
        [RgbColor.Blue, RgbColor.Magenta, RgbColor.Lime, RgbColor.Yellow, RgbColor.Cyan];

    /// <summary>Distance below which a pixel is considered "similar" to a color.</summary>
    public double SimilarityThreshold { get; init; } = 150;

    /// <summary>Maximum fraction of similar pixels tolerated to use the preferred color.</summary>
    public double MaxSimilarFraction { get; init; } = 0.15;

    /// <summary>Width (px) of the band sampled around the highlight border.</summary>
    public int SampleBand { get; init; } = 6;

    /// <summary>Distance between samples (px).</summary>
    public int SampleStep { get; init; } = 2;
}

/// <summary>
/// Samples the pixels around the highlight border (where the box is drawn) and uses
/// the preferred color (red) unless the background resembles it; in that case it picks
/// the fallback least likely to blend with the background.
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

        // The first acceptable fallback honors the configured order of preference.
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
                    // Skip the inside of the ring up to the first sample on the right.
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
