namespace ZFood.Core;

/// <summary>Which kind of calculation a log entry records.</summary>
public enum LogPanel
{
    /// <summary>Label math (portion panel).</summary>
    Portion,

    /// <summary>Tare-only pot-row calculation (gross/net, no water clause).</summary>
    Tare,

    /// <summary>Water reconciliation (a dish row's pipeline including the recipe).</summary>
    Water,
}

/// <summary>
/// One completed calculation. The result comes first because recall is the whole
/// point; the equation carries enough context to reconstruct what was calculated.
/// </summary>
public sealed class LogEntry
{
    /// <summary>When the calculation settled.</summary>
    public DateTimeOffset Ts { get; set; }

    /// <summary>Which kind of calculation this records.</summary>
    public LogPanel Panel { get; set; }

    /// <summary>
    /// The commit unit that produced the entry: "portion" for the portion panel,
    /// or a per-row key for pot rows. Duplicate suppression is per unit.
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>Bare result number, invariant formatting, signed when negative.</summary>
    public string Result { get; set; } = "";

    /// <summary>Unit of the result: "g" or "cal".</summary>
    public string ResultUnit { get; set; } = "";

    /// <summary>Human-readable provenance: the full equation behind the result.</summary>
    public string Equation { get; set; } = "";

    /// <summary>Raw structured inputs, invariant-formatted, for reconstruction and dedupe.</summary>
    public Dictionary<string, string> Inputs { get; set; } = new();

    /// <summary>
    /// True when every required field survived deserialization. A damaged or
    /// hand-edited log line can null any of them (e.g. "inputs": null), and such
    /// an entry must be treated as corrupt, never dereferenced.
    /// </summary>
    public bool HasRequiredFields
        => Unit is not null
           && Result is not null
           && ResultUnit is not null
           && Equation is not null
           && Inputs is not null
           && Enum.IsDefined(Panel);

    /// <summary>True when the other entry records the identical settled calculation.</summary>
    public bool SameCalculation(LogEntry other)
    {
        if (Panel != other.Panel || Unit != other.Unit || Result != other.Result || ResultUnit != other.ResultUnit)
            return false;

        // Tolerate null inputs (a corrupt line that slipped into memory must
        // never crash a commit).
        if (Inputs is null || other.Inputs is null)
            return Inputs is null && other.Inputs is null;

        return Inputs.Count == other.Inputs.Count
               && Inputs.All(kv => other.Inputs.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }
}
