namespace ZFood.Core;

/// <summary>Resolved state of the scale panel pipeline for a given set of inputs.</summary>
/// <param name="Gross">Resolved gross scale reading (input or computed), or null.</param>
/// <param name="Net">Resolved net food weight (input or computed), or null. May be negative when gross is below tare.</param>
/// <param name="WaterDelta">Water to add (net minus recipe), or null while either operand is missing.</param>
public sealed record ScaleResult(double? Gross, double? Net, double? WaterDelta)
{
    /// <summary>True when the gross reading is below the tare (probable wrong cookware).</summary>
    public bool NetBelowZero => Net is < 0;
}

/// <summary>
/// Cookware tare and recipe water reconciliation, fused into one pipeline:
/// tare -> gross &lt;-&gt; net -> recipe -> water delta. Net is the actual cooked
/// dish weight, so the water delta needs no intermediate retyping.
/// </summary>
public static class ScaleCalculator
{
    /// <summary>
    /// Computes the pipeline. <paramref name="grossNetInput"/> says which member
    /// of the gross/net pair is the input (A = gross, B = net) and
    /// <paramref name="grossNetValue"/> carries its parsed value. The water rows
    /// stay dormant (null delta) until the recipe weight parses.
    /// </summary>
    public static ScaleResult Compute(double tare, PairSide grossNetInput, double? grossNetValue, double? recipeWeight)
    {
        double? gross = null;
        double? net = null;

        switch (grossNetInput)
        {
            case PairSide.A: // gross entered, net computed (may go negative below tare)
                gross = grossNetValue;
                net = grossNetValue - tare;
                break;
            case PairSide.B: // desired net entered, target gross computed
                net = grossNetValue;
                gross = grossNetValue + tare;
                break;
        }

        double? delta = net is not null && recipeWeight is not null ? net - recipeWeight : null;

        return new ScaleResult(gross, net, delta);
    }
}
