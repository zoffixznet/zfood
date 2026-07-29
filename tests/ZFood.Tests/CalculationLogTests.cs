using ZFood.Core;

namespace ZFood.Tests;

public class CalculationLogTests
{
    private sealed class FakeSink : ILogSink
    {
        public readonly List<LogEntry> Appended = new();
        public List<LogEntry>? Rewritten;

        public void Append(LogEntry entry) => Appended.Add(entry);

        public void Rewrite(IReadOnlyList<LogEntry> entries) => Rewritten = entries.ToList();
    }

    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 14, 30, 0, TimeSpan.Zero);

    private static LogEntry PortionEntry(double eatenGrams = 160)
        => LogEntryFactory.Portion(T0, 250, 775, PairSide.A, eatenGrams, 3.1 * eatenGrams, 3.1);

    private static LogEntry TareEntry(double gross = 1440)
        => LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.A, gross, gross - 640);

    private static LogEntry WaterEntry(double gross = 1440, double recipe = 1000)
        => LogEntryFactory.Water(T0, "Big pot", 640, PairSide.A, gross, gross - 640, recipe, gross - 640 - recipe);

    [Fact]
    public void Commit_appends_and_notifies()
    {
        var sink = new FakeSink();
        var log = new CalculationLog(sink);
        var changed = 0;
        log.Changed += () => changed++;

        Assert.Equal(CommitOutcome.Committed, log.Commit(PortionEntry()));
        Assert.Single(log.Entries);
        Assert.Single(sink.Appended);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Identical_consecutive_settle_writes_nothing()
    {
        var sink = new FakeSink();
        var log = new CalculationLog(sink);
        log.Commit(PortionEntry());

        Assert.Equal(CommitOutcome.DuplicateSkipped, log.Commit(PortionEntry()));
        Assert.Single(log.Entries);
        Assert.Single(sink.Appended);
    }

    [Fact]
    public void Duplicate_check_is_per_panel_group()
    {
        var log = new CalculationLog();
        log.Commit(PortionEntry());
        log.Commit(TareEntry());

        // The portion entry is no longer the portion group's newest? It is; a
        // repeat of it is still suppressed even though a scale entry landed later.
        Assert.Equal(CommitOutcome.DuplicateSkipped, log.Commit(PortionEntry()));

        // A changed calculation in the same group commits.
        Assert.Equal(CommitOutcome.Committed, log.Commit(PortionEntry(eatenGrams: 200)));
    }

    [Fact]
    public void Water_commit_subsumes_the_tare_entry_from_the_same_inputs()
    {
        var sink = new FakeSink();
        var log = new CalculationLog(sink);
        log.Commit(TareEntry());
        log.Commit(WaterEntry());

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogPanel.Water, entry.Panel);
        Assert.NotNull(sink.Rewritten);
        Assert.Single(sink.Rewritten!);
    }

    [Fact]
    public void Water_commit_subsumes_across_an_intervening_portion_entry()
    {
        var log = new CalculationLog();
        log.Commit(TareEntry());
        log.Commit(PortionEntry());
        log.Commit(WaterEntry());

        Assert.Equal(2, log.Entries.Count);
        Assert.DoesNotContain(log.Entries, e => e.Panel == LogPanel.Tare);
    }

    [Fact]
    public void Water_does_not_subsume_a_tare_entry_with_different_inputs()
    {
        var log = new CalculationLog();
        log.Commit(TareEntry(gross: 900));
        log.Commit(WaterEntry(gross: 1440));

        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public void Existing_entries_seed_the_duplicate_check()
    {
        var log = new CalculationLog(sink: null, existing: new[] { PortionEntry() });
        Assert.Equal(CommitOutcome.DuplicateSkipped, log.Commit(PortionEntry()));
    }
}

public class LogEntryFactoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Portion_forward_records_calories_result_and_full_equation()
    {
        var e = LogEntryFactory.Portion(T0, 250, 775, PairSide.A, 160, 496, 3.1);
        Assert.Equal("496", e.Result);
        Assert.Equal("cal", e.Unit);
        Assert.Contains("250 g = 775 cal (3.10 cal/g)", e.Equation);
        Assert.Contains("eaten 160 g", e.Equation);
    }

    [Fact]
    public void Portion_reverse_records_grams_result()
    {
        var e = LogEntryFactory.Portion(T0, 56, 250, PairSide.B, 56, 250, 250.0 / 56);
        Assert.Equal("56", e.Result);
        Assert.Equal("g", e.Unit);
        Assert.Contains("budget 250 cal", e.Equation);
    }

    [Fact]
    public void Tare_forward_records_net_result()
    {
        var e = LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.A, 1440, 800);
        Assert.Equal("800", e.Result);
        Assert.Contains("Big pot (640 g)", e.Equation);
        Assert.Contains("gross 1440 g", e.Equation);
    }

    [Fact]
    public void Tare_reverse_records_target_gross_result()
    {
        var e = LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.B, 840, 200);
        Assert.Equal("840", e.Result);
        Assert.Contains("net 200 g", e.Equation);
    }

    [Fact]
    public void Water_records_signed_delta_and_the_whole_pipeline()
    {
        var e = LogEntryFactory.Water(T0, "Big pot", 640, PairSide.A, 1440, 800, 1000, -200);
        Assert.Equal("-200", e.Result);
        Assert.Equal("g", e.Unit);
        Assert.Contains("gross 1440 g", e.Equation);
        Assert.Contains("net 800 g", e.Equation);
        Assert.Contains("recipe 1000 g", e.Equation);
    }

    [Fact]
    public void Water_subsumes_tare_only_when_all_inputs_match()
    {
        var water = LogEntryFactory.Water(T0, "Big pot", 640, PairSide.A, 1440, 800, 1000, -200);
        var sameTare = LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.A, 1440, 800);
        var otherGross = LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.A, 900, 260);
        var otherPot = LogEntryFactory.Tare(T0, "Small pan", 396, PairSide.A, 1440, 1044);
        var otherSide = LogEntryFactory.Tare(T0, "Big pot", 640, PairSide.B, 1440, 800);

        Assert.True(LogEntryFactory.WaterSubsumesTare(water, sameTare));
        Assert.False(LogEntryFactory.WaterSubsumesTare(water, otherGross));
        Assert.False(LogEntryFactory.WaterSubsumesTare(water, otherPot));
        Assert.False(LogEntryFactory.WaterSubsumesTare(water, otherSide));
    }
}
