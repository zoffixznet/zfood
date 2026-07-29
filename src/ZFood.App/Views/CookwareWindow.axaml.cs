using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.App.Views;

public partial class CookwareWindow : Window
{
    private readonly CookwareEditorViewModel _vm;
    private readonly Settings _settings;
    private bool _closing;
    private bool _initializingTheme;

    /// <summary>
    /// Runtime-loader and designer entry point over blank settings; the app
    /// itself always passes the live settings object.
    /// </summary>
    public CookwareWindow()
        : this(new Settings())
    {
    }

    public CookwareWindow(Settings settings, Func<bool>? save = null)
    {
        InitializeComponent();
        _settings = settings;
        _vm = new CookwareEditorViewModel(settings, save);
        DataContext = _vm;

        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        InitializeThemePicker();
    }

    // -- Theme picker --------------------------------------------------------

    private void InitializeThemePicker()
    {
        _initializingTheme = true;
        var current = ThemeManager.Normalize(_settings.Theme);
        foreach (var radio in ThemeRadios())
            radio.IsChecked = (string?)radio.Tag == current;
        _initializingTheme = false;
    }

    private IEnumerable<RadioButton> ThemeRadios()
        => new[] { ThemePorcelain, ThemeAurora, ThemeJuiceBar, ThemeGlossy };

    private void OnThemeChecked(object? sender, RoutedEventArgs e)
    {
        // Applies instantly, live; the choice persists with the settings on
        // Save (or via the owner's save-on-close path).
        if (_initializingTheme
            || sender is not RadioButton { IsChecked: true, Tag: string theme }
            || Application.Current is not Application app)
        {
            return;
        }

        _settings.Theme = theme;
        ThemeManager.Apply(app, theme);
    }

    /// <summary>How long the inline saved confirmation stays visible before the dialog closes.</summary>
    public static TimeSpan CloseDelay { get; set; } = TimeSpan.FromMilliseconds(650);

    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        // Same rule as the main window's fields: nothing the user enters is
        // physically negative, so the minus key is rejected in numeric fields.
        if (e.Source is TextBox box && box.Classes.Contains("numeric") && e.Text?.Contains('-') == true)
            e.Handled = true;
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => _vm.Add();

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => _vm.DeleteSelected();

    private void OnMoveUpClick(object? sender, RoutedEventArgs e) => _vm.MoveSelected(-1);

    private void OnMoveDownClick(object? sender, RoutedEventArgs e) => _vm.MoveSelected(1);

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        // On failure the dialog stays open with the warning note so nothing
        // is lost silently; closing through the window decoration still saves
        // through the owner as before.
        if (_closing || !_vm.Save())
            return;
        _closing = true;

        // Leave the inline confirmation visible for a beat, then close.
        await Task.Delay(CloseDelay);
        Close();
    }
}
