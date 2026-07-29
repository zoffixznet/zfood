using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.App.Views;

public partial class CookwareWindow : Window
{
    private readonly CookwareEditorViewModel _vm;

    public CookwareWindow(Settings settings)
    {
        InitializeComponent();
        _vm = new CookwareEditorViewModel(settings);
        DataContext = _vm;

        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
    }

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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
