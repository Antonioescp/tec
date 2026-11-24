using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class VisualChangeDetectorTests
{
    private const int Width = 480;
    private const int Height = 360;
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);
    private static readonly RgbColor Background = new(240, 240, 240);
    private static readonly RgbColor Dark = new(20, 20, 20);

    private readonly VisualChangeDetector _detector = new();
    private DateTimeOffset _now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_change_produces_nothing()
    {
        var frame = Frame();
        Assert.Empty(Run(frame, frame, frame, frame, frame));
    }

    [Fact]
    public void Large_change_is_reported_once_after_the_screen_settles()
    {
        var before = Frame();
        var overlay = Frame((120, 90, 240, 180, Dark));

        var changes = Run(before, overlay, overlay, overlay, overlay, overlay, overlay);

        var change = Assert.Single(changes);
        Assert.True(change.Settled);
        Assert.True(change.Bounds.Contains(new ScreenPoint(240, 180)));
        Assert.True(change.Bounds.Left <= 120 && change.Bounds.Right >= 360);
        Assert.True(change.Bounds.Top <= 90 && change.Bounds.Bottom >= 270);
    }

    [Fact]
    public void Change_is_not_reported_until_it_stops_changing()
    {
        var before = Frame();
        var step1 = Frame((0, 0, 200, 100, Dark));
        var step2 = Frame((0, 0, 400, 200, Dark));

        Assert.Empty(Run(before, step1, step2));
        Assert.Single(Run(step2, step2));
    }

    [Fact]
    public void Small_changes_like_a_caret_or_typing_are_ignored()
    {
        var frames = new List<RasterImage> { Frame() };
        for (var i = 1; i <= 12; i++)
            frames.Add(Frame((20, 20, i * 10, 14, Dark), (20 + i * 10, 20, 2, 14, i % 2 == 0 ? Dark : Background)));

        Assert.Empty(Run([.. frames]));
    }

    [Fact]
    public void Continuous_animation_does_not_produce_repeated_evidence()
    {
        // An overlay appears with a large "spinner" that changes on every capture.
        var before = Frame();
        var frames = new List<RasterImage> { before };
        for (var i = 0; i < 24; i++)
        {
            var spinner = i % 2 == 0 ? Dark : new RgbColor(90, 90, 255);
            frames.Add(Frame((60, 60, 360, 240, new RgbColor(100, 100, 100)), (180, 140, 120, 80, spinner)));
        }

        var changes = Run([.. frames]);

        Assert.Single(changes);
    }

    [Fact]
    public void Reverting_to_the_original_screen_before_settling_reports_nothing()
    {
        var before = Frame();
        var flash = Frame((0, 0, 480, 360, Dark));
        Assert.Empty(Run(before, flash, before, before, before, before));
    }

    [Fact]
    public void Resizing_the_window_restarts_detection()
    {
        Assert.Empty(Run(Frame(), Frame(width: 300, height: 200), Frame(width: 300, height: 200)));
    }

    private List<VisualChange> Run(params RasterImage[] frames)
    {
        var changes = new List<VisualChange>();
        foreach (var frame in frames)
        {
            if (_detector.Process(frame, _now) is { } change) changes.Add(change);
            _now += Tick;
        }
        return changes;
    }

    private static RasterImage Frame(params (int X, int Y, int W, int H, RgbColor Color)[] rects) =>
        Frame(Width, Height, rects);

    private static RasterImage Frame(int width, int height, params (int X, int Y, int W, int H, RgbColor Color)[] rects)
    {
        var image = RasterImage.Solid(width, height, Background);
        foreach (var (x0, y0, w, h, color) in rects)
        {
            for (var y = y0; y < Math.Min(height, y0 + h); y++)
                for (var x = x0; x < Math.Min(width, x0 + w); x++)
                {
                    var i = y * image.Stride + x * RasterImage.BytesPerPixel;
                    image.Pixels[i] = color.B;
                    image.Pixels[i + 1] = color.G;
                    image.Pixels[i + 2] = color.R;
                }
        }
        return image;
    }
}
