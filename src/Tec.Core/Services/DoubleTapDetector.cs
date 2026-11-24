namespace Tec.Core.Services;

/// <summary>
/// Detects a double tap of a key that is not a command on its own
/// (e.g. Shift + Shift or Ctrl + Ctrl in JetBrains IDEs).
/// </summary>
/// <remarks>
/// A "tap" is pressing and releasing the key with no other key in between and without
/// holding it longer than <c>maxInterval</c>. Two taps of the same key with at most
/// <c>maxInterval</c> between both releases form a double tap. Any other key
/// (including a combination such as Ctrl + Shift) cancels the sequence.
/// </remarks>
public sealed class DoubleTapDetector<TKey>(TimeSpan maxInterval) where TKey : struct
{
    private TKey? _held;
    private DateTimeOffset _heldSince;
    private TKey? _lastTap;
    private DateTimeOffset _lastTapAt;

    /// <param name="tapKey">The key if it can form a double tap; <c>null</c> for any other key.</param>
    /// <param name="at">When the key was pressed.</param>
    public void KeyDown(TKey? tapKey, DateTimeOffset at)
    {
        if (tapKey is not { } key)
        {
            Reset();
            return;
        }

        if (_held is { } held)
        {
            // Auto-repeat of the held key: ignored.
            if (Same(held, key)) return;

            // Two keys at once (Ctrl + Shift): a combination, not a tap.
            Reset();
            return;
        }

        _held = key;
        _heldSince = at;
    }

    /// <returns><c>true</c> if this release completes a double tap.</returns>
    public bool KeyUp(TKey? tapKey, DateTimeOffset at)
    {
        if (tapKey is not { } key) return false;

        if (_held is not { } held || !Same(held, key))
        {
            Reset();
            return false;
        }

        _held = null;
        if (at - _heldSince > maxInterval)
        {
            _lastTap = null; // held too long: not a tap
            return false;
        }

        if (_lastTap is { } last && Same(last, key) && at - _lastTapAt <= maxInterval)
        {
            _lastTap = null;
            return true;
        }

        _lastTap = key;
        _lastTapAt = at;
        return false;
    }

    private static bool Same(TKey a, TKey b) => EqualityComparer<TKey>.Default.Equals(a, b);

    public void Reset()
    {
        _held = null;
        _lastTap = null;
    }
}
