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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.CreateDefault();
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
