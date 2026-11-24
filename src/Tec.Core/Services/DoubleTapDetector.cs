namespace Tec.Core.Services;

/// <summary>
/// Detecta la doble pulsación de una tecla que por sí sola no es un comando
/// (p. ej. Shift + Shift o Ctrl + Ctrl en los IDE de JetBrains).
/// </summary>
/// <remarks>
/// Cuenta como "toque" presionar y soltar la tecla sin pulsar ninguna otra en medio y sin
/// mantenerla más de <c>maxInterval</c>. Dos toques de la misma tecla con a lo más
/// <c>maxInterval</c> entre ambas liberaciones forman una doble pulsación. Cualquier otra
/// tecla (incluida una combinación como Ctrl + Shift) cancela la secuencia.
/// </remarks>
public sealed class DoubleTapDetector<TKey>(TimeSpan maxInterval) where TKey : struct
{
    private TKey? _held;
    private DateTimeOffset _heldSince;
    private TKey? _lastTap;
    private DateTimeOffset _lastTapAt;

    /// <param name="tapKey">La tecla si puede formar una doble pulsación; <c>null</c> para cualquier otra.</param>
    public void KeyDown(TKey? tapKey, DateTimeOffset at)
    {
        if (tapKey is not { } key)
        {
            Reset();
            return;
        }

        if (_held is { } held)
        {
            // Repetición automática de la tecla mantenida: se ignora.
            if (Same(held, key)) return;

            // Dos teclas a la vez (Ctrl + Shift): es una combinación, no un toque.
            Reset();
            return;
        }

        _held = key;
        _heldSince = at;
    }

    /// <returns><c>true</c> si esta liberación completa una doble pulsación.</returns>
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
            _lastTap = null; // se mantuvo presionada: no es un toque
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
