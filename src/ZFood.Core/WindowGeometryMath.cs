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
/// The window frame's extra size beyond the client area: side borders
/// (<see cref="Horizontal"/>, both sides together) and the title bar plus the
/// top and bottom borders (<see cref="Vertical"/>). Positions refer to the
/// frame's top-left corner, so only the extra size matters for clamping.
/// </summary>
public readonly record struct FrameMargin(int Horizontal, int Vertical);

/// <summary>
/// Pure geometry for restoring the window: saved geometry is validated and
/// corrected so the whole window frame, title bar included, always appears
/// inside a visible screen working area, whatever the settings file claims.
/// </summary>
public static class WindowGeometryMath
{
    /// <summary>Upper bound for plausible saved dimensions; beyond this the value is garbage.</summary>
    private const int MaxPlausible = 100_000;

    /// <summary>
    /// Produces the rectangle the window should be shown at: X/Y are the frame's
    /// top-left position, Width/Height the client size. Saved geometry that is
    /// missing, corrupt, off-screen, or on a no-longer-existing monitor is
    /// corrected: sizes fall back to defaults, frames clamp into the best
    /// matching working area, and completely invisible windows recenter on the
    /// primary working area. Two guarantees hold on every path: the frame's top
    /// edge never sits above its working area (the title bar stays reachable),
    /// and a first run without saved geometry comes out centered on the primary
    /// working area with the entire frame visible.
    /// </summary>
    /// <param name="saved">Saved geometry, or null on first run.</param>
    /// <param name="workingAreas">Working areas of all current screens.</param>
    /// <param name="primaryWorkingArea">Working area of the primary screen.</param>
    /// <param name="defaultWidth">Client width to use when the saved width is unusable.</param>
    /// <param name="defaultHeight">Client height to use when the saved height is unusable.</param>
    /// <param name="minWidth">Smallest acceptable client width.</param>
    /// <param name="minHeight">Smallest acceptable client height.</param>
    /// <param name="frame">Extra frame size beyond the client area.</param>
    public static GeoRect EnsureVisible(WindowGeometry? saved, IReadOnlyList<GeoRect> workingAreas,
        GeoRect primaryWorkingArea, int defaultWidth, int defaultHeight, int minWidth, int minHeight,
        FrameMargin frame = default)
    {
        var screens = workingAreas.Count > 0 ? workingAreas : new[] { primaryWorkingArea };

        var width = UsableSize(saved?.Width) ?? defaultWidth;
        var height = UsableSize(saved?.Height) ?? defaultHeight;
        width = Math.Max(width, minWidth);
        height = Math.Max(height, minHeight);

        if (saved is null || UsablePosition(saved.X) is null || UsablePosition(saved.Y) is null)
            return Center(primaryWorkingArea, width, height, frame);

        var frameRect = new GeoRect(saved.X, saved.Y, width + frame.Horizontal, height + frame.Vertical);

        // Pick the screen that shows the largest part of the saved frame.
        GeoRect? best = null;
        long bestArea = 0;
        foreach (var screen in screens)
        {
            var area = screen.IntersectionArea(frameRect);
            if (area > bestArea)
            {
                bestArea = area;
                best = screen;
            }
        }

        if (best is null)
            return Center(primaryWorkingArea, width, height, frame); // dead monitor or fully off-screen

        var target = best.Value;
        return Fit(target, frameRect.X, frameRect.Y, frameRect.Width, frameRect.Height, frame, clampPosition: true);
    }

    /// <summary>
    /// Fits a frame into a working area: the frame shrinks to the area when it
    /// is larger, and its position clamps (or centers) so the whole frame is
    /// visible with the top edge never above the area. Returns the client rect.
    /// </summary>
    private static GeoRect Fit(GeoRect area, int x, int y, int frameWidth, int frameHeight,
        FrameMargin frame, bool clampPosition)
    {
        frameWidth = Math.Min(frameWidth, area.Width);
        frameHeight = Math.Min(frameHeight, area.Height);

        if (clampPosition)
        {
            x = Math.Clamp(x, area.X, area.Right - frameWidth);
            y = Math.Clamp(y, area.Y, area.Bottom - frameHeight);
        }
        else
        {
            x = area.X + (area.Width - frameWidth) / 2;
            y = area.Y + (area.Height - frameHeight) / 2;
        }

        // The title bar lives at the frame's top edge; keeping y at or below
        // the working area's top is the reachability guarantee. The Clamp above
        // already ensures it (frameHeight <= area.Height), and the centering
        // arithmetic cannot go above it either; this is the explicit backstop.
        y = Math.Max(y, area.Y);

        return new GeoRect(x, y,
            Math.Max(1, frameWidth - frame.Horizontal),
            Math.Max(1, frameHeight - frame.Vertical));
    }

    private static GeoRect Center(GeoRect area, int width, int height, FrameMargin frame)
        => Fit(area, 0, 0, width + frame.Horizontal, height + frame.Vertical, frame, clampPosition: false);

    private static int? UsableSize(int? value)
        => value is int v && v > 0 && v <= MaxPlausible ? v : null;

    private static int? UsablePosition(int? value)
        => value is int v && Math.Abs((long)v) <= MaxPlausible ? v : null;
}
