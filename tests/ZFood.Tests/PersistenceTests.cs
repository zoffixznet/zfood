using ZFood.Core;

namespace ZFood.Tests;

public sealed class TempDir : IDisposable
{
    public TempDir() => Directory.CreateDirectory(Path);

    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zfood-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public class SettingsStoreTests
{
    private static SettingsStore Store(TempDir dir) => new(new AppPaths(dir.Path), DiagnosticsLog.Null);

    [Fact]
    public void Missing_file_yields_defaults()
    {
        using var dir = new TempDir();
        var settings = Store(dir).Load();
        Assert.Empty(settings.Cookware);
        Assert.Null(settings.Window);
    }

    [Fact]
    public void Settings_round_trip_preserves_everything()
    {
        using var dir = new TempDir();
        var store = Store(dir);
        var settings = new Settings
        {
            Window = new WindowGeometry { X = 10, Y = 20, Width = 800, Height = 600, Maximized = true },
            Cookware = { new Cookware { Name = "Big pot", Grams = 640.5, Pinned = true, Order = 3 } },
        };

        Assert.True(store.Save(settings));
        var loaded = store.Load();

        Assert.Equal(10, loaded.Window!.X);
        Assert.Equal(20, loaded.Window.Y);
        Assert.Equal(800, loaded.Window.Width);
        Assert.Equal(600, loaded.Window.Height);
        Assert.True(loaded.Window.Maximized);
        var item = Assert.Single(loaded.Cookware);
        Assert.Equal("Big pot", item.Name);
        Assert.Equal(640.5, item.Grams);
        Assert.True(item.Pinned);
        Assert.Equal(3, item.Order);
    }

    [Theory]
    [InlineData("{ not json at all")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    public void Corrupt_file_is_moved_aside_and_replaced_with_defaults(string content)
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.SettingsFile, content);

        var settings = Store(dir).Load();

        Assert.Empty(settings.Cookware);
        Assert.True(File.Exists(paths.SettingsFile + ".bak"));
        Assert.False(File.Exists(paths.SettingsFile));
    }

    [Fact]
    public void Out_of_range_values_are_sanitized_not_fatal()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.SettingsFile,
            """{ "version": 1, "cookware": [ { "id": "", "name": null, "grams": -5 } ] }""");

        var settings = Store(dir).Load();

        var item = Assert.Single(settings.Cookware);
        Assert.Equal(0, item.Grams);
        Assert.Equal("", item.Name);
        Assert.NotEqual("", item.Id);
    }

    [Fact]
    public void Save_into_an_unwritable_location_reports_failure_without_throwing()
    {
        var store = new SettingsStore(new AppPaths("/proc/zfood-cannot-write-here"), DiagnosticsLog.Null);
        Assert.False(store.Save(new Settings()));
    }
}

public class LogStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private static LogStore Store(TempDir dir) => new(new AppPaths(dir.Path), DiagnosticsLog.Null);

    private static LogEntry Entry(DateTimeOffset ts, string result = "496")
        => new()
        {
            Ts = ts,
            Panel = LogPanel.Portion,
            Unit = LogEntryFactory.PortionUnit,
            Result = result,
            ResultUnit = "cal",
            Equation = "250 g = 775 cal (3.10 cal/g) · eaten 160 g",
            Inputs = new Dictionary<string, string> { ["servingG"] = "250" },
        };

    [Fact]
    public void Log_round_trips_through_jsonl()
    {
        using var dir = new TempDir();
        var store = Store(dir);
        store.Append(Entry(Now.AddMinutes(-5)));
        store.Append(Entry(Now.AddMinutes(-1), result: "800"));

        var loaded = store.Load(Now);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("496", loaded[0].Result);
        Assert.Equal("800", loaded[1].Result);
        Assert.Equal(LogPanel.Portion, loaded[0].Panel);
        Assert.Equal("250", loaded[0].Inputs["servingG"]);
    }

    [Fact]
    public void Missing_file_loads_empty()
    {
        using var dir = new TempDir();
        Assert.Empty(Store(dir).Load(Now));
    }

    [Fact]
    public void Old_entries_are_pruned_on_load()
    {
        using var dir = new TempDir();
        var store = Store(dir);
        store.Append(Entry(Now.AddDays(-20)));
        store.Append(Entry(Now.AddDays(-1)));

        var loaded = store.Load(Now);

        var survivor = Assert.Single(loaded);
        Assert.Equal(Now.AddDays(-1), survivor.Ts);
        // The pruned file no longer contains the old entry.
        Assert.Single(store.Load(Now));
    }

    [Fact]
    public void Entry_count_is_capped_on_load()
    {
        using var dir = new TempDir();
        var store = Store(dir);
        var total = LogStore.MaxEntries + 25;
        for (var i = 0; i < total; i++)
            store.Append(Entry(Now.AddMinutes(i - total), result: i.ToString()));

        var loaded = store.Load(Now);

        Assert.Equal(LogStore.MaxEntries, loaded.Count);
        Assert.Equal((total - 1).ToString(), loaded[^1].Result); // newest survives
        Assert.Equal("25", loaded[0].Result); // oldest 25 dropped
    }

    [Fact]
    public void Corrupt_lines_are_recovered_from_and_the_damaged_file_moved_aside()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var store = Store(dir);
        store.Append(Entry(Now.AddMinutes(-10)));
        File.AppendAllText(paths.LogFile, "{{{ definitely not json\n");
        store.Append(Entry(Now.AddMinutes(-1), result: "800"));

        var loaded = store.Load(Now);

        Assert.Equal(2, loaded.Count);
        Assert.True(File.Exists(paths.LogFile + ".bak"));
        // The rewritten file is clean now.
        Assert.Equal(2, store.Load(Now).Count);
    }

    [Theory]
    [InlineData("""{"ts":"2026-07-20T11:59:00+00:00","panel":"portion","unit":"portion","result":"496","resultUnit":"cal","equation":"x","inputs":null}""")]
    [InlineData("""{"ts":"2026-07-20T11:59:00+00:00","panel":"portion","unit":null,"result":"496","resultUnit":"cal","equation":"x","inputs":{}}""")]
    [InlineData("""{"ts":"2026-07-20T11:59:00+00:00","panel":"portion","unit":"portion","result":null,"resultUnit":"cal","equation":"x","inputs":{}}""")]
    public void Lines_with_null_required_fields_are_treated_as_corrupt(string damagedLine)
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var store = Store(dir);
        store.Append(Entry(Now.AddMinutes(-10)));
        File.AppendAllText(paths.LogFile, damagedLine + "\n");

        var loaded = store.Load(Now);

        var survivor = Assert.Single(loaded);
        Assert.NotNull(survivor.Inputs);
        Assert.True(File.Exists(paths.LogFile + ".bak"));
        // The rewritten file no longer contains the damaged line.
        Assert.Single(store.Load(Now));
    }

    [Fact]
    public void Log_entries_written_by_earlier_versions_still_load_and_dedupe()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var store = Store(dir);
        // A verbatim line in the on-disk format used before the cookware
        // naming: the unit carries the "pot:" prefix and the inputs use the
        // "pot" key. It must load unchanged, never be treated as corrupt.
        File.WriteAllText(paths.LogFile,
            """{"ts":"2026-07-28T14:40:12+00:00","panel":"tare","unit":"pot:row1","result":"800","resultUnit":"g","equation":"Big pot (640 g) · gross 1440 g → net","inputs":{"pot":"Big pot","tare":"640","side":"gross","value":"1440"}}"""
            + "\n");

        var loaded = store.Load(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));

        var entry = Assert.Single(loaded);
        Assert.Equal("pot:row1", entry.Unit);
        Assert.False(File.Exists(paths.LogFile + ".bak"));

        // A freshly built identical calculation lands in the same unit and is
        // suppressed as a consecutive duplicate.
        var log = new CalculationLog(store, loaded);
        var fresh = LogEntryFactory.Tare(
            new DateTimeOffset(2026, 7, 29, 9, 0, 0, TimeSpan.Zero), "row1", "Big pot", 640, PairSide.A, 1440, 800);
        Assert.Equal(CommitOutcome.DuplicateSkipped, log.Commit(fresh));
    }

    [Fact]
    public void Append_failure_raises_the_warning_event_without_throwing()
    {
        var store = new LogStore(new AppPaths("/proc/zfood-cannot-write-here"), DiagnosticsLog.Null);
        var warned = false;
        store.WriteFailed += () => warned = true;

        store.Append(Entry(Now));

        Assert.True(warned);
    }
}
