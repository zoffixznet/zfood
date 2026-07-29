using Avalonia.Controls;
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
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => _vm.Add();

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => _vm.DeleteSelected();

    private void OnMoveUpClick(object? sender, RoutedEventArgs e) => _vm.MoveSelected(-1);

    private void OnMoveDownClick(object? sender, RoutedEventArgs e) => _vm.MoveSelected(1);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
