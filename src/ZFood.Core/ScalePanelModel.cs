using System.Collections.ObjectModel;

namespace ZFood.Core;

/// <summary>
/// The SCALE panel: one row per pinned cookware item, session rows promoted from the
/// unpinned tail, the permanent no-cookware row last, and the water section bound to
/// the dish row through the <see cref="DishLatch"/>. Owns row lifecycle (pins,
/// promotions, CRUD reconciliation, Reset) and the delta pipeline.
/// </summary>
public sealed class ScalePanelModel : ObservableModel
{
    public const string Dash = "—";
    public const int PinnedCap = 5;

    private readonly Settings _settings;
    private readonly DishLatch _latch = new();
    private string _recipeText = "";
    private string _deltaText = Dash;
    private string _dishCaption = "dish = —";
    private int _deltaPulse;
    private string? _deltaSourceId;

    public ScalePanelModel(Settings settings)
    {
        _settings = settings;
        NoCookwareRow = new CookwareRowModel(this, CookwareRowModel.NoCookwareId, "No cookware", 0, pinned: false);
        foreach (var item in settings.Cookware.Where(c => c.Pinned).OrderBy(c => c.Order))
            Rows.Add(new CookwareRowModel(this, item.Id, item.Name, item.Grams, pinned: true));
        Rows.Add(NoCookwareRow);
    }

    /// <summary>All rows in display order: pinned, then session rows, then the no-cookware row.</summary>
    public ObservableCollection<CookwareRowModel> Rows { get; } = new();

    public CookwareRowModel NoCookwareRow { get; }

    /// <summary>The dish row, or null while the water section is dormant.</summary>
    public CookwareRowModel? DishRow => Rows.FirstOrDefault(r => r.IsDish);

    /// <summary>The recipe weight text. Setting it via binding is a user edit.</summary>
    public string RecipeText
    {
        get => _recipeText;
        set
        {
            if (!Set(ref _recipeText, value))
                return;
            OnRecipeEdited();
        }
    }

    /// <summary>The water delta display text: signed whole number or a dash.</summary>
    public string DeltaText
    {
        get => _deltaText;
        private set => Set(ref _deltaText, value);
    }

    /// <summary>The accent caption naming the dish, e.g. "dish = Big pot · net 800 g".</summary>
    public string DishCaption
    {
        get => _dishCaption;
        private set => Set(ref _dishCaption, value);
    }

    /// <summary>Increments whenever the delta recomputes (drives the pulse).</summary>
    public int DeltaPulse
    {
        get => _deltaPulse;
        private set => Set(ref _deltaPulse, value);
    }

    /// <summary>The water delta value, or null while dormant.</summary>
    public double? DeltaValue { get; private set; }

    /// <summary>The parsed recipe weight, or null.</summary>
    public double? RecipeValue => Numeric.ParseNonNegative(RecipeText);

    /// <summary>False while the water section is dormant (Copy disabled).</summary>
    public bool CanCopy => DeltaValue is not null;

    /// <summary>Unpinned cookware available in the More-cookware expander.</summary>
    public IReadOnlyList<Cookware> AvailableCookware
        => _settings.Cookware
            .Where(c => !c.Pinned && Rows.All(r => r.Id != c.Id))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Raised on every accepted user edit; carries the status-bar echo text.</summary>
    public event Action<string>? Activity;

    /// <summary>
    /// Raised on every user-edit recompute whose computed partner is a valid
    /// number, carrying that partner's displayed text: net for a gross entry,
    /// gross for a net entry, the delta for recipe edits. Drives the live
    /// clipboard; never raised for placeholders, and never a log settle point.
    /// </summary>
    public event Action<string>? AutoCopy;

    /// <summary>Raised when the dish binding moves; true means the move must be loud.</summary>
    public event Action<CookwareRowModel?, bool>? DishRebound;

    /// <summary>Raised for status-bar notes about loud row changes (deleted cookware, pin cap).</summary>
    public event Action<string>? Note;

    /// <summary>Raised when pins or pin order changed and settings should be saved.</summary>
    public event Action? SettingsChanged;

    /// <summary>Number of currently pinned rows.</summary>
    public int PinnedCount => Rows.Count(r => r.Pinned);

    /// <summary>Handles an accepted user edit in a row (called from the row's text setters).</summary>
    internal void OnRowEdited(CookwareRowModel row, PairSide side)
    {
        row.Pair.UserEdited(side);
        row.Recompute();
        row.ChangedSinceCommit = true;

        if (_latch.RowTyped(row.Id))
            ApplyDishChange(loud: false);
        else
            RecomputeDelta();

        Activity?.Invoke(RowEcho(row));

        var partner = side == PairSide.A ? row.NetText : row.GrossText;
        if (Numeric.ParseDisplayed(partner) is not null)
            AutoCopy?.Invoke(partner);
    }

    /// <summary>
    /// An explicit act (Enter, click, accelerator) binds the row as the dish.
    /// A move while the recipe holds a value is loud.
    /// </summary>
    public void BindDish(CookwareRowModel row)
    {
        var loud = _latch.Frozen && _latch.DishRowId is not null && _latch.DishRowId != row.Id;
        if (_latch.ExplicitBind(row.Id))
            ApplyDishChange(loud);
    }

    /// <summary>Promotes an unpinned cookware item into an immediately typeable session row.</summary>
    public CookwareRowModel? PromoteToSession(string cookwareId)
    {
        var existing = Rows.FirstOrDefault(r => r.Id == cookwareId);
        if (existing is not null)
            return existing;

        var item = _settings.Cookware.FirstOrDefault(c => c.Id == cookwareId);
        if (item is null)
            return null;

        var row = new CookwareRowModel(this, item.Id, item.Name, item.Grams, pinned: false);
        Rows.Insert(Rows.Count - 1, row); // above the no-cookware row
        Raise(nameof(AvailableCookware));
        return row;
    }

    /// <summary>
    /// Pins a session row (respecting the cap) or unpins a pinned row, demoting
    /// it to a session row when it holds a value or the dish role and removing
    /// it otherwise. Returns true when anything changed.
    /// </summary>
    public bool TogglePin(CookwareRowModel row)
    {
        if (row.IsNoCookware)
            return false;
        var item = _settings.Cookware.FirstOrDefault(c => c.Id == row.Id);
        if (item is null)
            return false;

        if (!row.Pinned)
        {
            if (PinnedCount >= PinnedCap)
            {
                Note?.Invoke($"pin limit is {PinnedCap}; unpin another cookware item first");
                return false;
            }

            item.Pinned = true;
            item.Order = _settings.Cookware.Where(c => c.Pinned).Max(c => c.Order) + 1;
            row.Pinned = true;
            MoveRow(row, PinnedSectionEnd(exclude: row));
        }
        else
        {
            item.Pinned = false;
            row.Pinned = false;
            if (row.HoldsValue || row.IsDish)
                MoveRow(row, SessionSectionEnd(exclude: row)); // stays visible as a session row
            else
                Rows.Remove(row);
        }

        Raise(nameof(AvailableCookware));
        SettingsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Reconciles rows after the cookware CRUD changed settings: renames and
    /// tare edits re-derive computed members (never moving the dish binding and
    /// never logging), deletions clear rows loudly, pin changes add or demote
    /// rows, and pinned rows re-sort by their order.
    /// </summary>
    public void SyncFromSettings()
    {
        foreach (var row in Rows.Where(r => !r.IsNoCookware).ToList())
        {
            var item = _settings.Cookware.FirstOrDefault(c => c.Id == row.Id);
            if (item is null)
            {
                RemoveDeletedRow(row);
                continue;
            }

            row.Name = item.Name;
            if (Math.Abs(row.Tare - item.Grams) > double.Epsilon)
            {
                row.Tare = item.Grams;
                row.Recompute();
                // The tare edit changed this row's calculation, so the unit must
                // commit the corrected numbers at its next settle point. (The
                // edit itself is not a settle point and writes nothing here.)
                if (row.HoldsValue)
                    row.ChangedSinceCommit = true;
            }

            if (item.Pinned != row.Pinned)
            {
                row.Pinned = item.Pinned;
                if (!item.Pinned && !row.HoldsValue && !row.IsDish)
                    Rows.Remove(row);
            }
        }

        foreach (var item in _settings.Cookware.Where(c => c.Pinned))
        {
            if (Rows.All(r => r.Id != item.Id))
                Rows.Insert(PinnedSectionEnd(exclude: null), new CookwareRowModel(this, item.Id, item.Name, item.Grams, pinned: true));
        }

        SortPinnedRows();
        RecomputeDelta();
        Raise(nameof(AvailableCookware));
    }

    /// <summary>
    /// Clears every row, the recipe, the delta, and the dish binding, and
    /// collapses session rows. Pins survive. Committing changed units first is
    /// the caller's job.
    /// </summary>
    public void Reset()
    {
        foreach (var row in Rows.Where(r => !r.Pinned && !r.IsNoCookware).ToList())
            Rows.Remove(row);
        foreach (var row in Rows)
        {
            row.Clear();
            row.IsDish = false;
        }

        if (_recipeText != "")
        {
            _recipeText = "";
            Raise(nameof(RecipeText));
        }

        _latch.Reset();
        RecomputeDelta();
        Raise(nameof(AvailableCookware));
    }

    /// <summary>Recomputes the water section from the dish row and recipe text.</summary>
    private void RecomputeDelta()
    {
        var dish = DishRow;
        var recipe = RecipeValue;
        var newValue = dish?.NetValue is double net && recipe is double r ? net - r : (double?)null;
        // The pulse fires only when the delta's value or its source row actually
        // changed; editing an unrelated row must not mimic the loud re-target cue.
        var changed = newValue != DeltaValue || dish?.Id != _deltaSourceId;
        DeltaValue = newValue;
        _deltaSourceId = dish?.Id;
        DeltaText = DeltaValue is double d ? Numeric.FormatWhole(d) : Dash;
        DishCaption = dish is null
            ? "dish = —"
            : $"dish = {dish.Name} · net {(dish.NetValue is double n ? Numeric.FormatWhole(n) : Dash)} g";
        Raise(nameof(CanCopy));
        if (changed)
            DeltaPulse++;
    }

    private void OnRecipeEdited()
    {
        _latch.RecipeChanged(!string.IsNullOrWhiteSpace(RecipeText));
        if (DishRow is CookwareRowModel dish)
            dish.ChangedSinceCommit = true;
        RecomputeDelta();
        Activity?.Invoke(DeltaValue is double d ? $"= {Numeric.FormatWhole(d)} g" : Dash);
        if (DeltaValue is not null)
            AutoCopy?.Invoke(DeltaText);
    }

    private void ApplyDishChange(bool loud)
    {
        foreach (var row in Rows)
            row.IsDish = row.Id == _latch.DishRowId;
        if (DishRow is CookwareRowModel dish)
            dish.ChangedSinceCommit = true;
        RecomputeDelta();
        DishRebound?.Invoke(DishRow, loud);
    }

    private void RemoveDeletedRow(CookwareRowModel row)
    {
        var hadValue = row.HoldsValue;
        var wasDish = row.IsDish;
        Rows.Remove(row);
        _latch.RowRemoved(row.Id);
        if (wasDish)
            ApplyDishChange(loud: false);
        if (hadValue || wasDish)
        {
            Note?.Invoke($"{row.Name} was deleted; its reading is gone");
            DeltaPulse++;
        }
    }

    private string RowEcho(CookwareRowModel row)
    {
        if (row.IsDish && DeltaValue is double d)
            return $"= {Numeric.FormatWhole(d)} g";
        var partner = row.Pair.Input == PairSide.A ? row.NetValue : row.GrossValue;
        return partner is double p ? $"= {Numeric.FormatWhole(p)} g" : Dash;
    }

    private int PinnedSectionEnd(CookwareRowModel? exclude)
        => Rows.Count(r => r.Pinned && !ReferenceEquals(r, exclude));

    private int SessionSectionEnd(CookwareRowModel? exclude)
        => Rows.Count(r => !r.IsNoCookware && !ReferenceEquals(r, exclude));

    private void MoveRow(CookwareRowModel row, int newIndex)
    {
        var oldIndex = Rows.IndexOf(row);
        if (oldIndex < 0 || oldIndex == newIndex)
            return;
        Rows.Move(oldIndex, Math.Clamp(newIndex, 0, Rows.Count - 1));
    }

    private void SortPinnedRows()
    {
        var order = _settings.Cookware.Where(c => c.Pinned).OrderBy(c => c.Order).Select(c => c.Id).ToList();
        var target = 0;
        foreach (var id in order)
        {
            var index = Rows.ToList().FindIndex(r => r.Id == id);
            if (index >= 0 && index != target)
                Rows.Move(index, target);
            if (index >= 0)
                target++;
        }
    }
}
