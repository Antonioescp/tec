using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class ContrastingHighlightColorSelectorTests
{
    private static readonly ScreenRect FrameBounds = new(100, 100, 200, 150);
    private static readonly ScreenRect Highlight = new(150, 140, 60, 30);

    private static CapturedFrame Solid(RgbColor color) =>
        new(FrameBounds, RasterImage.Solid(FrameBounds.Width, FrameBounds.Height, color));

    [Fact]
    public void Uses_red_on_a_light_background()
    {
        var selector = new ContrastingHighlightColorSelector();
        Assert.Equal(RgbColor.Red, selector.Select(Solid(RgbColor.White), Highlight));
    }

    [Theory]
    [InlineData(255, 0, 0)]
    [InlineData(208, 0, 0)]
    [InlineData(230, 30, 40)]
    public void Avoids_red_on_a_red_background(byte r, byte g, byte b)
    {
        var background = new RgbColor(r, g, b);
        var selected = new ContrastingHighlightColorSelector().Select(Solid(background), Highlight);

        Assert.NotEqual(RgbColor.Red, selected);
        Assert.True(selected.DistanceTo(background) > 150);
    }

    [Fact]
    public void Picks_an_alternative_that_does_not_match_the_background()
    {
        var options = new HighlightColorOptions { Alternatives = [RgbColor.Blue, RgbColor.Lime] };
        var selector = new ContrastingHighlightColorSelector(options);

        // Fondo rojo dentro del anillo y azul fuera: el azul también se confunde, debe elegir verde.
        var pixels = RasterImage.Solid(FrameBounds.Width, FrameBounds.Height, RgbColor.Red);
        var frame = new CapturedFrame(FrameBounds, pixels);
        PaintOutside(frame, Highlight, RgbColor.Blue);

        Assert.Equal(RgbColor.Lime, selector.Select(frame, Highlight));
    }

    [Fact]
    public void Returns_preferred_when_highlight_is_outside_the_frame()
    {
        var selected = new ContrastingHighlightColorSelector().Select(Solid(RgbColor.Red), new ScreenRect(5000, 5000, 10, 10));
        Assert.Equal(RgbColor.Red, selected);
    }

    private static void PaintOutside(CapturedFrame frame, ScreenRect highlight, RgbColor color)
    {
        var image = frame.Image;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            if (highlight.Contains(x + frame.Bounds.Left, y + frame.Bounds.Top)) continue;
            var i = y * image.Stride + x * RasterImage.BytesPerPixel;
            image.Pixels[i] = color.B;
            image.Pixels[i + 1] = color.G;
            image.Pixels[i + 2] = color.R;
        }
    }
}
