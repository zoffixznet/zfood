namespace ZFood.Core;

/// <summary>Where committed log entries are persisted.</summary>
public interface ILogSink
{
    /// <summary>Appends one entry.</summary>
    void Append(LogEntry entry);

    /// <summary>Replaces the persisted log with the given entries (used after a subsumption).</summary>
    void Rewrite(IReadOnlyList<LogEntry> entries);
}

/// <summary>Outcome of a commit attempt.</summary>
public enum CommitOutcome
{
    /// <summary>The entry was written.</summary>
    Committed,

    /// <summary>The entry duplicated its unit's newest entry and was skipped.</summary>
    DuplicateSkipped,
}

/// <summary>
/// The in-memory calculation log plus its commit rules: per-unit
/// consecutive-duplicate suppression (so alternating between two cookware rows cannot
/// flood the log) and water-subsumes-tare within one row's unit. Persistence is
/// delegated to an <see cref="ILogSink"/>.
/// </summary>
public sealed class CalculationLog
{
    private readonly List<LogEntry> _entries = new();
    private readonly ILogSink? _sink;

    public CalculationLog(ILogSink? sink = null, IEnumerable<LogEntry>? existing = null)
    {
        _sink = sink;
        if (existing is not null)
            _entries.AddRange(existing);
    }

    /// <summary>All entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>Raised after the entry list changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Commits a settled calculation. A candidate identical to its own unit's
    /// newest entry writes nothing. A water entry replaces its unit's newest
    /// entry when that entry is the tare-only calculation from the same inputs
    /// (the water line already records gross, tare, and net).
    /// </summary>
    public CommitOutcome Commit(LogEntry entry)
    {
        var newestOfUnit = _entries.LastOrDefault(e => e.Unit == entry.Unit);
        if (newestOfUnit is not null && newestOfUnit.SameCalculation(entry))
            return CommitOutcome.DuplicateSkipped;

        if (newestOfUnit is not null && LogEntryFactory.WaterSubsumesTare(entry, newestOfUnit))
        {
            _entries.Remove(newestOfUnit);
            _entries.Add(entry);
            _sink?.Rewrite(_entries);
        }
        else
        {
            _entries.Add(entry);
            _sink?.Append(entry);
        }

        Changed?.Invoke();
        return CommitOutcome.Committed;
    }
}
