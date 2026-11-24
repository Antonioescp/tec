using System.Numerics;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed record VisualChangeOptions
{
    /// <summary>Size (px) of the blocks the image is split into for comparison.</summary>
    public int BlockSize { get; init; } = 24;

    /// <summary>One pixel out of every <c>SampleStep</c> is compared on each axis.</summary>
    public int SampleStep { get; init; } = 2;

    /// <summary>Luminance difference (0-255) from which a pixel counts as changed.</summary>
    public int PixelThreshold { get; init; } = 24;

    /// <summary>Sampled pixels that must change for a block to count as changed.</summary>
    public int MinChangedPixelsPerBlock { get; init; } = 3;

    /// <summary>Minimum fraction of changed blocks for the change to be relevant.</summary>
    public double MinChangedFraction { get; init; } = 0.02;

    /// <summary>Minimum number of changed blocks (for small windows).</summary>
    public int MinChangedBlocks { get; init; } = 6;

    /// <summary>Time without changes after which the screen is considered stable.</summary>
    public TimeSpan SettleTime { get; init; } = TimeSpan.FromMilliseconds(350);

    /// <summary>Maximum wait for the screen to settle (e.g. with animations).</summary>
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Number of recent comparisons remembered per block.</summary>
    public int AnimationWindow { get; init; } = 4;

    /// <summary>A block that changed in at least this many recent comparisons is an animation.</summary>
    public int AnimationHits { get; init; } = 2;
}

/// <param name="Bounds">Changed region, in image coordinates.</param>
/// <param name="ChangedFraction">Fraction of the image that changed (0-1).</param>
/// <param name="Settled">False if emitted on timeout while the screen was still changing.</param>
public sealed record VisualChange(ScreenRect Bounds, double ChangedFraction, bool Settled);

/// <summary>
/// Detects relevant visual changes between successive captures of a window
/// ("loading" overlays, panels that appear, results that are shown).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Small changes (text caret, typing, hover) are absorbed without emitting.</item>
/// <item>On a relevant change it waits for the screen to settle and emits it once.</item>
/// <item>Blocks that change continuously (spinners, progress bars) are treated as
/// animations: they neither prevent settling nor trigger new evidence.</item>
/// </list>
/// Not thread-safe: one instance per observed window.
/// </remarks>
public sealed class VisualChangeDetector(VisualChangeOptions? options = null)
{
    private readonly VisualChangeOptions _o = options ?? new VisualChangeOptions();

    private byte[]? _reference;
    private byte[]? _previous;
    private byte[] _history = [];
    private int _width, _height, _sampledWidth, _sampledHeight, _cols, _rows;
    private bool _changing;
    private DateTimeOffset _changeStartedAt, _lastActivityAt;

    public void Reset()
    {
        _reference = null;
        _previous = null;
        _changing = false;
    }

    /// <summary>Processes a new capture; returns the change when a relevant one completes.</summary>
    public VisualChange? Process(RasterImage frame, DateTimeOffset at)
    {
        if (_reference is null || frame.Width != _width || frame.Height != _height)
        {
            Initialize(frame);
            return null;
        }

        var luminance = Sample(frame);
        var tickChanged = ChangedBlocks(_previous!, luminance);
        UpdateHistory(tickChanged);
        _previous = luminance;

        var fromReference = ChangedBlocks(_reference, luminance);

        if (!_changing)
        {
            if (IsSignificant(fromReference, excludeAnimated: true))
            {
                _changing = true;
                _changeStartedAt = at;
                _lastActivityAt = at;
            }
            else
            {
                _reference = luminance; // absorb small changes and animations
            }
            return null;
        }

        if (HasNonAnimatedChange(tickChanged)) _lastActivityAt = at;

        var settled = at - _lastActivityAt >= _o.SettleTime;
        if (!settled && at - _changeStartedAt < _o.MaxWait) return null;

        _changing = false;
        _reference = luminance;

        if (!IsSignificant(fromReference, excludeAnimated: false)) return null; // back to how it was

        var useStatic = IsSignificant(fromReference, excludeAnimated: true);
        return new VisualChange(
            Bounds(fromReference, excludeAnimated: useStatic),
            Count(fromReference, excludeAnimated: false) / (double)fromReference.Length,
            settled);
    }

    private void Initialize(RasterImage frame)
    {
        var step = Math.Max(1, _o.SampleStep);
        _width = frame.Width;
        _height = frame.Height;
        _sampledWidth = (frame.Width + step - 1) / step;
        _sampledHeight = (frame.Height + step - 1) / step;
        var samplesPerBlock = Math.Max(1, _o.BlockSize / step);
        _cols = (_sampledWidth + samplesPerBlock - 1) / samplesPerBlock;
        _rows = (_sampledHeight + samplesPerBlock - 1) / samplesPerBlock;
        _history = new byte[_cols * _rows];
        _reference = Sample(frame);
        _previous = _reference;
        _changing = false;
    }

    private byte[] Sample(RasterImage frame)
    {
        var step = Math.Max(1, _o.SampleStep);
        var result = new byte[_sampledWidth * _sampledHeight];
        var pixels = frame.Pixels;
        for (var sy = 0; sy < _sampledHeight; sy++)
        {
            var row = sy * step * frame.Stride;
            for (var sx = 0; sx < _sampledWidth; sx++)
            {
                var i = row + sx * step * RasterImage.BytesPerPixel;
                result[sy * _sampledWidth + sx] = (byte)((pixels[i + 2] * 77 + pixels[i + 1] * 150 + pixels[i] * 29) >> 8);
            }
        }
        return result;
    }

    private bool[] ChangedBlocks(byte[] a, byte[] b)
    {
        var samplesPerBlock = Math.Max(1, _o.BlockSize / Math.Max(1, _o.SampleStep));
        var counts = new int[_cols * _rows];
        for (var y = 0; y < _sampledHeight; y++)
        {
            var blockRow = y / samplesPerBlock * _cols;
            var offset = y * _sampledWidth;
            for (var x = 0; x < _sampledWidth; x++)
            {
                if (Math.Abs(a[offset + x] - b[offset + x]) > _o.PixelThreshold)
                    counts[blockRow + x / samplesPerBlock]++;
            }
        }
        var changed = new bool[counts.Length];
        for (var i = 0; i < counts.Length; i++) changed[i] = counts[i] >= _o.MinChangedPixelsPerBlock;
        return changed;
    }

    private void UpdateHistory(bool[] tickChanged)
    {
        for (var i = 0; i < _history.Length; i++)
            _history[i] = (byte)((_history[i] << 1) | (tickChanged[i] ? 1 : 0));
    }

    private bool IsAnimated(int block)
    {
        var mask = (1 << Math.Clamp(_o.AnimationWindow, 1, 8)) - 1;
        return BitOperations.PopCount((uint)(_history[block] & mask)) >= _o.AnimationHits;
    }

    private bool HasNonAnimatedChange(bool[] changed)
    {
        for (var i = 0; i < changed.Length; i++)
            if (changed[i] && !IsAnimated(i)) return true;
        return false;
    }

    private int Count(bool[] changed, bool excludeAnimated)
    {
        var count = 0;
        for (var i = 0; i < changed.Length; i++)
            if (changed[i] && !(excludeAnimated && IsAnimated(i))) count++;
        return count;
    }

    private bool IsSignificant(bool[] changed, bool excludeAnimated)
    {
        var count = Count(changed, excludeAnimated);
        return count >= _o.MinChangedBlocks && count >= _o.MinChangedFraction * changed.Length;
    }

    private ScreenRect Bounds(bool[] changed, bool excludeAnimated)
    {
        int minCol = int.MaxValue, minRow = int.MaxValue, maxCol = -1, maxRow = -1;
        for (var i = 0; i < changed.Length; i++)
        {
            if (!changed[i] || (excludeAnimated && IsAnimated(i))) continue;
            int col = i % _cols, row = i / _cols;
            minCol = Math.Min(minCol, col);
            maxCol = Math.Max(maxCol, col);
            minRow = Math.Min(minRow, row);
            maxRow = Math.Max(maxRow, row);
        }
        if (maxCol < 0) return new ScreenRect(0, 0, _width, _height);

        var blockPixels = Math.Max(1, _o.BlockSize / Math.Max(1, _o.SampleStep)) * Math.Max(1, _o.SampleStep);
        return ScreenRect.FromEdges(
            minCol * blockPixels,
            minRow * blockPixels,
            Math.Min(_width, (maxCol + 1) * blockPixels),
            Math.Min(_height, (maxRow + 1) * blockPixels));
    }
}
