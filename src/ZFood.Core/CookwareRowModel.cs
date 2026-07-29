namespace ZFood.Core;

/// <summary>
/// One cookware row on the SCALE panel: a cookware item (or the permanent no-cookware
/// row) with its own bidirectional gross/net pair and last-edited-wins state.
/// Text properties are bind-ready; user edits flow through the owning
/// <see cref="ScalePanelModel"/>, programmatic partner updates do not.
/// </summary>
public sealed class CookwareRowModel : ObservableModel
{
    /// <summary>Row id of the permanent no-cookware (tare 0) row. The literal
    /// value is persisted inside log.jsonl unit ids and must never change.</summary>
    public const string NoCookwareId = "nopot";

    private readonly ScalePanelModel _panel;
    private string _name;
    private double _tare;
    private bool _pinned;
    private bool _isDish;
    private bool _belowTare;
    private string _grossText = "";
    private string _netText = "";
    private bool _grossIsComputed;
    private bool _netIsComputed;
    private int _computedPulse;

    internal CookwareRowModel(ScalePanelModel panel, string id, string name, double tare, bool pinned)
    {
        _panel = panel;
        Id = id;
        _name = name;
        _tare = tare;
        _pinned = pinned;
    }

    internal PairRoleMachine Pair { get; } = new();

    internal bool Updating { get; set; }

    /// <summary>Cookware id, or <see cref="NoCookwareId"/> for the permanent no-cookware row.</summary>
    public string Id { get; }

    public bool IsNoCookware => Id == NoCookwareId;

    public string Name
    {
        get => _name;
        internal set
        {
            if (Set(ref _name, value))
                Raise(nameof(TareEcho));
        }
    }

    /// <summary>Empty (tare) weight in grams.</summary>
    public double Tare
    {
        get => _tare;
        internal set
        {
            if (Set(ref _tare, value))
                Raise(nameof(TareEcho));
        }
    }

    /// <summary>The permanent tare echo beside the name, e.g. "tare 640".</summary>
    public string TareEcho => $"tare {Numeric.FormatWhole(Tare)}";

    /// <summary>Pinned rows are permanent; unpinned rows live for the session.</summary>
    public bool Pinned
    {
        get => _pinned;
        internal set => Set(ref _pinned, value);
    }

    /// <summary>True when this row feeds the water section.</summary>
    public bool IsDish
    {
        get => _isDish;
        internal set => Set(ref _isDish, value);
    }

    /// <summary>True while the gross reading is below the tare (probable wrong cookware).</summary>
    public bool BelowTare
    {
        get => _belowTare;
        private set => Set(ref _belowTare, value);
    }

    /// <summary>The gross ("reads") field text. Setting it via binding is a user edit.</summary>
    public string GrossText
    {
        get => _grossText;
        set
        {
            if (!Set(ref _grossText, value) || Updating)
                return;
            _panel.OnRowEdited(this, PairSide.A);
        }
    }

    /// <summary>The net field text. Setting it via binding is a user edit.</summary>
    public string NetText
    {
        get => _netText;
        set
        {
            if (!Set(ref _netText, value) || Updating)
                return;
            _panel.OnRowEdited(this, PairSide.B);
        }
    }

    public bool GrossIsComputed
    {
        get => _grossIsComputed;
        private set => Set(ref _grossIsComputed, value);
    }

    public bool NetIsComputed
    {
        get => _netIsComputed;
        private set => Set(ref _netIsComputed, value);
    }

    /// <summary>Increments whenever the computed partner recomputes (drives the pulse).</summary>
    public int ComputedPulse
    {
        get => _computedPulse;
        private set => Set(ref _computedPulse, value);
    }

    /// <summary>Which member currently holds the input role.</summary>
    public PairSide Input => Pair.Input;

    /// <summary>Resolved gross value (input or computed), or null.</summary>
    public double? GrossValue { get; private set; }

    /// <summary>Resolved net value (input or computed), or null. May be negative.</summary>
    public double? NetValue { get; private set; }

    /// <summary>True when the row's input side holds a parseable number.</summary>
    public bool HoldsValue => Pair.Input != PairSide.None && Numeric.ParseNonNegative(InputText) is not null;

    /// <summary>True when the row has no entered text at all (renders dim).</summary>
    public bool IsEmpty => Pair.Input == PairSide.None || string.IsNullOrWhiteSpace(InputText);

    /// <summary>
    /// Set when the row's calculation changed after its last log commit; the
    /// commit coordinator clears it after settling the unit.
    /// </summary>
    public bool ChangedSinceCommit { get; set; }

    private string InputText => Pair.Input == PairSide.B ? NetText : GrossText;

    /// <summary>Re-derives the computed member from the sticky input member.</summary>
    internal void Recompute()
    {
        var side = Pair.Input;
        if (side == PairSide.None)
        {
            GrossValue = null;
            NetValue = null;
            BelowTare = false;
            return;
        }

        var value = Numeric.ParseNonNegative(InputText);
        var result = ScaleCalculator.Compute(Tare, side, value, null);
        GrossValue = result.Gross;
        NetValue = result.Net;
        BelowTare = result.NetBelowZero;

        Updating = true;
        try
        {
            if (side == PairSide.A)
            {
                NetText = result.Net is double n ? Numeric.FormatWhole(n) : ScalePanelModel.Dash;
                NetIsComputed = true;
                GrossIsComputed = false;
            }
            else
            {
                GrossText = result.Gross is double g ? Numeric.FormatWhole(g) : ScalePanelModel.Dash;
                GrossIsComputed = true;
                NetIsComputed = false;
            }

            ComputedPulse++;
            Raise(nameof(IsEmpty));
        }
        finally
        {
            Updating = false;
        }
    }

    /// <summary>Clears the row's texts, roles, and values (Reset).</summary>
    internal void Clear()
    {
        Updating = true;
        try
        {
            GrossText = "";
            NetText = "";
            GrossIsComputed = false;
            NetIsComputed = false;
            BelowTare = false;
            GrossValue = null;
            NetValue = null;
            Pair.Reset();
            ChangedSinceCommit = false;
            Raise(nameof(IsEmpty));
        }
        finally
        {
            Updating = false;
        }
    }

    /// <summary>
    /// Builds this row's log entry when its calculation is complete; a dish row
    /// with a live water delta absorbs the water clause. Null otherwise.
    /// </summary>
    public LogEntry? TryBuildEntry(DateTimeOffset ts)
    {
        if (Pair.Input == PairSide.None)
            return null;
        if (GrossValue is not double gross || NetValue is not double net)
            return null;

        if (IsDish && _panel.RecipeValue is double recipe && _panel.DeltaValue is double delta)
            return LogEntryFactory.Water(ts, Id, Name, Tare, Pair.Input, gross, net, recipe, delta);

        return LogEntryFactory.Tare(ts, Id, Name, Tare, Pair.Input, gross, net);
    }
}
