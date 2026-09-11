using FixArea.Core.Models;

namespace FixArea.Core.Services;

public static class CollisionEngine
{
    public static bool Intersects(ScreenRect a, ScreenRect b) => a.Intersects(b);

    public static ScreenRect? Resolve(
        ScreenRect window,
        bool isMaximized,
        ScreenRect monitorWork,
        IReadOnlyList<ScreenRect> locked,
        CollisionMode mode)
    {
        var blocking = locked.Where(r => !r.IsEmpty && window.Intersects(r)).ToList();
        if (blocking.Count == 0)
            return null;

        if (mode == CollisionMode.OnlyProtectMaximized && !isMaximized)
            return null;

        if (isMaximized)
            return FitLargestRemainder(monitorWork, locked);

        return mode switch
        {
            CollisionMode.Move => BestMove(window, monitorWork, locked),
            CollisionMode.Resize => BestResize(window, monitorWork, locked) ?? FitLargestRemainder(monitorWork, locked),
            CollisionMode.MoveThenResize => BestMove(window, monitorWork, locked)
                ?? BestResize(window, monitorWork, locked)
                ?? FitLargestRemainder(monitorWork, locked),
            _ => BestResize(window, monitorWork, locked)
        };
    }

    public static LockedRegion? FindDropTarget(int cursorX, int cursorY, ScreenRect window, IReadOnlyList<LockedRegion> regions)
    {
        var enabled = regions.Where(r => r.Enabled && !r.Bounds.IsEmpty).ToList();
        var underCursor = enabled.LastOrDefault(r => r.Bounds.ContainsPoint(cursorX, cursorY));
        if (underCursor is not null)
            return underCursor;

        LockedRegion? best = null;
        long bestOverlap = 0;
        foreach (var region in enabled)
        {
            if (!window.Intersects(region.Bounds))
                continue;

            var overlap = window.Intersect(region.Bounds).Area;
            if (overlap <= 0)
                continue;

            var regionCovered = (double)overlap / Math.Max(1, region.Bounds.Area);
            if (regionCovered < 0.08 && overlap < 12_000)
                continue;

            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = region;
            }
        }

        return best;
    }

    public static bool IsMostlyOutside(int cursorX, int cursorY, ScreenRect window, ScreenRect region)
    {
        if (region.ContainsPoint(cursorX, cursorY))
            return false;
        if (!window.Intersects(region))
            return true;

        var overlap = window.Intersect(region).Area;
        var regionCovered = (double)overlap / Math.Max(1, region.Area);
        return regionCovered < 0.08 && overlap < 12_000;
    }

    public static ScreenRect? FitLargestRemainder(ScreenRect monitor, IReadOnlyList<ScreenRect> locked)
    {
        var pieces = new List<ScreenRect> { monitor };
        foreach (var hole in locked.Where(r => !r.IsEmpty && monitor.Intersects(r)))
        {
            var next = new List<ScreenRect>();
            foreach (var piece in pieces)
                next.AddRange(Subtract(piece, hole.Intersect(monitor)));
            pieces = next;
            if (pieces.Count == 0)
                break;
        }

        ScreenRect? best = null;
        foreach (var piece in pieces)
        {
            if (piece.Width < 80 || piece.Height < 80)
                continue;
            if (best is null || piece.Area > best.Value.Area)
                best = piece;
        }

        return best;
    }

    internal static IEnumerable<ScreenRect> Subtract(ScreenRect a, ScreenRect b)
    {
        if (!a.Intersects(b))
        {
            yield return a;
            yield break;
        }

        if (b.Left > a.Left)
            yield return new ScreenRect(a.Left, a.Top, b.Left - a.Left, a.Height);
        if (b.Right < a.Right)
            yield return new ScreenRect(b.Right, a.Top, a.Right - b.Right, a.Height);

        var x1 = Math.Max(a.Left, b.Left);
        var x2 = Math.Min(a.Right, b.Right);
        if (x2 > x1)
        {
            if (b.Top > a.Top)
                yield return new ScreenRect(x1, a.Top, x2 - x1, b.Top - a.Top);
            if (b.Bottom < a.Bottom)
                yield return new ScreenRect(x1, b.Bottom, x2 - x1, a.Bottom - b.Bottom);
        }
    }

    static ScreenRect? BestMove(ScreenRect window, ScreenRect monitor, IReadOnlyList<ScreenRect> locked)
    {
        var candidates = new List<ScreenRect>();
        foreach (var r in locked.Where(window.Intersects))
        {
            candidates.Add(new ScreenRect(r.Left - window.Width, window.Y, window.Width, window.Height));
            candidates.Add(new ScreenRect(r.Right, window.Y, window.Width, window.Height));
            candidates.Add(new ScreenRect(window.X, r.Top - window.Height, window.Width, window.Height));
            candidates.Add(new ScreenRect(window.X, r.Bottom, window.Width, window.Height));
        }

        ScreenRect? best = null;
        long bestCost = long.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!monitor.Contains(candidate))
                continue;
            if (locked.Any(candidate.Intersects))
                continue;

            var cost = (long)Math.Abs(candidate.X - window.X) + Math.Abs(candidate.Y - window.Y);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
            }
        }

        return best;
    }

    static ScreenRect? BestResize(ScreenRect window, ScreenRect monitor, IReadOnlyList<ScreenRect> locked)
    {
        var candidates = new List<ScreenRect>();
        foreach (var r in locked.Where(window.Intersects))
        {
            var leftWidth = r.Left - window.Left;
            if (leftWidth >= 80)
                candidates.Add(new ScreenRect(window.Left, window.Top, leftWidth, window.Height).ClampTo(monitor));

            var rightWidth = window.Right - r.Right;
            if (rightWidth >= 80)
                candidates.Add(new ScreenRect(r.Right, window.Top, rightWidth, window.Height).ClampTo(monitor));

            var topHeight = r.Top - window.Top;
            if (topHeight >= 80)
                candidates.Add(new ScreenRect(window.Left, window.Top, window.Width, topHeight).ClampTo(monitor));

            var bottomHeight = window.Bottom - r.Bottom;
            if (bottomHeight >= 80)
                candidates.Add(new ScreenRect(window.Left, r.Bottom, window.Width, bottomHeight).ClampTo(monitor));
        }

        ScreenRect? best = null;
        foreach (var candidate in candidates)
        {
            if (candidate.IsEmpty || candidate.Width < 80 || candidate.Height < 80)
                continue;
            if (locked.Any(candidate.Intersects))
                continue;
            if (best is null || AreaDelta(window, candidate) < AreaDelta(window, best.Value))
                best = candidate;
        }

        return best;
    }

    static long AreaDelta(ScreenRect original, ScreenRect next) =>
        Math.Abs(original.Area - next.Area) + Math.Abs(original.X - next.X) + Math.Abs(original.Y - next.Y);
}
