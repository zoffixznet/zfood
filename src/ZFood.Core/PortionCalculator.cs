namespace ZFood.Core;

/// <summary>Resolved state of the portion panel for a given set of inputs.</summary>
/// <param name="Density">Calories per gram, or null when the serving pair does not define one.</param>
/// <param name="EatenGrams">Resolved eaten grams (input or computed), or null.</param>
/// <param name="EatenCalories">Resolved eaten calories (input or computed), or null.</param>
public sealed record PortionResult(double? Density, double? EatenGrams, double? EatenCalories)
{
    /// <summary>Calories per 100 g, or null when density is undefined.</summary>
    public double? Per100 => Density * 100;
}

/// <summary>
/// Label math: a serving of S_g grams contains S_cal calories. The eaten pair is
/// bidirectional; the serving pair is always input.
/// </summary>
public static class PortionCalculator
{
    /// <summary>
    /// Computes density and the eaten pair. <paramref name="eatenInput"/> says
    /// which eaten member is the input (A = grams, B = calories) and
    /// <paramref name="eatenValue"/> carries its parsed value (null when empty
    /// or unparseable).
    /// Division rules: serving grams 0 or empty blanks density and both eaten
    /// computations; serving calories 0 is legal (eaten calories computes 0)
    /// but blanks the calories-to-grams direction.
    /// </summary>
    public static PortionResult Compute(double? servingGrams, double? servingCalories, PairSide eatenInput, double? eatenValue)
    {
        double? density = servingGrams is > 0 && servingCalories is >= 0
            ? servingCalories / servingGrams
            : null;

        double? eatenGrams = null;
        double? eatenCalories = null;

        switch (eatenInput)
        {
            case PairSide.A: // grams entered, calories computed
                eatenGrams = eatenValue;
                eatenCalories = density is not null && eatenValue is not null
                    ? density * eatenValue
                    : null;
                break;
            case PairSide.B: // calories entered, grams computed
                eatenCalories = eatenValue;
                eatenGrams = servingGrams is > 0 && servingCalories is > 0 && eatenValue is not null
                    ? eatenValue * servingGrams / servingCalories
                    : null;
                break;
        }

        return new PortionResult(density, eatenGrams, eatenCalories);
    }
}
