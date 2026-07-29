namespace ZFood.Core;

/// <summary>An integer rectangle in screen coordinates.</summary>
public readonly record struct GeoRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(GeoRect other)
        => other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Area of the intersection with another rectangle, 0 when disjoint.</summary>
    public long IntersectionArea(GeoRect other)
    {
        long w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        long h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return w > 0 && h > 0 ? w * h : 0;
    }
}

/// <summary>
/// Pure geometry for restoring the window: saved geometry is validated and
/// corrected so the window always appears fully inside a visible screen working
/// area, whatever the settings file claims.
/// </summary>
public static class WindowGeometryMath
{
    /// <summary>Upper bound for plausible saved dimensions; beyond this the value is garbage.</summary>
    private const int MaxPlausible = 100_000;

    /// <summary>
    /// Produces the rectangle the window should be shown at. Saved geometry that
    /// is missing, corrupt, off-screen, or on a no-longer-existing monitor is
    /// corrected: sizes fall back to defaults, positions clamp into the best
    /// matching working area, and completely invisible windows recenter on the
    /// primary working area.
    /// </summary>
    /// <param name="saved">Saved geometry, or null on first run.</param>
    /// <param name="workingAreas">Working areas of all current screens.</param>
    /// <param name="primaryWorkingArea">Working area of the primary screen.</param>
    /// <param name="defaultWidth">Width to use when the saved width is unusable.</param>
    /// <param name="defaultHeight">Height to use when the saved height is unusable.</param>
    /// <param name="minWidth">Smallest acceptable width.</param>
    /// <param name="minHeight">Smallest acceptable height.</param>
    public static GeoRect EnsureVisible(WindowGeometry? saved, IReadOnlyList<GeoRect> workingAreas,
        GeoRect primaryWorkingArea, int defaultWidth, int defaultHeight, int minWidth, int minHeight)
    {
        var screens = workingAreas.Count > 0 ? workingAreas : new[] { primaryWorkingArea };

        var width = UsableSize(saved?.Width) ?? defaultWidth;
        var height = UsableSize(saved?.Height) ?? defaultHeight;
        width = Math.Max(width, minWidth);
        height = Math.Max(height, minHeight);

        if (saved is null || UsablePosition(saved.X) is null || UsablePosition(saved.Y) is null)
            return Center(primaryWorkingArea, width, height);

        var rect = new GeoRect(saved.X, saved.Y, width, height);

        // Pick the screen that shows the largest part of the saved rectangle.
        GeoRect? best = null;
        long bestArea = 0;
        foreach (var screen in screens)
        {
            var area = screen.IntersectionArea(rect);
            if (area > bestArea)
            {
                bestArea = area;
                best = screen;
            }
        }

        if (best is null)
            return Center(primaryWorkingArea, width, height); // dead monitor or fully off-screen

        var target = best.Value;

        // Fit inside the target working area, then clamp position so the whole
        // window is visible.
        width = Math.Min(width, target.Width);
        height = Math.Min(height, target.Height);
        var x = Math.Clamp(rect.X, target.X, target.Right - width);
        var y = Math.Clamp(rect.Y, target.Y, target.Bottom - height);
        return new GeoRect(x, y, width, height);
    }

    private static int? UsableSize(int? value)
        => value is int v && v > 0 && v <= MaxPlausible ? v : null;

    private static int? UsablePosition(int? value)
        => value is int v && Math.Abs((long)v) <= MaxPlausible ? v : null;

    private static GeoRect Center(GeoRect area, int width, int height)
    {
        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);
        return new GeoRect(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
    }
}
