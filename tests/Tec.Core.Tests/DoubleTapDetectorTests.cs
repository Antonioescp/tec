using Tec.Core.Services;

namespace Tec.Core.Tests;

public sealed class DoubleTapDetectorTests
{
    private enum Key { Shift, Ctrl }

    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 14, 0, 0, TimeSpan.Zero);
    private readonly DoubleTapDetector<Key> _detector = new(TimeSpan.FromMilliseconds(500));

    private static DateTimeOffset Ms(int ms) => T0.AddMilliseconds(ms);

    private bool Tap(Key key, int downMs, int upMs)
    {
        _detector.KeyDown(key, Ms(downMs));
        return _detector.KeyUp(key, Ms(upMs));
    }

    [Fact]
    public void Two_quick_taps_of_the_same_key_are_a_double_tap()
    {
        Assert.False(Tap(Key.Shift, 0, 80));
        Assert.True(Tap(Key.Shift, 200, 280));
    }

    [Fact]
    public void Auto_repeat_while_held_does_not_break_the_tap()
    {
        _detector.KeyDown(Key.Shift, Ms(0));
        _detector.KeyDown(Key.Shift, Ms(30));
        Assert.False(_detector.KeyUp(Key.Shift, Ms(80)));
        Assert.True(Tap(Key.Shift, 150, 220));
    }

    [Fact]
    public void A_third_tap_starts_a_new_sequence()
    {
        Tap(Key.Shift, 0, 50);
        Assert.True(Tap(Key.Shift, 100, 150));
        Assert.False(Tap(Key.Shift, 200, 250));
    }

    [Fact]
    public void Taps_too_far_apart_are_not_a_double_tap()
    {
        Tap(Key.Shift, 0, 50);
        Assert.False(Tap(Key.Shift, 600, 700));
    }

    [Fact]
    public void Holding_the_key_is_not_a_tap()
    {
        Tap(Key.Shift, 0, 50);
        Assert.False(Tap(Key.Shift, 100, 800));
    }

    [Fact]
    public void Different_keys_are_not_a_double_tap()
    {
        Tap(Key.Shift, 0, 50);
        Assert.False(Tap(Key.Ctrl, 100, 150));
    }

    [Fact]
    public void Another_key_in_between_cancels_the_sequence()
    {
        Tap(Key.Shift, 0, 50);
        _detector.KeyDown(null, Ms(80)); // e.g. a letter
        Assert.False(Tap(Key.Shift, 100, 150));
    }

    [Fact]
    public void A_combination_of_two_tap_keys_is_not_a_tap()
    {
        Tap(Key.Shift, 0, 50);
        _detector.KeyDown(Key.Ctrl, Ms(100));
        _detector.KeyDown(Key.Shift, Ms(110)); // Ctrl + Shift
        Assert.False(_detector.KeyUp(Key.Shift, Ms(150)));
        Assert.False(_detector.KeyUp(Key.Ctrl, Ms(160)));
        Assert.False(Tap(Key.Shift, 200, 250));
    }
}
