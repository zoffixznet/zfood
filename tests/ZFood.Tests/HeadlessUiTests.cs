using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZFood.App.Services;
using ZFood.App.ViewModels;
using ZFood.App.Views;
using ZFood.Core;

namespace ZFood.Tests;

public class HeadlessUiTests
{
    private sealed class Fixture : IDisposable
    {
        public TempDir Dir { get; } = new();

        public AppServices Services { get; }

        public MainViewModel Vm { get; }

        public MainWindow Window { get; }

        public Fixture()
        {
            var paths = new AppPaths(Dir.Path);
            var settings = new Settings
            {
                Cookware =
                {
                    new Cookware { Id = "p1", Name = "Big pot", Grams = 640, Pinned = true, Order = 0 },
                    new Cookware { Id = "p2", Name = "Steel bowl", Grams = 210, Pinned = true, Order = 1 },
                    new Cookware { Id = "p3", Name = "Sieve", Grams = 120, Pinned = false, Order = 2 },
                },
            };
            new SettingsStore(paths, DiagnosticsLog.Null).Save(settings);
            Services = AppServices.Create(paths);
            Vm = new MainViewModel(Services);
            Window = new MainWindow(Vm, Services);
            Window.Show();
            Pump();
        }

        public void Dispose()
        {
            Window.Close();
            Pump();
            Dir.Dispose();
        }

        public static void Pump() => Dispatcher.UIThread.RunJobs();

        public TextBox Box(string name) => Window.FindControl<TextBox>(name)!;

        public TextBox RowBox(int index, string className)
        {
            Window.UpdateLayout();
            var items = Window.FindControl<ItemsControl>("CookwareRowsControl")!;
            var container = items.ContainerFromIndex(index)!;
            return container.GetVisualDescendants().OfType<TextBox>()
                .First(t => t.Classes.Contains(className));
        }

        public void Type(TextBox box, string text)
        {
            box.Focus();
            Pump();
            Window.KeyTextInput(text);
            Pump();
        }

        public void Press(Key key)
        {
            Window.KeyPressQwerty(ToPhysical(key), RawInputModifiers.None);
            Pump();
        }

        private static PhysicalKey ToPhysical(Key key) => key switch
        {
            Key.Enter => PhysicalKey.Enter,
            Key.Down => PhysicalKey.ArrowDown,
            Key.Up => PhysicalKey.ArrowUp,
            Key.Escape => PhysicalKey.Escape,
            Key.Tab => PhysicalKey.Tab,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

        public string? FocusedName()
            => (Window.FocusManager?.GetFocusedElement() as Control)?.Name;

        public TextBox? FocusedBox() => Window.FocusManager?.GetFocusedElement() as TextBox;
    }

    [AvaloniaFact]
    public void Typing_into_either_pair_member_computes_the_partner_and_flips_roles()
    {
        using var f = new Fixture();

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.Box("ServingCaloriesBox"), "250");
        f.Type(f.Box("EatenGramsBox"), "128");
        Assert.Equal("571", f.Box("EatenCaloriesBox").Text);

        // Typing into the computed member flips the roles.
        f.Type(f.Box("EatenCaloriesBox"), "250");
        Assert.Equal("56", f.Box("EatenGramsBox").Text);
        Assert.Equal(PairSide.B, f.Vm.Portion.EatenInput);
    }

    [AvaloniaFact]
    public void Typing_in_a_cookware_row_makes_it_the_dish_and_the_delta_follows()
    {
        using var f = new Fixture();

        f.Type(f.RowBox(0, "rowGross"), "1440");
        Assert.Equal("800", f.RowBox(0, "rowNet").Text);
        Assert.Equal("p1", f.Vm.Scale.DishRow?.Id);

        f.Type(f.Box("RecipeBox"), "1000");
        Assert.Equal("-200", f.Vm.Scale.DeltaText);
        Assert.Equal("-200", f.Box("DeltaBox").Text);
    }

    [AvaloniaFact]
    public void The_latch_holds_when_another_row_is_edited_while_the_recipe_is_filled()
    {
        using var f = new Fixture();

        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Type(f.Box("RecipeBox"), "1000");
        f.Type(f.RowBox(1, "rowGross"), "610");

        Assert.Equal("p1", f.Vm.Scale.DishRow?.Id); // still Big pot
        Assert.Equal("-200", f.Vm.Scale.DeltaText); // delta untouched by the other row
        Assert.Equal("400", f.RowBox(1, "rowNet").Text); // but the row itself computed
    }

    [AvaloniaFact]
    public void A_tare_edit_after_a_commit_recommits_the_corrected_calculation()
    {
        using var f = new Fixture();

        // Settle a water calculation at tare 640: net 800, delta -200.
        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Type(f.Box("RecipeBox"), "1000");
        f.Press(Key.Enter);
        Fixture.Pump();
        Assert.Contains(f.Services.Log.Entries, e => e.Panel == LogPanel.Water && e.Result == "-200");

        // Correct the pot's tare in settings (as the cookware editor would).
        f.Services.Settings.Cookware.First(c => c.Id == "p1").Grams = 600;
        f.Vm.Scale.SyncFromSettings();
        Assert.Equal("-160", f.Vm.Scale.DeltaText);

        // The next settle point (window deactivation) commits the corrected
        // numbers; the stale -200 is no longer the last word in the log.
        f.Vm.OnWindowDeactivated();
        var newest = f.Services.Log.Entries.Last();
        Assert.Equal(LogPanel.Water, newest.Panel);
        Assert.Equal("-160", newest.Result);
    }

    [AvaloniaFact]
    public void Net_direction_computes_target_gross_per_row()
    {
        using var f = new Fixture();

        f.Type(f.RowBox(1, "rowNet"), "200");
        Assert.Equal("410", f.RowBox(1, "rowGross").Text); // 200 + 210 tare
        Assert.Equal(PairSide.B, f.Vm.Scale.Rows[1].Input);
    }

    [AvaloniaFact]
    public void Promoting_cookware_from_the_expander_yields_an_immediately_typeable_row()
    {
        using var f = new Fixture();

        f.Vm.MoreCookwareOpen = true;
        Fixture.Pump();
        f.Window.UpdateLayout();
        var list = f.Window.FindControl<ListBox>("MoreCookwareList")!;
        list.SelectedIndex = 0; // Sieve, the only unpinned cookware
        var container = list.ContainerFromIndex(0)!;
        container.Focus();
        Fixture.Pump();

        f.Press(Key.Enter);
        f.Window.UpdateLayout();
        Fixture.Pump();

        // The session row exists above the no-cookware row and its reads field has focus.
        Assert.Equal(new[] { "p1", "p2", "p3", CookwareRowModel.NoCookwareId }, f.Vm.Scale.Rows.Select(r => r.Id));
        var focused = f.FocusedBox();
        Assert.NotNull(focused);
        Assert.Contains("rowGross", focused!.Classes);

        f.Window.KeyTextInput("310");
        Fixture.Pump();
        Assert.Equal("190", f.Vm.Scale.Rows.First(r => r.Id == "p3").NetText);
    }

    [AvaloniaFact]
    public void Up_and_down_walk_focus_through_all_fields()
    {
        using var f = new Fixture();

        f.Box("ServingGramsBox").Focus();
        Fixture.Pump();

        f.Press(Key.Down);
        Assert.Equal("EatenGramsBox", f.FocusedName());

        f.Press(Key.Down); // first cookware row's reads field
        var focused = f.FocusedBox();
        Assert.NotNull(focused);
        Assert.Contains("rowGross", focused!.Classes);

        f.Press(Key.Down); // second cookware row
        f.Press(Key.Down); // no-cookware row
        f.Press(Key.Down);
        Assert.Equal("RecipeBox", f.FocusedName());

        f.Press(Key.Down);
        Assert.Equal("DeltaBox", f.FocusedName());

        f.Press(Key.Down); // edge: stays put
        Assert.Equal("DeltaBox", f.FocusedName());

        f.Press(Key.Up);
        Assert.Equal("RecipeBox", f.FocusedName());
    }

    [AvaloniaFact]
    public void The_enter_chain_works_end_to_end_without_modifiers()
    {
        using var f = new Fixture();

        // Type a gross reading, Enter: binds the dish, commits, jumps to recipe.
        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Press(Key.Enter);
        Assert.Equal("RecipeBox", f.FocusedName());
        Assert.Contains(f.Services.Log.Entries, e => e.Panel == LogPanel.Tare && e.Result == "800");

        // Type the recipe weight, Enter: commits the water entry and copies it.
        f.Window.KeyTextInput("1000");
        Fixture.Pump();
        f.Press(Key.Enter);
        Fixture.Pump();

        var water = f.Services.Log.Entries.Last();
        Assert.Equal(LogPanel.Water, water.Panel);
        Assert.Equal("-200", water.Result);

        // The water entry replaced the same row's tare-only entry.
        Assert.DoesNotContain(f.Services.Log.Entries, e => e.Panel == LogPanel.Tare);
    }

    [AvaloniaFact]
    public void Copy_puts_the_bare_signed_number_on_the_clipboard()
    {
        using var f = new Fixture();

        string? copied = null;
        var inner = f.Vm.CopyToClipboard;
        f.Vm.CopyToClipboard = async text =>
        {
            copied = text;
            if (inner is not null)
                await inner(text);
        };

        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Type(f.Box("RecipeBox"), "1000");
        f.Press(Key.Enter);
        Fixture.Pump();

        Assert.Equal("-200", copied);
    }

    private static Button CopyIconOf(TextBox box)
        => box.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("copyIcon"));

    private static void Click(Button button)
        => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void Every_field_carries_a_copy_icon_that_copies_the_bare_number()
    {
        using var f = new Fixture();
        string? copied = null;
        f.Vm.CopyToClipboard = text =>
        {
            copied = text;
            return Task.CompletedTask;
        };

        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Window.UpdateLayout();

        // Input and computed members both show the icon.
        Assert.True(CopyIconOf(f.RowBox(0, "rowNet")).IsVisible);
        Assert.True(CopyIconOf(f.RowBox(0, "rowGross")).IsVisible);

        Click(CopyIconOf(f.RowBox(0, "rowNet")));
        Fixture.Pump();
        Assert.Equal("800", copied);
        Assert.Contains("copied 800", f.Vm.StatusNotice);

        // The input member's icon copies its typed value.
        Click(CopyIconOf(f.RowBox(0, "rowGross")));
        Fixture.Pump();
        Assert.Equal("1440", copied);
    }

    [AvaloniaFact]
    public void Copy_icons_enable_only_while_the_field_shows_a_number()
    {
        using var f = new Fixture();
        f.Window.UpdateLayout();

        // Empty fields: icon present but disabled.
        Assert.False(CopyIconOf(f.Box("ServingGramsBox")).IsEnabled);
        Assert.False(CopyIconOf(f.RowBox(0, "rowGross")).IsEnabled);
        Assert.False(CopyIconOf(f.Box("RecipeBox")).IsEnabled);

        f.Type(f.Box("ServingGramsBox"), "56");
        Assert.True(CopyIconOf(f.Box("ServingGramsBox")).IsEnabled);

        // A placeholder dash is not copyable: eaten calories shows a dash
        // while the serving pair is incomplete.
        f.Type(f.Box("EatenGramsBox"), "128");
        Assert.Equal(PortionViewModel.Dash, f.Box("EatenCaloriesBox").Text);
        Assert.False(CopyIconOf(f.Box("EatenCaloriesBox")).IsEnabled);

        // Completing the serving pair turns the dash into a number.
        f.Type(f.Box("ServingCaloriesBox"), "250");
        Assert.Equal("571", f.Box("EatenCaloriesBox").Text);
        Assert.True(CopyIconOf(f.Box("EatenCaloriesBox")).IsEnabled);
    }

    [AvaloniaFact]
    public void The_delta_copy_icon_replaces_the_external_button_and_commits()
    {
        using var f = new Fixture();
        string? copied = null;
        f.Vm.CopyToClipboard = text =>
        {
            copied = text;
            return Task.CompletedTask;
        };

        // The old external text button is gone; the in-field icon remains.
        Assert.Null(f.Window.FindControl<Button>("CopyButton"));
        var icon = f.Window.FindControl<Button>("DeltaCopyButton")!;
        Assert.False(icon.IsEnabled); // dormant water section

        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Type(f.Box("RecipeBox"), "1000");
        Assert.True(icon.IsEnabled);

        Click(icon);
        Fixture.Pump();

        Assert.Equal("-200", copied);
        Assert.Contains("Big pot", f.Vm.StatusNotice); // names the cookware
        // The icon commits the dish unit like the old button did.
        Assert.Contains(f.Services.Log.Entries, e => e.Panel == LogPanel.Water && e.Result == "-200");
    }

    [AvaloniaFact]
    public void Focusing_a_field_with_a_valid_number_copies_it()
    {
        using var f = new Fixture();
        var copies = new List<string>();
        f.Vm.CopyToClipboard = text =>
        {
            copies.Add(text);
            return Task.CompletedTask;
        };

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.Box("ServingCaloriesBox"), "250");
        f.Type(f.Box("EatenGramsBox"), "128");
        copies.Clear();

        // Focusing the computed member selects its content and copies it.
        f.Box("EatenCaloriesBox").Focus();
        Fixture.Pump();
        Assert.Equal(new[] { "571" }, copies);
        Assert.Contains("copied 571", f.Vm.StatusNotice);

        // Focusing an empty field copies nothing.
        f.Box("RecipeBox").Focus();
        Fixture.Pump();
        Assert.Equal(new[] { "571" }, copies);

        // An input member with a value copies too.
        f.Box("ServingGramsBox").Focus();
        Fixture.Pump();
        Assert.Equal(new[] { "571", "56" }, copies);
    }

    [AvaloniaFact]
    public void The_scale_panel_labels_its_two_weight_columns()
    {
        using var f = new Fixture();
        f.Window.UpdateLayout();

        var withCookware = f.Window.FindControl<TextBlock>("WithCookwareHeader")!;
        var foodOnly = f.Window.FindControl<TextBlock>("FoodOnlyHeader")!;
        Assert.Equal("WITH COOKWARE", withCookware.Text);
        Assert.Equal("FOOD ONLY", foodOnly.Text);
        Assert.True(withCookware.IsEffectivelyVisible);
        Assert.True(foodOnly.IsEffectivelyVisible);

        // Each header sits over its field column: the horizontal spans overlap.
        var gross = f.RowBox(0, "rowGross");
        var net = f.RowBox(0, "rowNet");
        var grossX = gross.TranslatePoint(new Avalonia.Point(0, 0), f.Window)!.Value.X;
        var netX = net.TranslatePoint(new Avalonia.Point(0, 0), f.Window)!.Value.X;
        var withX = withCookware.TranslatePoint(new Avalonia.Point(0, 0), f.Window)!.Value.X;
        var foodX = foodOnly.TranslatePoint(new Avalonia.Point(0, 0), f.Window)!.Value.X;
        Assert.InRange(withX, grossX, grossX + gross.Bounds.Width);
        Assert.InRange(foodX, netX, netX + net.Bounds.Width);
    }

    [AvaloniaFact]
    public void Reset_clears_fields_and_session_rows_but_not_pins()
    {
        using var f = new Fixture();

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.Vm.Scale.PromoteToSession("p3");
        Fixture.Pump();
        f.Type(f.Box("RecipeBox"), "1000");

        f.Vm.Reset();
        Fixture.Pump();

        Assert.Equal("", f.Box("ServingGramsBox").Text ?? "");
        Assert.Equal(new[] { "p1", "p2", CookwareRowModel.NoCookwareId }, f.Vm.Scale.Rows.Select(r => r.Id));
        Assert.All(f.Vm.Scale.Rows, r => Assert.Equal("", r.GrossText));
        Assert.Equal("", f.Vm.Scale.RecipeText);
        Assert.Null(f.Vm.Scale.DishRow);
        Assert.True(f.Vm.Scale.Rows[0].Pinned);
        // Reset committed the pending calculation first.
        Assert.Contains(f.Services.Log.Entries, e => e.Panel == LogPanel.Water && e.Result == "-200");
    }

    [AvaloniaFact]
    public void The_log_drawer_shows_committed_entries()
    {
        using var f = new Fixture();

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.Box("ServingCaloriesBox"), "250");
        f.Type(f.Box("EatenGramsBox"), "128");
        f.Press(Key.Enter); // commits the portion unit

        f.Vm.LogDrawerOpen = true;
        Fixture.Pump();

        var row = f.Vm.Log.Drawer.First();
        Assert.Equal("571 cal", row.Result);
        Assert.Equal("PORTION", row.Kind);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", row.Time);
        Assert.Single(f.Vm.Log.Recent);
        Assert.Equal(row.Time, f.Vm.Log.Recent.Single().Time);
    }

    [AvaloniaFact]
    public void Tab_walks_only_numeric_fields_in_logical_order()
    {
        using var f = new Fixture();

        f.Box("ServingGramsBox").Focus();
        Fixture.Pump();

        var stops = new List<string>();
        for (var i = 0; i < 9; i++)
        {
            f.Press(Key.Tab);
            var focused = f.FocusedBox();
            Assert.NotNull(focused);
            stops.Add(focused!.Name
                ?? (focused.Classes.Contains("rowGross") ? "rowGross" : focused.Classes.Contains("rowNet") ? "rowNet" : "?"));
        }

        Assert.Equal(
            new[]
            {
                "ServingCaloriesBox", "EatenGramsBox", "EatenCaloriesBox",
                "rowGross", "rowNet",  // Big pot
                "rowGross", "rowNet",  // Steel bowl
                "rowGross",            // No cookware (single field)
                "RecipeBox",
            },
            stops);
    }

    [AvaloniaFact]
    public void Escape_clears_only_the_focused_field()
    {
        using var f = new Fixture();

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.Box("ServingCaloriesBox"), "250");
        f.Box("ServingGramsBox").Focus();
        Fixture.Pump();
        f.Press(Key.Escape);

        Assert.Equal("", f.Box("ServingGramsBox").Text ?? "");
        Assert.Equal("250", f.Box("ServingCaloriesBox").Text);
    }

    [AvaloniaFact]
    public void Escape_on_a_computed_cookware_pair_member_is_a_no_op()
    {
        using var f = new Fixture();

        // Gross is the input, net is computed. Esc on the computed net must
        // not clear anything, flip the roles, or wipe the typed gross value.
        f.Type(f.RowBox(0, "rowGross"), "1440");
        f.RowBox(0, "rowNet").Focus();
        Fixture.Pump();
        f.Press(Key.Escape);

        Assert.Equal("1440", f.RowBox(0, "rowGross").Text);
        Assert.Equal("800", f.RowBox(0, "rowNet").Text);
        Assert.Equal(PairSide.A, f.Vm.Scale.Rows[0].Input);

        // The unit is still complete and commits normally.
        f.Vm.CommitUnit(MainViewModel.UnitOfRow(f.Vm.Scale.Rows[0]));
        Assert.Contains(f.Services.Log.Entries, e => e.Panel == LogPanel.Tare && e.Result == "800");

        // Esc on the input member still clears it.
        f.RowBox(0, "rowGross").Focus();
        Fixture.Pump();
        f.Press(Key.Escape);
        Assert.Equal("", f.RowBox(0, "rowGross").Text ?? "");
    }

    [AvaloniaFact]
    public void Escape_on_the_computed_eaten_member_keeps_the_typed_partner()
    {
        using var f = new Fixture();

        f.Type(f.Box("ServingGramsBox"), "56");
        f.Type(f.Box("ServingCaloriesBox"), "250");
        f.Type(f.Box("EatenGramsBox"), "128");
        Assert.Equal("571", f.Box("EatenCaloriesBox").Text);

        f.Box("EatenCaloriesBox").Focus();
        Fixture.Pump();
        f.Press(Key.Escape);

        Assert.Equal("128", f.Box("EatenGramsBox").Text);
        Assert.Equal("571", f.Box("EatenCaloriesBox").Text);
        Assert.Equal(PairSide.A, f.Vm.Portion.EatenInput);
    }

    [AvaloniaFact]
    public void Tabular_numerals_make_equal_length_numbers_equal_width()
    {
        using var f = new Fixture();

        var narrow = new FormattedTextBox(f.Box("ServingGramsBox"), "1111");
        var wide = new FormattedTextBox(f.Box("ServingGramsBox"), "9999");
        Assert.Equal(narrow.Width, wide.Width, 2);
    }

    [AvaloniaFact]
    public void Cookware_weight_field_rejects_minus_and_flags_invalid_text()
    {
        var settings = new Settings
        {
            Cookware = { new Cookware { Id = "p1", Name = "Big pot", Grams = 640 } },
        };
        var window = new CookwareWindow(settings);
        window.Show();
        Fixture.Pump();

        var editor = (CookwareEditorViewModel)window.DataContext!;
        var weightBox = window.GetVisualDescendants().OfType<TextBox>()
            .First(t => t.Classes.Contains("numeric"));

        // The minus key is rejected outright.
        weightBox.Focus();
        Fixture.Pump();
        weightBox.SelectAll();
        window.KeyTextInput("-");
        Fixture.Pump();
        Assert.Equal("640", weightBox.Text);
        Assert.Equal(640, settings.Cookware[0].Grams);

        // Invalid text is visibly flagged and never reaches the stored tare.
        weightBox.SelectAll();
        window.KeyTextInput("12x");
        Fixture.Pump();
        Assert.True(editor.Selected!.GramsInvalid);
        Assert.Contains("warn", weightBox.Classes);
        Assert.Equal(640, settings.Cookware[0].Grams);

        // Valid text clears the flag and updates the model.
        weightBox.SelectAll();
        window.KeyTextInput("600");
        Fixture.Pump();
        Assert.False(editor.Selected!.GramsInvalid);
        Assert.DoesNotContain("warn", weightBox.Classes);
        Assert.Equal(600, settings.Cookware[0].Grams);

        window.Close();
        Fixture.Pump();
    }

    private sealed class FormattedTextBox
    {
        public FormattedTextBox(TextBox box, string text)
        {
            var typeface = new Avalonia.Media.Typeface(box.FontFamily);
            var formatted = new Avalonia.Media.FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                typeface,
                box.FontSize,
                Avalonia.Media.Brushes.Black);
            if (box.FontFeatures is not null)
                formatted.SetFontFeatures(box.FontFeatures);
            Width = formatted.Width;
        }

        public double Width { get; }
    }
}
