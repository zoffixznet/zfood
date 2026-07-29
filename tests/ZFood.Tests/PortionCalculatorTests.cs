using ZFood.Core;

namespace ZFood.Tests;

public class PortionCalculatorTests
{
    [Fact]
    public void Grams_to_calories_matches_label_math()
    {
        // Worked example: serving 56 g = 250 cal; eaten 128 g gives 571 cal.
        var r = PortionCalculator.Compute(56, 250, PairSide.A, 128);
        Assert.NotNull(r.EatenCalories);
        Assert.Equal("571", Numeric.FormatWhole(r.EatenCalories!.Value));
        Assert.Equal(128, r.EatenGrams);
    }

    [Fact]
    public void Calories_to_grams_reverses_the_label_math()
    {
        // Reverse: 250 cal budget gives 56 g.
        var r = PortionCalculator.Compute(56, 250, PairSide.B, 250);
        Assert.Equal(56, r.EatenGrams!.Value, 10);
        Assert.Equal(250, r.EatenCalories);
    }

    [Fact]
    public void Density_appears_as_soon_as_the_serving_pair_is_valid()
    {
        var r = PortionCalculator.Compute(250, 775, PairSide.None, null);
        Assert.Equal(3.1, r.Density!.Value, 10);
        Assert.Equal(310, r.Per100!.Value, 10);
        Assert.Null(r.EatenGrams);
        Assert.Null(r.EatenCalories);
    }

    [Fact]
    public void Zero_serving_grams_blanks_density_and_both_directions()
    {
        var forward = PortionCalculator.Compute(0, 250, PairSide.A, 100);
        Assert.Null(forward.Density);
        Assert.Null(forward.EatenCalories);

        var reverse = PortionCalculator.Compute(0, 250, PairSide.B, 100);
        Assert.Null(reverse.EatenGrams);
    }

    [Fact]
    public void Empty_serving_grams_blanks_density_and_both_directions()
    {
        var r = PortionCalculator.Compute(null, 250, PairSide.A, 100);
        Assert.Null(r.Density);
        Assert.Null(r.EatenCalories);
    }

    [Fact]
    public void Zero_serving_calories_is_legal_food_forward_but_blanks_reverse()
    {
        var forward = PortionCalculator.Compute(100, 0, PairSide.A, 50);
        Assert.Equal(0, forward.Density);
        Assert.Equal(0, forward.EatenCalories);

        var reverse = PortionCalculator.Compute(100, 0, PairSide.B, 50);
        Assert.Null(reverse.EatenGrams);
    }

    [Fact]
    public void Missing_eaten_input_leaves_partner_unknown()
    {
        var r = PortionCalculator.Compute(56, 250, PairSide.A, null);
        Assert.Null(r.EatenGrams);
        Assert.Null(r.EatenCalories);
    }

    [Fact]
    public void No_role_yields_no_eaten_values()
    {
        var r = PortionCalculator.Compute(56, 250, PairSide.None, 999);
        Assert.Null(r.EatenGrams);
        Assert.Null(r.EatenCalories);
    }
}
