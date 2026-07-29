using ZFood.Core;

namespace ZFood.App.Services;

/// <summary>
/// The app's shared state and persistence, created once at startup. All files
/// live in the per-user application-data directory under ZFood (overridable via
/// ZFOOD_DATA_DIR), same code path on every OS.
/// </summary>
public sealed class AppServices
{
    private AppServices(AppPaths paths)
    {
        Paths = paths;
        Diagnostics = new DiagnosticsLog(paths.DiagnosticsFile);
        SettingsStore = new SettingsStore(paths, Diagnostics);
        Settings = SettingsStore.Load();
        LogStore = new LogStore(paths, Diagnostics);
        Log = new CalculationLog(LogStore, LogStore.Load());
    }

    public AppPaths Paths { get; }

    public DiagnosticsLog Diagnostics { get; }

    public SettingsStore SettingsStore { get; }

    public Settings Settings { get; }

    public LogStore LogStore { get; }

    public CalculationLog Log { get; }

    /// <summary>Creates services over the default (or ZFOOD_DATA_DIR) data directory.</summary>
    public static AppServices CreateDefault() => Create(AppPaths.Default());

    /// <summary>Creates services over an explicit data directory.</summary>
    public static AppServices Create(AppPaths paths)
    {
        try
        {
            paths.EnsureCreated();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A data directory that cannot be created leaves every store in its
            // failure-tolerant path; the app still runs, with a status warning
            // the first time a write fails.
        }

        var services = new AppServices(paths);
        services.Diagnostics.Write("startup");
        return services;
    }

    /// <summary>Persists settings; false means the write failed (show a quiet warning).</summary>
    public bool SaveSettings() => SettingsStore.Save(Settings);
}
