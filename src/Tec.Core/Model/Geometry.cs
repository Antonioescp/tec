namespace Tec.Core.Model;

/// <summary>Punto en coordenadas físicas de pantalla (píxeles del escritorio virtual).</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>Rectángulo en coordenadas físicas de pantalla.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public static ScreenRect Empty { get; } = new(0, 0, 0, 0);

    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static ScreenRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public ScreenRect Inflate(int amount) =>
        new(Left - amount, Top - amount, Width + 2 * amount, Height + 2 * amount);

    public ScreenRect Offset(int dx, int dy) => this with { Left = Left + dx, Top = Top + dy };

    public bool Contains(ScreenPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    public bool Contains(int x, int y) => Contains(new ScreenPoint(x, y));

    public ScreenRect Intersect(ScreenRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? FromEdges(left, top, right, bottom) : Empty;
    }

    public ScreenRect Union(ScreenRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        return FromEdges(
            Math.Min(Left, other.Left),
            Math.Min(Top, other.Top),
            Math.Max(Right, other.Right),
            Math.Max(Bottom, other.Bottom));
    }

    public override string ToString() => $"({Left}, {Top}, {Width}x{Height})";
}
