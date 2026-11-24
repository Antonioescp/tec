using System.Numerics;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed record VisualChangeOptions
{
    /// <summary>Tamaño (px) de los bloques en que se divide la imagen para compararla.</summary>
    public int BlockSize { get; init; } = 24;

    /// <summary>Se compara un píxel de cada <c>SampleStep</c> en cada eje.</summary>
    public int SampleStep { get; init; } = 2;

    /// <summary>Diferencia de luminancia (0-255) a partir de la cual un píxel cambió.</summary>
    public int PixelThreshold { get; init; } = 24;

    /// <summary>Píxeles muestreados que deben cambiar para considerar que un bloque cambió.</summary>
    public int MinChangedPixelsPerBlock { get; init; } = 3;

    /// <summary>Fracción mínima de bloques cambiados para considerar el cambio relevante.</summary>
    public double MinChangedFraction { get; init; } = 0.02;

    /// <summary>Cantidad mínima de bloques cambiados (para ventanas pequeñas).</summary>
    public int MinChangedBlocks { get; init; } = 6;

    /// <summary>Tiempo sin cambios tras el cual la pantalla se considera estable.</summary>
    public TimeSpan SettleTime { get; init; } = TimeSpan.FromMilliseconds(350);

    /// <summary>Tiempo máximo de espera a que la pantalla se estabilice (p. ej. con animaciones).</summary>
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Cantidad de comparaciones recientes que se recuerdan por bloque.</summary>
    public int AnimationWindow { get; init; } = 4;

    /// <summary>Un bloque que cambió en al menos esta cantidad de las comparaciones recientes es una animación.</summary>
    public int AnimationHits { get; init; } = 2;
}

/// <param name="Bounds">Región cambiada, en coordenadas de la imagen.</param>
/// <param name="ChangedFraction">Fracción de la imagen que cambió (0-1).</param>
/// <param name="Settled">Falso si se emitió por tiempo máximo mientras la pantalla seguía cambiando.</param>
public sealed record VisualChange(ScreenRect Bounds, double ChangedFraction, bool Settled);

/// <summary>
/// Detecta cambios visuales relevantes entre capturas sucesivas de una ventana
/// (overlays de "cargando", paneles que aparecen, resultados que se muestran).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Los cambios pequeños (cursor de texto, escritura, hover) se absorben sin emitir.</item>
/// <item>Ante un cambio relevante espera a que la pantalla se estabilice y lo emite una sola vez.</item>
/// <item>Los bloques que cambian continuamente (spinners, barras de progreso) se tratan como
/// animaciones: no impiden la estabilización ni disparan evidencias nuevas.</item>
/// </list>
/// No es seguro para uso concurrente: una instancia por ventana observada.
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

    /// <summary>Procesa una captura nueva; devuelve el cambio cuando se completa uno relevante.</summary>
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
                _reference = luminance; // absorber cambios pequeños y animaciones
            }
            return null;
        }

        if (HasNonAnimatedChange(tickChanged)) _lastActivityAt = at;

        var settled = at - _lastActivityAt >= _o.SettleTime;
        if (!settled && at - _changeStartedAt < _o.MaxWait) return null;

        _changing = false;
        _reference = luminance;

        if (!IsSignificant(fromReference, excludeAnimated: false)) return null; // volvió a como estaba

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
