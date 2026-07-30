using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZFood.App.ViewModels;
using ZFood.App.Views;
using ZFood.Core;

namespace ZFood.Tests;

public class CookwareEditorTests
{
    private static Settings DemoSettings()
        => new() { Cookware = { new Cookware { Id = "p1", Name = "Big pot", Grams = 640 } } };

    [Fact]
    public void Save_reports_success_and_shows_the_confirmation_state()
    {
        var saves = 0;
        var vm = new CookwareEditorViewModel(DemoSettings(), () =>
        {
            saves++;
            return true;
        });

        Assert.True(vm.Save());
        Assert.Equal(1, saves);
        Assert.True(vm.Saved);
        Assert.Equal("", vm.Note);
    }

    [Fact]
    public void Failed_save_sets_the_warning_note_instead()
    {
        var vm = new CookwareEditorViewModel(DemoSettings(), () => false);

        Assert.False(vm.Save());
        Assert.False(vm.Saved);
        Assert.Contains("couldn't save", vm.Note);

        // A later successful save clears the warning.
        var ok = new CookwareEditorViewModel(DemoSettings(), () => true);
        ok.Save();
        Assert.Equal("", ok.Note);
    }

    [Fact]
    public void Add_appends_the_entry_and_clears_the_add_row_for_the_next_one()
    {
        var settings = DemoSettings();
        var vm = new CookwareEditorViewModel(settings, () => true);
        var selectedBefore = vm.Selected;

        vm.NewName = "  Sieve ";
        vm.NewGramsText = "120";
        Assert.True(vm.AddNew());

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("Sieve", vm.Items[1].Name);
        Assert.Equal(120, settings.Cookware[1].Grams);
        Assert.False(settings.Cookware[1].Pinned);
        Assert.Equal(1, settings.Cookware[1].Order);

        // The add row cleared and no edit interface opened for the new item.
        Assert.Equal("", vm.NewName);
        Assert.Equal("", vm.NewGramsText);
        Assert.Same(selectedBefore, vm.Selected);

        // An empty weight is a valid tare-0 entry (a plate, a sheet of paper).
        vm.NewName = "Plate";
        Assert.True(vm.AddNew());
        Assert.Equal(0, settings.Cookware[2].Grams);
    }

    [Fact]
    public void Add_refuses_blank_names_and_unparseable_weights()
    {
        var vm = new CookwareEditorViewModel(DemoSettings(), () => true);

        vm.NewName = "   ";
        Assert.False(vm.AddNew());
        Assert.Contains("name", vm.Note);
        Assert.Single(vm.Items);

        vm.NewName = "Sieve";
        vm.NewGramsText = "12x";
        Assert.True(vm.NewGramsInvalid);
        Assert.False(vm.AddNew());
        Assert.Single(vm.Items);

        // Correcting the weight clears the flag and the add succeeds.
        vm.NewGramsText = "120";
        Assert.False(vm.NewGramsInvalid);
        Assert.True(vm.AddNew());
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("", vm.Note);
    }

    private static Button SaveButton(CookwareWindow window)
        => window.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Save"));

    [AvaloniaFact]
    public void The_add_button_appends_to_the_list_and_clears_the_add_row()
    {
        var window = new CookwareWindow(DemoSettings(), () => true);
        window.Show();
        Pump();

        var name = window.FindControl<TextBox>("NewNameBox")!;
        var weight = window.FindControl<TextBox>("NewWeightBox")!;
        name.Text = "Sieve";
        weight.Text = "120";
        window.FindControl<Button>("AddButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        var vm = (CookwareEditorViewModel)window.DataContext!;
        Assert.Equal(new[] { "Big pot", "Sieve" }, vm.Items.Select(i => i.Name));
        Assert.Equal("", name.Text ?? "");
        Assert.Equal("", weight.Text ?? "");

        window.Close();
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Save_button_persists_shows_the_confirmation_then_closes()
    {
        var saves = 0;
        var window = new CookwareWindow(DemoSettings(), () =>
        {
            saves++;
            return true;
        });
        window.Show();
        Pump();

        // With a long close delay the confirmation is observable first.
        CookwareWindow.CloseDelay = TimeSpan.FromSeconds(30);
        SaveButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        Assert.Equal(1, saves);
        Assert.True(window.IsVisible); // still showing the confirmation
        Assert.True(window.FindControl<TextBlock>("SavedText")!.IsVisible);

        window.Close();
        Pump();
    }

    [AvaloniaFact]
    public void Save_button_closes_the_dialog_once_the_confirmation_delay_elapses()
    {
        var window = new CookwareWindow(DemoSettings(), () => true);
        window.Show();
        Pump();

        CookwareWindow.CloseDelay = TimeSpan.Zero;
        SaveButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void Failed_save_keeps_the_dialog_open_with_the_warning()
    {
        var window = new CookwareWindow(DemoSettings(), () => false);
        window.Show();
        Pump();

        CookwareWindow.CloseDelay = TimeSpan.Zero;
        SaveButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        Assert.True(window.IsVisible);
        var vm = (CookwareEditorViewModel)window.DataContext!;
        Assert.False(vm.Saved);
        Assert.Contains("couldn't save", vm.Note);
        Assert.False(window.FindControl<TextBlock>("SavedText")!.IsVisible);

        window.Close();
        Pump();
    }
}
