namespace ZFood.Core;

/// <summary>
/// Builds log entries from settled panel states, rendering the equation text and
/// the structured inputs consistently.
/// </summary>
public static class LogEntryFactory
{
    /// <summary>
    /// A settled portion calculation. <paramref name="eatenInput"/> is the side
    /// the user typed (A = grams, B = calories); the partner is the result.
    /// </summary>
    public static LogEntry Portion(DateTimeOffset ts, double servingGrams, double servingCalories,
        PairSide eatenInput, double eatenGrams, double eatenCalories, double density)
    {
        var grams = Numeric.FormatWhole(eatenGrams);
        var cal = Numeric.FormatWhole(eatenCalories);
        var serving = $"{Numeric.FormatEditable(servingGrams)} g = {Numeric.FormatEditable(servingCalories)} cal ({Numeric.FormatDensity(density)} cal/g)";

        return eatenInput == PairSide.A
            ? new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Portion,
                Result = cal,
                Unit = "cal",
                Equation = $"{serving} · eaten {grams} g",
                Inputs = new Dictionary<string, string>
                {
                    ["servingG"] = Numeric.FormatEditable(servingGrams),
                    ["servingCal"] = Numeric.FormatEditable(servingCalories),
                    ["side"] = "g",
                    ["value"] = Numeric.FormatEditable(eatenGrams),
                },
            }
            : new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Portion,
                Result = grams,
                Unit = "g",
                Equation = $"{serving} · budget {cal} cal",
                Inputs = new Dictionary<string, string>
                {
                    ["servingG"] = Numeric.FormatEditable(servingGrams),
                    ["servingCal"] = Numeric.FormatEditable(servingCalories),
                    ["side"] = "cal",
                    ["value"] = Numeric.FormatEditable(eatenCalories),
                },
            };
    }

    /// <summary>
    /// A settled tare-only calculation. <paramref name="grossNetInput"/> is the
    /// side the user typed (A = gross, B = net); the partner is the result.
    /// </summary>
    public static LogEntry Tare(DateTimeOffset ts, string cookwareName, double tare,
        PairSide grossNetInput, double gross, double net)
    {
        var pot = $"{cookwareName} ({Numeric.FormatWhole(tare)} g)";

        return grossNetInput == PairSide.A
            ? new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Tare,
                Result = Numeric.FormatWhole(net),
                Unit = "g",
                Equation = $"{pot} · gross {Numeric.FormatWhole(gross)} g → net",
                Inputs = TareInputs(cookwareName, tare, "gross", gross),
            }
            : new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Tare,
                Result = Numeric.FormatWhole(gross),
                Unit = "g",
                Equation = $"{pot} · net {Numeric.FormatWhole(net)} g → target gross",
                Inputs = TareInputs(cookwareName, tare, "net", net),
            };
    }

    /// <summary>
    /// A settled water reconciliation: the full pipeline including the recipe
    /// weight. Records gross, tare, and net, so it subsumes the tare-only entry
    /// built from the same inputs.
    /// </summary>
    public static LogEntry Water(DateTimeOffset ts, string cookwareName, double tare,
        PairSide grossNetInput, double gross, double net, double recipe, double delta)
    {
        var pot = $"{cookwareName} ({Numeric.FormatWhole(tare)} g)";
        var side = grossNetInput == PairSide.A ? "gross" : "net";
        var sideValue = grossNetInput == PairSide.A ? gross : net;

        var inputs = TareInputs(cookwareName, tare, side, sideValue);
        inputs["recipe"] = Numeric.FormatEditable(recipe);

        return new LogEntry
        {
            Ts = ts,
            Panel = LogPanel.Water,
            Result = Numeric.FormatWhole(delta),
            Unit = "g",
            Equation = $"{pot} · gross {Numeric.FormatWhole(gross)} g → net {Numeric.FormatWhole(net)} g · recipe {Numeric.FormatWhole(recipe)} g → water",
            Inputs = inputs,
        };
    }

    /// <summary>
    /// True when the water entry records the same gross/net inputs as the
    /// tare-only entry, making the tare entry redundant.
    /// </summary>
    public static bool WaterSubsumesTare(LogEntry water, LogEntry tare)
        => water.Panel == LogPanel.Water
           && tare.Panel == LogPanel.Tare
           && SameValue(water, tare, "cookware")
           && SameValue(water, tare, "tare")
           && SameValue(water, tare, "side")
           && SameValue(water, tare, "value");

    private static bool SameValue(LogEntry a, LogEntry b, string key)
        => a.Inputs.TryGetValue(key, out var av) && b.Inputs.TryGetValue(key, out var bv) && av == bv;

    private static Dictionary<string, string> TareInputs(string cookwareName, double tare, string side, double value)
        => new()
        {
            ["cookware"] = cookwareName,
            ["tare"] = Numeric.FormatEditable(tare),
            ["side"] = side,
            ["value"] = Numeric.FormatEditable(value),
        };
}
