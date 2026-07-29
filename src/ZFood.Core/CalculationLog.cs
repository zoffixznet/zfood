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

    /// <summary>The entry duplicated its panel's newest entry and was skipped.</summary>
    DuplicateSkipped,
}

/// <summary>
/// The in-memory calculation log plus its commit rules: consecutive-duplicate
/// suppression (per panel group) and water-subsumes-tare. Persistence is
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
    /// Commits a settled calculation. A candidate identical to its panel group's
    /// newest entry writes nothing. A water entry removes the newest scale-group
    /// entry when that entry is a tare-only calculation from the same inputs.
    /// </summary>
    public CommitOutcome Commit(LogEntry entry)
    {
        var newestInGroup = _entries.LastOrDefault(entry.SameGroup);
        if (newestInGroup is not null && newestInGroup.SameCalculation(entry))
            return CommitOutcome.DuplicateSkipped;

        if (newestInGroup is not null && LogEntryFactory.WaterSubsumesTare(entry, newestInGroup))
        {
            _entries.Remove(newestInGroup);
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
