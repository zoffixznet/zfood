using CommunityToolkit.Mvvm.ComponentModel;
using ZFood.Core;

namespace ZFood.App.ViewModels;

/// <summary>
/// The PORTION panel: serving pair always input, eaten pair bidirectional with
/// last-edited-wins, live density readout, recalculation on every keystroke.
/// </summary>
public partial class PortionViewModel : ObservableObject
{
    public const string Dash = "—";

    private readonly PairRoleMachine _eatenPair = new();
    private bool _updating;

    [ObservableProperty]
    private string servingGramsText = "";

    [ObservableProperty]
    private string servingCaloriesText = "";

    [ObservableProperty]
    private string eatenGramsText = "";

    [ObservableProperty]
    private string eatenCaloriesText = "";

    /// <summary>The hero density number, e.g. "3.10", or a dash.</summary>
    [ObservableProperty]
    private string densityCalPerG = Dash;

    /// <summary>Calories per 100 g, e.g. "310", or a dash.</summary>
    [ObservableProperty]
    private string densityPer100 = Dash;

    /// <summary>True while the density hero shows a placeholder dash.</summary>
    [ObservableProperty]
    private bool densityIsPlaceholder = true;

    [ObservableProperty]
    private bool eatenGramsIsComputed;

    [ObservableProperty]
    private bool eatenCaloriesIsComputed;

    /// <summary>
    /// Raised on every user-edit recompute whose computed eaten member is a
    /// valid number, carrying that member's displayed text (calories for a
    /// grams entry and vice versa). Drives the live clipboard; never raised
    /// for placeholders, and never a log settle point.
    /// </summary>
    public event Action<string>? AutoCopy;

    /// <summary>Raised when a computed field recomputes; the argument names it (for the pulse).</summary>
    public event Action<string>? ComputedChanged;

    /// <summary>Which eaten member currently holds the input role.</summary>
    public PairSide EatenInput => _eatenPair.Input;

    /// <summary>
    /// Set when the panel's calculation changed after its last log commit; the
    /// commit coordinator clears it after settling the unit.
    /// </summary>
    public bool ChangedSinceCommit { get; set; }

    partial void OnServingGramsTextChanged(string value) => UserEdit(null);

    partial void OnServingCaloriesTextChanged(string value) => UserEdit(null);

    partial void OnEatenGramsTextChanged(string value) => UserEdit(PairSide.A);

    partial void OnEatenCaloriesTextChanged(string value) => UserEdit(PairSide.B);

    /// <summary>
    /// Copies the scale panel's net weight into eaten grams as a normal user
    /// edit, claiming the input role for grams.
    /// </summary>
    public void UseAsEatenGrams(double netWeight) => EatenGramsText = Numeric.FormatEditable(netWeight);

    /// <summary>Clears every field and both pair roles.</summary>
    public void Reset()
    {
        _updating = true;
        try
        {
            ServingGramsText = "";
            ServingCaloriesText = "";
            EatenGramsText = "";
            EatenCaloriesText = "";
            DensityCalPerG = Dash;
            DensityPer100 = Dash;
            DensityIsPlaceholder = true;
            EatenGramsIsComputed = false;
            EatenCaloriesIsComputed = false;
            _eatenPair.Reset();
            ChangedSinceCommit = false;
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>
    /// Builds the panel's log entry when every input parses and a result exists;
    /// null otherwise.
    /// </summary>
    public LogEntry? TryBuildEntry(DateTimeOffset ts)
    {
        var sg = Numeric.ParseNonNegative(ServingGramsText);
        var scal = Numeric.ParseNonNegative(ServingCaloriesText);
        if (sg is not double g || scal is not double c)
            return null;

        var result = Compute(g, c);
        if (result.Density is not double d || result.EatenGrams is not double eg || result.EatenCalories is not double ecal)
            return null;

        return LogEntryFactory.Portion(ts, g, c, _eatenPair.Input, eg, ecal, d);
    }

    private void UserEdit(PairSide? editedSide)
    {
        if (_updating)
            return;

        if (editedSide is PairSide side)
            _eatenPair.UserEdited(side);

        ChangedSinceCommit = true;
        Recompute();
    }

    private PortionResult Compute(double? servingG, double? servingCal)
    {
        var inputText = _eatenPair.Input switch
        {
            PairSide.A => EatenGramsText,
            PairSide.B => EatenCaloriesText,
            _ => null,
        };
        return PortionCalculator.Compute(servingG, servingCal, _eatenPair.Input, Numeric.ParseNonNegative(inputText));
    }

    private void Recompute()
    {
        var sg = Numeric.ParseNonNegative(ServingGramsText);
        var scal = Numeric.ParseNonNegative(ServingCaloriesText);
        var result = Compute(sg, scal);

        _updating = true;
        try
        {
            DensityCalPerG = result.Density is double d ? Numeric.FormatDensity(d) : Dash;
            DensityPer100 = result.Per100 is double p ? Numeric.FormatWhole(p) : Dash;
            DensityIsPlaceholder = result.Density is null;
            EatenGramsIsComputed = _eatenPair.Input == PairSide.B;
            EatenCaloriesIsComputed = _eatenPair.Input == PairSide.A;

            switch (_eatenPair.Input)
            {
                case PairSide.A:
                    EatenCaloriesText = result.EatenCalories is double ec ? Numeric.FormatWhole(ec) : Dash;
                    ComputedChanged?.Invoke(nameof(EatenCaloriesText));
                    break;
                case PairSide.B:
                    EatenGramsText = result.EatenGrams is double eg ? Numeric.FormatWhole(eg) : Dash;
                    ComputedChanged?.Invoke(nameof(EatenGramsText));
                    break;
            }

            ComputedChanged?.Invoke(nameof(DensityCalPerG));
        }
        finally
        {
            _updating = false;
        }

        var partner = _eatenPair.Input switch
        {
            PairSide.A => EatenCaloriesText,
            PairSide.B => EatenGramsText,
            _ => null,
        };
        if (Numeric.ParseDisplayed(partner) is not null)
            AutoCopy?.Invoke(partner!);
    }
}
