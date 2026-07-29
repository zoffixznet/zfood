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

    private static Button SaveButton(CookwareWindow window)
        => window.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Save"));

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
