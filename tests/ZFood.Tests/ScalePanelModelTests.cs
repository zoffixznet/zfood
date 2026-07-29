using ZFood.Core;

namespace ZFood.Tests;

public class ScalePanelModelTests
{
    private static Settings DemoSettings()
        => new()
        {
            Cookware =
            {
                new Cookware { Id = "pot1", Name = "Big pot", Grams = 640, Pinned = true, Order = 1 },
                new Cookware { Id = "pot2", Name = "Steel bowl", Grams = 210, Pinned = true, Order = 2 },
                new Cookware { Id = "pot3", Name = "Sieve", Grams = 120, Pinned = false },
            },
        };

    private static ScalePanelModel Panel(Settings? settings = null) => new(settings ?? DemoSettings());

    private static PotRowModel Row(ScalePanelModel panel, string id) => panel.Rows.First(r => r.Id == id);

    [Fact]
    public void Builds_pinned_rows_in_order_with_the_no_pot_row_last()
    {
        var panel = Panel();
        Assert.Equal(new[] { "pot1", "pot2", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
        Assert.Equal("tare 640", Row(panel, "pot1").TareEcho);
    }

    [Fact]
    public void Typing_gross_computes_net_and_binds_the_dish()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        row.GrossText = "1440";

        Assert.Equal("800", row.NetText);
        Assert.True(row.NetIsComputed);
        Assert.Equal(row, panel.DishRow);
        Assert.Contains("Big pot", panel.DishCaption);
        Assert.Contains("800", panel.DishCaption);
    }

    [Fact]
    public void Typing_net_computes_target_gross()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        row.NetText = "200";

        Assert.Equal("840", row.GrossText);
        Assert.True(row.GrossIsComputed);
        Assert.Equal(PairSide.B, row.Input);
    }

    [Fact]
    public void Several_rows_hold_readings_simultaneously()
    {
        var panel = Panel();
        Row(panel, "pot1").GrossText = "1440";
        Row(panel, "pot2").GrossText = "610";

        Assert.Equal("800", Row(panel, "pot1").NetText);
        Assert.Equal("400", Row(panel, "pot2").NetText);
    }

    [Fact]
    public void Water_delta_flows_from_the_dish_row()
    {
        var panel = Panel();
        Row(panel, "pot1").GrossText = "1440";
        panel.RecipeText = "1000";

        Assert.Equal(-200d, panel.DeltaValue);
        Assert.Equal("-200", panel.DeltaText);
        Assert.True(panel.CanCopy);
    }

    [Fact]
    public void Water_section_stays_dormant_without_recipe_or_dish()
    {
        var panel = Panel();
        Assert.Equal(ScalePanelModel.Dash, panel.DeltaText);
        Assert.Equal("dish = —", panel.DishCaption);
        Assert.False(panel.CanCopy);

        Row(panel, "pot1").GrossText = "1440";
        Assert.Equal(ScalePanelModel.Dash, panel.DeltaText); // no recipe yet

        panel.Reset();
        panel.RecipeText = "1000";
        Assert.Equal(ScalePanelModel.Dash, panel.DeltaText); // no dish yet
    }

    [Fact]
    public void No_pot_row_covers_dishes_weighed_directly()
    {
        var panel = Panel();
        panel.NoPotRow.GrossText = "800";
        panel.RecipeText = "1000";

        Assert.Equal(800d, panel.NoPotRow.NetValue);
        Assert.Equal(-200d, panel.DeltaValue);
        Assert.Equal(panel.NoPotRow, panel.DishRow);
    }

    [Fact]
    public void Latch_follows_typing_while_recipe_empty_and_freezes_when_filled()
    {
        var panel = Panel();
        Row(panel, "pot1").GrossText = "1440";
        Row(panel, "pot2").GrossText = "610";
        Assert.Equal("pot2", panel.DishRow!.Id); // followed the typing

        panel.RecipeText = "1000";
        Row(panel, "pot1").GrossText = "1500";
        Assert.Equal("pot2", panel.DishRow!.Id); // frozen: typing no longer moves it
        Assert.Equal(-600d, panel.DeltaValue);   // (610 - 210) - 1000
    }

    [Fact]
    public void Explicit_bind_moves_the_frozen_latch_and_reports_loud()
    {
        var panel = Panel();
        PotRowModel? reboundTo = null;
        var loudMove = false;
        panel.DishRebound += (row, loud) => (reboundTo, loudMove) = (row, loud);

        Row(panel, "pot1").GrossText = "1440";
        panel.RecipeText = "1000";
        panel.BindDish(Row(panel, "pot2"));

        Assert.Equal("pot2", panel.DishRow!.Id);
        Assert.Equal("pot2", reboundTo!.Id);
        Assert.True(loudMove);
    }

    [Fact]
    public void Rebinding_while_recipe_is_empty_is_not_loud()
    {
        var panel = Panel();
        var loudMove = true;
        panel.DishRebound += (_, loud) => loudMove = loud;

        Row(panel, "pot1").GrossText = "1440";
        panel.BindDish(Row(panel, "pot2"));

        Assert.False(loudMove);
    }

    [Fact]
    public void Recipe_first_commutativity_materializes_the_delta_on_first_typing()
    {
        var panel = Panel();
        panel.RecipeText = "1000";
        Row(panel, "pot1").GrossText = "1440";

        Assert.Equal("pot1", panel.DishRow!.Id);
        Assert.Equal(-200d, panel.DeltaValue);
    }

    [Fact]
    public void Gross_below_tare_flags_the_row_and_still_propagates()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        row.GrossText = "500";

        Assert.True(row.BelowTare);
        Assert.Equal("-140", row.NetText);

        panel.RecipeText = "100";
        Assert.Equal(-240d, panel.DeltaValue);
    }

    [Fact]
    public void Promoting_from_the_expander_inserts_a_session_row_above_no_pot()
    {
        var panel = Panel();
        var row = panel.PromoteToSession("pot3")!;

        Assert.False(row.Pinned);
        Assert.Equal(new[] { "pot1", "pot2", "pot3", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
        Assert.DoesNotContain(panel.AvailablePots, c => c.Id == "pot3");
    }

    [Fact]
    public void Pinning_a_session_row_updates_settings_and_position()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);
        var saved = 0;
        panel.SettingsChanged += () => saved++;

        var row = panel.PromoteToSession("pot3")!;
        Assert.True(panel.TogglePin(row));

        Assert.True(row.Pinned);
        Assert.True(settings.Cookware.First(c => c.Id == "pot3").Pinned);
        Assert.Equal(1, saved);
        Assert.Equal(new[] { "pot1", "pot2", "pot3", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
    }

    [Fact]
    public void Pin_cap_refuses_with_a_note()
    {
        var settings = DemoSettings();
        for (var i = 0; i < 3; i++)
            settings.Cookware.Add(new Cookware { Id = $"extra{i}", Name = $"Extra {i}", Grams = 100, Pinned = true, Order = 10 + i });
        var panel = Panel(settings);
        string? note = null;
        panel.Note += n => note = n;

        var row = panel.PromoteToSession("pot3")!;
        Assert.False(panel.TogglePin(row));
        Assert.False(row.Pinned);
        Assert.NotNull(note);
    }

    [Fact]
    public void Unpinning_an_empty_row_removes_it_back_to_the_expander()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);

        Assert.True(panel.TogglePin(Row(panel, "pot2")));

        Assert.DoesNotContain(panel.Rows, r => r.Id == "pot2");
        Assert.False(settings.Cookware.First(c => c.Id == "pot2").Pinned);
        Assert.Contains(panel.AvailablePots, c => c.Id == "pot2");
    }

    [Fact]
    public void Unpinning_a_row_holding_a_value_demotes_it_to_a_session_row()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        row.GrossText = "1440";

        Assert.True(panel.TogglePin(row));

        Assert.Contains(panel.Rows, r => r.Id == "pot1");
        Assert.False(Row(panel, "pot1").Pinned);
        Assert.Equal("800", Row(panel, "pot1").NetText); // value survived
        Assert.Equal(new[] { "pot2", "pot1", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
    }

    [Fact]
    public void Unpinning_the_dish_row_keeps_it_visible()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        row.GrossText = "1440"; // dish now, holds value; clear the value but keep dish
        row.GrossText = "";

        Assert.True(panel.TogglePin(row));
        Assert.Contains(panel.Rows, r => r.Id == "pot1");
        Assert.Equal(row, panel.DishRow);
    }

    [Fact]
    public void Reset_clears_rows_recipe_dish_and_collapses_session_rows()
    {
        var panel = Panel();
        Row(panel, "pot1").GrossText = "1440";
        panel.PromoteToSession("pot3")!.GrossText = "310";
        panel.RecipeText = "1000";

        panel.Reset();

        Assert.Equal(new[] { "pot1", "pot2", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
        Assert.All(panel.Rows, r => Assert.Equal("", r.GrossText));
        Assert.All(panel.Rows, r => Assert.Equal(PairSide.None, r.Input));
        Assert.Equal("", panel.RecipeText);
        Assert.Null(panel.DishRow);
        Assert.Equal(ScalePanelModel.Dash, panel.DeltaText);
        Assert.Equal("dish = —", panel.DishCaption);

        // The latch is free again: typing binds immediately.
        Row(panel, "pot2").GrossText = "610";
        Assert.Equal("pot2", panel.DishRow!.Id);
    }

    [Fact]
    public void Tare_edit_re_derives_from_the_sticky_input_without_moving_the_dish()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);
        Row(panel, "pot1").GrossText = "1440";
        Row(panel, "pot2").GrossText = "610";
        panel.RecipeText = "1000";
        panel.BindDish(Row(panel, "pot1"));

        settings.Cookware.First(c => c.Id == "pot1").Grams = 600;
        panel.SyncFromSettings();

        var row = Row(panel, "pot1");
        Assert.Equal(600, row.Tare);
        Assert.Equal("840", row.NetText);           // re-derived from sticky gross 1440
        Assert.Equal(PairSide.A, row.Input);        // role unchanged
        Assert.Equal("pot1", panel.DishRow!.Id);    // binding unchanged
        Assert.Equal(-160d, panel.DeltaValue);      // delta followed
    }

    [Fact]
    public void Deleting_a_pot_with_a_value_clears_the_row_loudly()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);
        Row(panel, "pot1").GrossText = "1440";
        panel.RecipeText = "1000";
        string? note = null;
        panel.Note += n => note = n;

        settings.Cookware.RemoveAll(c => c.Id == "pot1");
        panel.SyncFromSettings();

        Assert.DoesNotContain(panel.Rows, r => r.Id == "pot1");
        Assert.NotNull(note);
        Assert.Contains("Big pot", note);
        Assert.Null(panel.DishRow);                          // was the dish: dropped
        Assert.Equal(ScalePanelModel.Dash, panel.DeltaText); // water section dormant
    }

    [Fact]
    public void Deleting_an_untouched_pot_is_quiet()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);
        string? note = null;
        panel.Note += n => note = n;

        settings.Cookware.RemoveAll(c => c.Id == "pot2");
        panel.SyncFromSettings();

        Assert.DoesNotContain(panel.Rows, r => r.Id == "pot2");
        Assert.Null(note);
    }

    [Fact]
    public void Newly_pinned_cookware_appears_after_a_sync()
    {
        var settings = DemoSettings();
        var panel = Panel(settings);

        settings.Cookware.First(c => c.Id == "pot3").Pinned = true;
        settings.Cookware.First(c => c.Id == "pot3").Order = 0; // first
        panel.SyncFromSettings();

        Assert.Equal(new[] { "pot3", "pot1", "pot2", PotRowModel.NoPotId }, panel.Rows.Select(r => r.Id));
    }

    [Fact]
    public void Row_entries_use_per_row_units_and_the_dish_absorbs_the_water_clause()
    {
        var panel = Panel();
        var t0 = new DateTimeOffset(2026, 7, 20, 14, 40, 0, TimeSpan.Zero);
        Row(panel, "pot1").GrossText = "1440";
        Row(panel, "pot2").GrossText = "610";
        panel.RecipeText = "1000";

        var dishEntry = Row(panel, "pot2").TryBuildEntry(t0)!;
        Assert.Equal(LogPanel.Water, dishEntry.Panel);
        Assert.Equal(LogEntryFactory.PotUnit("pot2"), dishEntry.Unit);
        Assert.Equal("-600", dishEntry.Result);

        var otherEntry = Row(panel, "pot1").TryBuildEntry(t0)!;
        Assert.Equal(LogPanel.Tare, otherEntry.Panel);
        Assert.Equal("800", otherEntry.Result);

        Assert.Null(panel.NoPotRow.TryBuildEntry(t0)); // untouched row has nothing to log
    }

    [Fact]
    public void Incomplete_rows_build_no_entry()
    {
        var panel = Panel();
        var t0 = DateTimeOffset.Now;
        var row = Row(panel, "pot1");
        row.GrossText = "abc";
        Assert.Null(row.TryBuildEntry(t0));
        Assert.Equal(ScalePanelModel.Dash, row.NetText);
    }

    [Fact]
    public void Edits_mark_the_unit_changed_for_commit_tracking()
    {
        var panel = Panel();
        var row = Row(panel, "pot1");
        Assert.False(row.ChangedSinceCommit);
        row.GrossText = "1440";
        Assert.True(row.ChangedSinceCommit);

        row.ChangedSinceCommit = false;
        panel.RecipeText = "1000"; // recipe edits belong to the dish row's unit
        Assert.True(row.ChangedSinceCommit);
    }
}
