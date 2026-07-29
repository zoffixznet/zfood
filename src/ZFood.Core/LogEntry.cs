namespace ZFood.Core;

/// <summary>Which calculation a log entry records.</summary>
public enum LogPanel
{
    /// <summary>Label math (portion panel).</summary>
    Portion,

    /// <summary>Tare-only scale calculation (gross/net, no recipe).</summary>
    Tare,

    /// <summary>Water reconciliation (full scale pipeline including recipe).</summary>
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

    /// <summary>Which calculation this records.</summary>
    public LogPanel Panel { get; set; }

    /// <summary>Bare result number, invariant formatting, signed when negative.</summary>
    public string Result { get; set; } = "";

    /// <summary>Unit of the result: "g" or "cal".</summary>
    public string Unit { get; set; } = "";

    /// <summary>Human-readable provenance: the full equation behind the result.</summary>
    public string Equation { get; set; } = "";

    /// <summary>Raw structured inputs, invariant-formatted, for reconstruction and dedupe.</summary>
    public Dictionary<string, string> Inputs { get; set; } = new();

    /// <summary>Portion entries form one dedupe group; Tare and Water share the scale group.</summary>
    public bool SameGroup(LogEntry other)
        => (Panel == LogPanel.Portion) == (other.Panel == LogPanel.Portion);

    /// <summary>True when the other entry records the identical settled calculation.</summary>
    public bool SameCalculation(LogEntry other)
        => Panel == other.Panel
           && Result == other.Result
           && Unit == other.Unit
           && Inputs.Count == other.Inputs.Count
           && Inputs.All(kv => other.Inputs.TryGetValue(kv.Key, out var v) && v == kv.Value);
}
