namespace ZFood.Core;

/// <summary>
/// Locations of all persisted files. Everything lives in one per-user folder:
/// the platform application-data directory plus "ZFood" (~/.config/ZFood on
/// Linux, %APPDATA%\ZFood on Windows), overridable via the ZFOOD_DATA_DIR
/// environment variable.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string root) => Root = root;

    /// <summary>Resolves the default data directory, honoring ZFOOD_DATA_DIR.</summary>
    public static AppPaths Default()
    {
        var overridden = Environment.GetEnvironmentVariable("ZFOOD_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overridden))
            return new AppPaths(overridden);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new AppPaths(Path.Combine(appData, "ZFood"));
    }

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string LogFile => Path.Combine(Root, "log.jsonl");

    public string DiagnosticsFile => Path.Combine(Root, "diagnostics.log");

    /// <summary>Creates the data directory when missing.</summary>
    public void EnsureCreated() => Directory.CreateDirectory(Root);
}
