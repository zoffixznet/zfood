using ZFood.Core;

namespace ZFood.Tests;

public class WindowGeometryMathTests
{
    private static readonly GeoRect Primary = new(0, 0, 1920, 1040);
    private static readonly GeoRect Secondary = new(1920, 0, 1920, 1040);
    private static readonly GeoRect[] TwoScreens = { Primary, Secondary };

    private static GeoRect Ensure(WindowGeometry? saved, IReadOnlyList<GeoRect>? screens = null)
        => WindowGeometryMath.EnsureVisible(saved, screens ?? TwoScreens, Primary, 720, 560, 480, 400);

    [Fact]
    public void First_run_centers_the_default_size_on_the_primary_screen()
    {
        var r = Ensure(null);
        Assert.Equal(new GeoRect((1920 - 720) / 2, (1040 - 560) / 2, 720, 560), r);
    }

    [Fact]
    public void In_bounds_geometry_is_kept_exactly()
    {
        var saved = new WindowGeometry { X = 100, Y = 120, Width = 800, Height = 600 };
        Assert.Equal(new GeoRect(100, 120, 800, 600), Ensure(saved));
    }

    [Fact]
    public void Geometry_on_the_second_screen_is_kept()
    {
        var saved = new WindowGeometry { X = 2000, Y = 50, Width = 800, Height = 600 };
        Assert.Equal(new GeoRect(2000, 50, 800, 600), Ensure(saved));
    }

    [Fact]
    public void Partially_off_screen_geometry_is_clamped_fully_visible()
    {
        var saved = new WindowGeometry { X = -150, Y = 900, Width = 800, Height = 600 };
        var r = Ensure(saved);
        Assert.Equal(new GeoRect(0, 1040 - 600, 800, 600), r);
        Assert.True(Primary.Contains(r));
    }

    [Fact]
    public void Fully_off_screen_geometry_recenters_on_primary()
    {
        var saved = new WindowGeometry { X = 9000, Y = 9000, Width = 800, Height = 600 };
        var r = Ensure(saved);
        Assert.True(Primary.Contains(r));
        Assert.Equal(800, r.Width);
    }

    [Fact]
    public void Dead_monitor_geometry_moves_back_to_a_live_screen()
    {
        // Saved on a second screen that no longer exists.
        var saved = new WindowGeometry { X = 2000, Y = 50, Width = 800, Height = 600 };
        var r = Ensure(saved, new[] { Primary });
        Assert.True(Primary.Contains(r));
    }

    [Fact]
    public void Oversized_geometry_shrinks_to_the_working_area()
    {
        var saved = new WindowGeometry { X = 10, Y = 10, Width = 5000, Height = 5000 };
        var r = Ensure(saved);
        Assert.True(r.Width <= Primary.Width);
        Assert.True(r.Height <= Primary.Height);
        Assert.True(Primary.Contains(r) || Secondary.Contains(r));
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(-800, 600)]
    [InlineData(800, 0)]
    [InlineData(int.MaxValue, 600)]
    public void Corrupt_sizes_fall_back_to_defaults(int width, int height)
    {
        var saved = new WindowGeometry { X = 100, Y = 100, Width = width, Height = height };
        var r = Ensure(saved);
        Assert.True(r.Width is 720 or 800);
        Assert.True(r.Height is 560 or 600);
        Assert.True(Primary.Contains(r));
    }

    [Fact]
    public void Corrupt_positions_recenter_on_primary()
    {
        var saved = new WindowGeometry { X = int.MinValue, Y = 100, Width = 800, Height = 600 };
        var r = Ensure(saved);
        Assert.True(Primary.Contains(r));
        Assert.Equal(800, r.Width);
    }

    [Fact]
    public void Sizes_below_the_minimum_are_raised_to_it()
    {
        var saved = new WindowGeometry { X = 100, Y = 100, Width = 50, Height = 50 };
        var r = Ensure(saved);
        Assert.Equal(480, r.Width);
        Assert.Equal(400, r.Height);
    }

    [Fact]
    public void No_screens_at_all_still_yields_a_sane_rectangle()
    {
        var saved = new WindowGeometry { X = 100, Y = 100, Width = 800, Height = 600 };
        var r = Ensure(saved, Array.Empty<GeoRect>());
        Assert.True(Primary.Contains(r));
    }

    // -- Frame-aware clamping: the position is the frame's top-left corner and
    // -- the clamp must keep the whole frame (title bar included) visible.

    private static readonly FrameMargin Frame = new(16, 48);

    private static GeoRect FrameRect(GeoRect client)
        => new(client.X, client.Y, client.Width + Frame.Horizontal, client.Height + Frame.Vertical);

    private static GeoRect EnsureFramed(WindowGeometry? saved, GeoRect area)
        => WindowGeometryMath.EnsureVisible(saved, new[] { area }, area, 960, 620, 800, 460, Frame);

    [Fact]
    public void First_run_centers_the_whole_frame_in_the_primary_working_area()
    {
        var area = new GeoRect(0, 0, 1920, 1040);
        var r = EnsureFramed(null, area);
        var f = FrameRect(r);
        Assert.True(area.Contains(f));
        Assert.Equal((area.Width - f.Width) / 2, f.X - area.X);
        Assert.Equal((area.Height - f.Height) / 2, f.Y - area.Y);
        Assert.Equal(960, r.Width);
        Assert.Equal(620, r.Height);
    }

    [Fact]
    public void First_run_on_a_short_screen_keeps_the_title_bar_visible()
    {
        // A laptop working area shorter than the default frame: the client
        // shrinks so the frame fits, and the top edge never goes above the
        // working area, so the title bar stays reachable.
        var area = new GeoRect(0, 0, 1366, 640);
        var r = EnsureFramed(null, area);
        var f = FrameRect(r);
        Assert.True(area.Contains(f));
        Assert.True(f.Y >= area.Y);
        Assert.Equal(640 - 48, r.Height);
    }

    [Fact]
    public void First_run_respects_a_working_area_that_starts_below_a_top_taskbar()
    {
        var area = new GeoRect(0, 40, 1920, 1000);
        var r = EnsureFramed(null, area);
        Assert.True(r.Y >= 40);
        Assert.True(area.Contains(FrameRect(r)));
    }

    [Fact]
    public void A_saved_top_edge_above_the_working_area_is_pulled_down()
    {
        var area = new GeoRect(0, 0, 1920, 1040);
        var saved = new WindowGeometry { X = 200, Y = -35, Width = 900, Height = 600 };
        var r = EnsureFramed(saved, area);
        Assert.Equal(0, r.Y);
        Assert.Equal(200, r.X);
        Assert.True(area.Contains(FrameRect(r)));
    }

    [Fact]
    public void The_clamp_accounts_for_the_frame_below_and_beside_the_client_area()
    {
        // The client rect alone would fit; the frame around it would not.
        var area = new GeoRect(0, 0, 1920, 1040);
        var saved = new WindowGeometry { X = 1920 - 900 - 5, Y = 1040 - 600 - 5, Width = 900, Height = 600 };
        var r = EnsureFramed(saved, area);
        var f = FrameRect(r);
        Assert.True(area.Contains(f));
        Assert.Equal(area.Right, f.Right);
        Assert.Equal(area.Bottom, f.Bottom);
    }

    [Fact]
    public void A_saved_frame_taller_than_the_working_area_pins_the_top_and_shrinks()
    {
        var area = new GeoRect(0, 0, 1366, 700);
        var saved = new WindowGeometry { X = 100, Y = 100, Width = 900, Height = 900 };
        var r = EnsureFramed(saved, area);
        Assert.Equal(0, r.Y);
        Assert.Equal(700 - 48, r.Height);
        Assert.True(area.Contains(FrameRect(r)));
    }

    [Fact]
    public void Recentering_after_a_dead_monitor_keeps_the_frame_inside_the_primary()
    {
        var area = new GeoRect(0, 0, 1920, 1040);
        var saved = new WindowGeometry { X = 5000, Y = 5000, Width = 900, Height = 600 };
        var r = EnsureFramed(saved, area);
        Assert.True(area.Contains(FrameRect(r)));
        Assert.True(r.Y >= area.Y);
    }
}
