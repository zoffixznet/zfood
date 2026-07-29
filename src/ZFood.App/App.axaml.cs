using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ZFood.App.Services;
using ZFood.App.ViewModels;
using ZFood.App.Views;

namespace ZFood.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // The default theme is present from the start so every resource
        // lookup resolves; the persisted choice is applied once settings load.
        ThemeManager.Apply(this, ThemeManager.DefaultTheme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.CreateDefault();
            ThemeManager.Apply(this, services.Settings.Theme);
            var viewModel = new MainViewModel(services);
            desktop.MainWindow = new MainWindow(viewModel, services);
            desktop.Exit += (_, _) =>
            {
                viewModel.CommitAllChanged();
                services.Diagnostics.Write("shutdown");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
