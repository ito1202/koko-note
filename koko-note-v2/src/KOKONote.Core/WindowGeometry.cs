namespace KOKONote.Core;

public readonly record struct WorkArea(int X, int Y, int Width, int Height);

public static class WindowGeometry
{
    public static WindowPlacement Restore(WindowPlacement value, IReadOnlyList<WorkArea> areas)
    {
        if (areas.Count == 0) return new();
        // Largest intersection wins; disconnected monitors fall back to the primary work area.
        var area = areas.OrderByDescending(a => Intersection(value, a)).First();
        var width = Math.Clamp(value.Width, Math.Min(420, area.Width), area.Width);
        var height = Math.Clamp(value.Height, Math.Min(280, area.Height), area.Height);
        var x = (int)Math.Clamp((long)value.X, area.X, (long)area.X + area.Width - width);
        var y = (int)Math.Clamp((long)value.Y, area.Y, (long)area.Y + area.Height - height);
        return new(x, y, width, height, value.Maximized);
    }

    private static long Intersection(WindowPlacement p, WorkArea a) =>
        Math.Max(0, Math.Min((long)p.X + p.Width, (long)a.X + a.Width) - Math.Max(p.X, a.X)) *
        Math.Max(0, Math.Min((long)p.Y + p.Height, (long)a.Y + a.Height) - Math.Max(p.Y, a.Y));
}
