using System.Text.Json;

namespace ZFood.Core;

/// <summary>
/// Loads and saves settings.json. A missing file yields defaults; a corrupt file
/// is moved aside to .bak and replaced with defaults; a failed save reports
/// false so the UI can show a quiet warning. Nothing here ever throws.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly AppPaths _paths;
    private readonly DiagnosticsLog _diag;

    public SettingsStore(AppPaths paths, DiagnosticsLog diag)
    {
        _paths = paths;
        _diag = diag;
    }

    /// <summary>Loads settings, recovering from a missing or corrupt file.</summary>
    public Settings Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                _diag.Write("settings: no file, using defaults");
                return new Settings();
            }

            var text = File.ReadAllText(_paths.SettingsFile);
            var settings = JsonSerializer.Deserialize<Settings>(text, JsonOptions);
            if (settings is null)
            {
                MoveCorruptAside("null document");
                return new Settings();
            }

            _diag.Write("settings: loaded");
            return settings.Sanitized();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            MoveCorruptAside(e.GetType().Name);
            return new Settings();
        }
    }

    /// <summary>Saves settings atomically. Returns false (and logs) on failure.</summary>
    public bool Save(Settings settings)
    {
        try
        {
            _paths.EnsureCreated();
            AtomicFile.Write(_paths.SettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
            _diag.Write("settings: saved");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _diag.Write($"settings: save failed ({e.GetType().Name}: {e.Message})");
            return false;
        }
    }

    private void MoveCorruptAside(string reason)
    {
        var bak = AtomicFile.MoveAside(_paths.SettingsFile);
        _diag.Write($"settings: corrupt ({reason}), moved aside to {bak ?? "nowhere"}, using defaults");
    }
}
