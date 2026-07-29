using System;
using System.Threading.Tasks;
using Avalonia;
using ZFood.Core;

namespace ZFood.App;

class Program
{
    // Avalonia initialization must all happen via BuildAvaloniaApp; do not touch
    // Avalonia APIs before StartWithClassicDesktopLifetime runs.
    [STAThread]
    public static int Main(string[] args)
    {
        // Last-resort guard: anything that escapes every inner failure handler
        // is recorded to the diagnostics log instead of surfacing as a raw
        // crash, so a failure can always be diagnosed from the data directory.
        var diagnostics = new DiagnosticsLog(AppPaths.Default().DiagnosticsFile);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            diagnostics.Write($"unhandled exception: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            diagnostics.Write($"unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception e)
        {
            diagnostics.Write($"unhandled exception: {e}");
            return 1;
        }
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
