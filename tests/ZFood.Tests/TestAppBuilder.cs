using Avalonia;
using Avalonia.Headless;
using ZFood.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace ZFood.Tests;

public class TestAppBuilder
{
    static TestAppBuilder()
    {
        // Keep any application-level startup away from the real user profile.
        Environment.SetEnvironmentVariable("ZFOOD_DATA_DIR",
            Path.Combine(Path.GetTempPath(), "zfood-tests-app-" + Guid.NewGuid().ToString("N")));
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ZFood.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
