namespace FixArea.Core.Models;

public readonly struct ScreenRect : IEquatable<ScreenRect>
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;
    public long Area => (long)Math.Max(0, Width) * Math.Max(0, Height);

    public ScreenRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Intersects(ScreenRect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public bool Contains(ScreenRect other) =>
        other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    public bool ContainsPoint(int x, int y) =>
        x >= Left && x < Right && y >= Top && y < Bottom;

    public bool ApproximatelyEquals(ScreenRect other, int tolerance = 24) =>
        Math.Abs(X - other.X) <= tolerance
        && Math.Abs(Y - other.Y) <= tolerance
        && Math.Abs(Width - other.Width) <= tolerance
        && Math.Abs(Height - other.Height) <= tolerance;

    public ScreenRect Intersect(ScreenRect other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new ScreenRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public ScreenRect ClampTo(ScreenRect bounds)
    {
        var left = Math.Max(Left, bounds.Left);
        var top = Math.Max(Top, bounds.Top);
        var right = Math.Min(Right, bounds.Right);
        var bottom = Math.Min(Bottom, bounds.Bottom);
        return new ScreenRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public ScreenRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public override string ToString() => $"{X},{Y} {Width}x{Height}";

    public bool Equals(ScreenRect other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is ScreenRect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(ScreenRect a, ScreenRect b) => a.Equals(b);
    public static bool operator !=(ScreenRect a, ScreenRect b) => !a.Equals(b);
}
