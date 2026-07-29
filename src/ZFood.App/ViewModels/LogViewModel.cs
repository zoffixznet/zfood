using System.Collections.ObjectModel;
using System.Globalization;
using ZFood.Core;

namespace ZFood.App.ViewModels;

/// <summary>One rendered log line: result first, then the equation for provenance.</summary>
public sealed record LogRow(string Time, string Result, string Kind, string Equation, string CopyText);

/// <summary>A day separator inside the log drawer.</summary>
public sealed record LogDayHeader(string Label);

/// <summary>
/// Renders the calculation log for the UI: a collapsed strip with the last
/// three entries, and a drawer with everything grouped by day, newest first.
/// </summary>
public sealed class LogViewModel
{
    private readonly CalculationLog _log;

    public LogViewModel(CalculationLog log)
    {
        _log = log;
        _log.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The last three entries, newest first (collapsed strip).</summary>
    public ObservableCollection<LogRow> Recent { get; } = new();

    /// <summary>All entries newest first with day headers (expanded drawer).</summary>
    public ObservableCollection<object> Drawer { get; } = new();

    /// <summary>True when the log has no entries yet.</summary>
    public bool IsEmpty => _log.Entries.Count == 0;

    public event Action? Changed;

    private void Rebuild()
    {
        Recent.Clear();
        Drawer.Clear();

        var newestFirst = _log.Entries.Reverse().ToList();
        foreach (var entry in newestFirst.Take(3))
            Recent.Add(Render(entry));

        DateTime? day = null;
        foreach (var entry in newestFirst)
        {
            var entryDay = entry.Ts.LocalDateTime.Date;
            if (day != entryDay)
            {
                day = entryDay;
                Drawer.Add(new LogDayHeader(DayLabel(entryDay)));
            }

            Drawer.Add(Render(entry));
        }

        Changed?.Invoke();
    }

    private static LogRow Render(LogEntry entry)
        => new(
            Time: entry.Ts.LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            Result: $"{entry.Result} {entry.ResultUnit}",
            Kind: entry.Panel.ToString().ToUpperInvariant(),
            Equation: entry.Equation,
            CopyText: entry.Result);

    private static string DayLabel(DateTime day)
    {
        var today = DateTime.Now.Date;
        if (day == today)
            return "Today";
        if (day == today.AddDays(-1))
            return "Yesterday";
        return day.ToString("ddd yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
