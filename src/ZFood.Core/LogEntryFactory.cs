namespace ZFood.Core;

/// <summary>
/// Builds log entries from settled panel states, rendering the equation text and
/// the structured inputs consistently. Every scale entry names its pot and tare.
/// </summary>
public static class LogEntryFactory
{
    /// <summary>The portion panel's commit-unit key.</summary>
    public const string PortionUnit = "portion";

    /// <summary>The commit-unit key for a pot row.</summary>
    public static string PotUnit(string rowId) => "pot:" + rowId;

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
                Unit = PortionUnit,
                Result = cal,
                ResultUnit = "cal",
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
                Unit = PortionUnit,
                Result = grams,
                ResultUnit = "g",
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
    /// A settled tare-only pot-row calculation. <paramref name="grossNetInput"/>
    /// is the side the user typed (A = gross, B = net); the partner is the result.
    /// </summary>
    public static LogEntry Tare(DateTimeOffset ts, string rowId, string potName, double tare,
        PairSide grossNetInput, double gross, double net)
    {
        var pot = $"{potName} ({Numeric.FormatWhole(tare)} g)";

        return grossNetInput == PairSide.A
            ? new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Tare,
                Unit = PotUnit(rowId),
                Result = Numeric.FormatWhole(net),
                ResultUnit = "g",
                Equation = $"{pot} · gross {Numeric.FormatWhole(gross)} g → net",
                Inputs = TareInputs(potName, tare, "gross", gross),
            }
            : new LogEntry
            {
                Ts = ts,
                Panel = LogPanel.Tare,
                Unit = PotUnit(rowId),
                Result = Numeric.FormatWhole(gross),
                ResultUnit = "g",
                Equation = $"{pot} · net {Numeric.FormatWhole(net)} g → target gross",
                Inputs = TareInputs(potName, tare, "net", net),
            };
    }

    /// <summary>
    /// A settled water reconciliation: a dish row's full pipeline including the
    /// recipe weight. Records gross, tare, and net, so it subsumes the same
    /// row's tare-only entry built from the same inputs.
    /// </summary>
    public static LogEntry Water(DateTimeOffset ts, string rowId, string potName, double tare,
        PairSide grossNetInput, double gross, double net, double recipe, double delta)
    {
        var pot = $"{potName} ({Numeric.FormatWhole(tare)} g)";
        var side = grossNetInput == PairSide.A ? "gross" : "net";
        var sideValue = grossNetInput == PairSide.A ? gross : net;

        var inputs = TareInputs(potName, tare, side, sideValue);
        inputs["recipe"] = Numeric.FormatEditable(recipe);

        return new LogEntry
        {
            Ts = ts,
            Panel = LogPanel.Water,
            Unit = PotUnit(rowId),
            Result = Numeric.FormatWhole(delta),
            ResultUnit = "g",
            Equation = $"{pot} · gross {Numeric.FormatWhole(gross)} g → net {Numeric.FormatWhole(net)} g · recipe {Numeric.FormatWhole(recipe)} g → water",
            Inputs = inputs,
        };
    }

    /// <summary>
    /// True when the water entry comes from the same unit and records the same
    /// gross/net inputs as the tare-only entry, making the tare entry redundant.
    /// </summary>
    public static bool WaterSubsumesTare(LogEntry water, LogEntry tare)
        => water.Panel == LogPanel.Water
           && tare.Panel == LogPanel.Tare
           && water.Unit == tare.Unit
           && SameValue(water, tare, "pot")
           && SameValue(water, tare, "tare")
           && SameValue(water, tare, "side")
           && SameValue(water, tare, "value");

    private static bool SameValue(LogEntry a, LogEntry b, string key)
        => a.Inputs.TryGetValue(key, out var av) && b.Inputs.TryGetValue(key, out var bv) && av == bv;

    private static Dictionary<string, string> TareInputs(string potName, double tare, string side, double value)
        => new()
        {
            ["pot"] = potName,
            ["tare"] = Numeric.FormatEditable(tare),
            ["side"] = side,
            ["value"] = Numeric.FormatEditable(value),
        };
}
