using System;
using Avalonia;

namespace ZFood.App;

class Program
{
    // Avalonia initialization must all happen via BuildAvaloniaApp; do not touch
    // Avalonia APIs before StartWithClassicDesktopLifetime runs.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
