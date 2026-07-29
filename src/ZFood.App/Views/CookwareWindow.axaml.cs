using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.App.Views;

public partial class CookwareWindow : Window
{
    private readonly CookwareEditorViewModel _vm;
    private bool _closing;

    public CookwareWindow(Settings settings, Func<bool>? save = null)
    {
        InitializeComponent();
        _vm = new CookwareEditorViewModel(settings, save);
        DataContext = _vm;

        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
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
