using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZFood.Core;

/// <summary>
/// Persists the calculation log as append-only JSONL beside the settings file.
/// On load it prunes old entries (14 days / 500 entries) and recovers from
/// corruption by moving the damaged file aside and rewriting the readable
/// entries. Failed writes are reported through <see cref="WriteFailed"/> so the
/// UI can show a quiet warning; nothing here ever throws.
/// </summary>
public sealed class LogStore : ILogSink
{
    public const int MaxAgeDays = 14;
    public const int MaxEntries = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly AppPaths _paths;
    private readonly DiagnosticsLog _diag;

    public LogStore(AppPaths paths, DiagnosticsLog diag)
    {
        _paths = paths;
        _diag = diag;
    }

    /// <summary>Raised when an append or rewrite fails.</summary>
    public event Action? WriteFailed;

    /// <summary>Loads, prunes, and (when anything was dropped or damaged) rewrites the log.</summary>
    public List<LogEntry> Load() => Load(DateTimeOffset.Now);

    /// <summary>Load with an injectable clock for pruning tests.</summary>
    public List<LogEntry> Load(DateTimeOffset now)
    {
        var entries = new List<LogEntry>();
        var damaged = false;

        try
        {
            if (!File.Exists(_paths.LogFile))
                return entries;

            foreach (var line in File.ReadAllLines(_paths.LogFile))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line, JsonOptions);
                    if (entry is null)
                        damaged = true;
                    else
                        entries.Add(entry);
                }
                catch (JsonException)
                {
                    damaged = true;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _diag.Write($"log: unreadable ({e.GetType().Name}), moving aside");
            AtomicFile.MoveAside(_paths.LogFile);
            return new List<LogEntry>();
        }

        var cutoff = now.AddDays(-MaxAgeDays);
        var pruned = entries.Where(e => e.Ts >= cutoff).ToList();
        if (pruned.Count > MaxEntries)
            pruned.RemoveRange(0, pruned.Count - MaxEntries);

        if (damaged)
        {
            AtomicFile.MoveAside(_paths.LogFile);
            _diag.Write($"log: corrupt lines found, moved aside, kept {pruned.Count} readable entries");
            Rewrite(pruned);
        }
        else if (pruned.Count != entries.Count)
        {
            _diag.Write($"log: pruned {entries.Count - pruned.Count} old entries");
            Rewrite(pruned);
        }

        return pruned;
    }

    /// <summary>Appends one committed entry.</summary>
    public void Append(LogEntry entry)
    {
        try
        {
            _paths.EnsureCreated();
            File.AppendAllText(_paths.LogFile, JsonSerializer.Serialize(entry, JsonOptions) + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _diag.Write($"log: append failed ({e.GetType().Name}: {e.Message})");
            WriteFailed?.Invoke();
        }
    }

    /// <summary>Atomically replaces the whole log file.</summary>
    public void Rewrite(IReadOnlyList<LogEntry> entries)
    {
        try
        {
            _paths.EnsureCreated();
            var lines = entries.Select(e => JsonSerializer.Serialize(e, JsonOptions));
            AtomicFile.Write(_paths.LogFile, string.Join("\n", lines) + (entries.Count > 0 ? "\n" : ""));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _diag.Write($"log: rewrite failed ({e.GetType().Name}: {e.Message})");
            WriteFailed?.Invoke();
        }
    }
}
