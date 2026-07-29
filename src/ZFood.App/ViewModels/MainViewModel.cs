using CommunityToolkit.Mvvm.ComponentModel;
using ZFood.App.Services;
using ZFood.Core;

namespace ZFood.App.ViewModels;

/// <summary>
/// The main window's coordinator: owns the two panels, the log, the status bar,
/// per-unit commit scheduling, Reset, and copy actions. The clipboard delegate
/// is injected by the view.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string? _focusedUnit;
    private int _noticeVersion;

    [ObservableProperty]
    private string statusEcho = "—";

    [ObservableProperty]
    private string statusTime = "";

    [ObservableProperty]
    private string statusNotice = "";

    [ObservableProperty]
    private bool statusNoticeIsWarning;

    [ObservableProperty]
    private bool logDrawerOpen;

    [ObservableProperty]
    private string moreCookwareHeader = "More cookware";

    [ObservableProperty]
    private bool moreCookwareVisible;

    [ObservableProperty]
    private bool moreCookwareOpen;

    [ObservableProperty]
    private string useDishNetLabel = "use dish net";

    [ObservableProperty]
    private bool canUseDishNet;

    public MainViewModel(AppServices services)
    {
        _services = services;
        Scale = new ScalePanelModel(services.Settings);
        Log = new LogViewModel(services.Log);

        Portion.Activity += () => Echo(Portion.LatestEcho);
        Scale.Activity += echo =>
        {
            Echo(echo);
            UpdateDishDependent();
        };
        Scale.Note += note => Notice(note, warning: true);
        Scale.DishRebound += (row, loud) =>
        {
            UpdateDishDependent();
            if (loud)
                Notice($"dish → {row?.Name ?? "—"}");
        };
        Scale.SettingsChanged += SaveSettings;
        Scale.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ScalePanelModel.AvailableCookware) or nameof(ScalePanelModel.DishCaption))
                UpdateDishDependent();
        };
        services.LogStore.WriteFailed += () => Notice("log write failed; this entry may not survive a restart", warning: true);

        UpdateDishDependent();
    }

    public PortionViewModel Portion { get; } = new();

    public ScalePanelModel Scale { get; }

    public LogViewModel Log { get; }

    /// <summary>Set by the view; puts text on the system clipboard.</summary>
    public Func<string, Task>? CopyToClipboard { get; set; }

    /// <summary>The commit-unit key for a cookware row's fields.</summary>
    public static string UnitOfRow(CookwareRowModel row) => LogEntryFactory.CookwareUnit(row.Id);

    /// <summary>
    /// Keyboard focus moved to a (possibly different) unit; commits the unit
    /// being left. Null means focus is outside any unit.
    /// </summary>
    public void NotifyFocusedUnit(string? unit)
    {
        if (unit == _focusedUnit)
            return;
        var leaving = _focusedUnit;
        _focusedUnit = unit;
        if (leaving is not null)
            CommitUnit(leaving);
    }

    /// <summary>Window deactivated: commit the focused unit and the dish row's unit.</summary>
    public void OnWindowDeactivated()
    {
        if (_focusedUnit is not null)
            CommitUnit(_focusedUnit);
        if (Scale.DishRow is CookwareRowModel dish)
            CommitUnit(UnitOfRow(dish));
    }

    /// <summary>Commits one unit's calculation if it changed and is complete.</summary>
    public void CommitUnit(string unit)
    {
        var now = DateTimeOffset.Now;
        if (unit == LogEntryFactory.PortionUnit)
        {
            if (!Portion.ChangedSinceCommit)
                return;
            if (Portion.TryBuildEntry(now) is LogEntry entry)
            {
                Settle(entry);
                Portion.ChangedSinceCommit = false;
            }
        }
        else
        {
            var row = Scale.Rows.FirstOrDefault(r => UnitOfRow(r) == unit);
            if (row is null || !row.ChangedSinceCommit)
                return;
            if (row.TryBuildEntry(now) is LogEntry entry)
            {
                Settle(entry);
                row.ChangedSinceCommit = false;
            }
        }
    }

    /// <summary>Commits every changed unit (Reset, window deactivation, app exit).</summary>
    public void CommitAllChanged()
    {
        CommitUnit(LogEntryFactory.PortionUnit);
        foreach (var row in Scale.Rows.ToList())
            CommitUnit(UnitOfRow(row));
    }

    /// <summary>
    /// Reset: commit pending work first (the log is the undo), then clear both
    /// panels. Pins, cookware settings, and the log survive.
    /// </summary>
    public void Reset()
    {
        CommitAllChanged();
        Portion.Reset();
        Scale.Reset();
        StatusEcho = "—";
        StatusTime = "";
        UpdateDishDependent();
        Notice("reset");
    }

    /// <summary>
    /// Commits the dish row's unit and copies the bare signed delta. Returns
    /// false while the water section is dormant.
    /// </summary>
    public async Task<bool> CopyDeltaAsync()
    {
        if (Scale.DishRow is not CookwareRowModel dish || !Scale.CanCopy)
            return false;

        CommitUnit(UnitOfRow(dish));
        var text = Scale.DeltaText;
        await CopyAsync(text);
        Notice($"copied {text} · {dish.Name}");
        return true;
    }

    /// <summary>Copies a log row's bare result number.</summary>
    public async Task CopyLogRowAsync(LogRow row)
    {
        await CopyAsync(row.CopyText);
        Notice($"copied {row.CopyText}");
    }

    /// <summary>Copies the dish row's net weight into eaten grams.</summary>
    public void UseDishNet()
    {
        if (Scale.DishRow?.NetValue is double net)
            Portion.UseAsEatenGrams(net);
    }

    /// <summary>Promotes a cookware item from the expander and returns its fresh session row.</summary>
    public CookwareRowModel? PromoteCookware(Cookware item)
    {
        var row = Scale.PromoteToSession(item.Id);
        UpdateDishDependent();
        return row;
    }

    private async Task CopyAsync(string text)
    {
        if (CopyToClipboard is Func<string, Task> copy)
            await copy(text);
    }

    private void Echo(string echo)
    {
        StatusEcho = echo;
        StatusTime = DateTime.Now.ToString("HH:mm:ss");
    }

    private void Settle(LogEntry entry)
    {
        if (_services.Log.Commit(entry) == CommitOutcome.Committed)
        {
            _services.Diagnostics.Write($"log commit: {entry.Panel} {entry.Result} {entry.ResultUnit} ({entry.Unit})");
            Notice($"logged {entry.Ts.LocalDateTime:HH:mm}");
        }
    }

    /// <summary>
    /// Persists settings, surfacing the quiet status-bar warning when the write
    /// fails. Every interactive settings mutation (pin toggles, the cookware
    /// editor) must save through here so a failure is never silent.
    /// </summary>
    public void SaveSettings()
    {
        if (!_services.SaveSettings())
            Notice("couldn't save settings; changes may not survive a restart", warning: true);
    }

    private async void Notice(string text, bool warning = false)
    {
        var version = ++_noticeVersion;
        StatusNotice = text;
        StatusNoticeIsWarning = warning;
        await Task.Delay(warning ? 5000 : 2500);
        if (version == _noticeVersion)
        {
            StatusNotice = "";
            StatusNoticeIsWarning = false;
        }
    }

    private void UpdateDishDependent()
    {
        var dish = Scale.DishRow;
        CanUseDishNet = dish?.NetValue is not null;
        UseDishNetLabel = dish is null ? "use dish net" : $"use {dish.Name} net";
        var available = Scale.AvailableCookware.Count;
        MoreCookwareHeader = $"More cookware ({available})";
        MoreCookwareVisible = available > 0;
    }
}
