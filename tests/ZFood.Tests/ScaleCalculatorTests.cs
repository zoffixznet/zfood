using ZFood.Core;

namespace ZFood.Tests;

public class ScaleCalculatorTests
{
    [Fact]
    public void Gross_minus_tare_gives_net()
    {
        var r = ScaleCalculator.Compute(640, PairSide.A, 1440, null);
        Assert.Equal(800, r.Net);
        Assert.Equal(1440, r.Gross);
        Assert.Null(r.WaterDelta);
        Assert.False(r.NetBelowZero);
    }

    [Fact]
    public void Desired_net_plus_tare_gives_target_gross()
    {
        var r = ScaleCalculator.Compute(640, PairSide.B, 200, null);
        Assert.Equal(840, r.Gross);
        Assert.Equal(200, r.Net);
    }

    [Fact]
    public void Water_delta_is_net_minus_recipe()
    {
        // Worked example: R = 1000, A = 800, result -200.
        var r = ScaleCalculator.Compute(640, PairSide.A, 1440, 1000);
        Assert.Equal(-200, r.WaterDelta);
    }

    [Fact]
    public void No_cookware_path_uses_tare_zero()
    {
        var r = ScaleCalculator.Compute(0, PairSide.A, 800, 1000);
        Assert.Equal(800, r.Net);
        Assert.Equal(800, r.Gross);
        Assert.Equal(-200, r.WaterDelta);
    }

    [Fact]
    public void Water_rows_stay_dormant_without_recipe_weight()
    {
        var r = ScaleCalculator.Compute(640, PairSide.A, 1440, null);
        Assert.Null(r.WaterDelta);
    }

    [Fact]
    public void Recipe_without_scale_reading_gives_no_delta()
    {
        var r = ScaleCalculator.Compute(640, PairSide.A, null, 1000);
        Assert.Null(r.WaterDelta);
        Assert.Null(r.Net);
    }

    [Fact]
    public void Gross_below_tare_computes_negative_net_and_flags_it()
    {
        var r = ScaleCalculator.Compute(640, PairSide.A, 500, null);
        Assert.Equal(-140, r.Net);
        Assert.True(r.NetBelowZero);
    }

    [Fact]
    public void Negative_net_still_propagates_into_the_delta()
    {
        var r = ScaleCalculator.Compute(640, PairSide.A, 500, 100);
        Assert.Equal(-240, r.WaterDelta);
    }

    [Fact]
    public void No_role_yields_nothing()
    {
        var r = ScaleCalculator.Compute(640, PairSide.None, 1440, 1000);
        Assert.Null(r.Gross);
        Assert.Null(r.Net);
        Assert.Null(r.WaterDelta);
    }
}

public class PairRoleMachineTests
{
    [Fact]
    public void Starts_with_no_role()
    {
        Assert.Equal(PairSide.None, new PairRoleMachine().Input);
    }

    [Fact]
    public void First_edit_claims_the_input_role()
    {
        var pair = new PairRoleMachine();
        pair.UserEdited(PairSide.A);
        Assert.Equal(PairSide.A, pair.Input);
    }

    [Fact]
    public void Editing_the_partner_flips_the_role()
    {
        var pair = new PairRoleMachine();
        pair.UserEdited(PairSide.A);
        pair.UserEdited(PairSide.B);
        Assert.Equal(PairSide.B, pair.Input);
    }

    [Fact]
    public void Editing_the_same_side_keeps_the_role()
    {
        var pair = new PairRoleMachine();
        pair.UserEdited(PairSide.B);
        pair.UserEdited(PairSide.B);
        Assert.Equal(PairSide.B, pair.Input);
    }

    [Fact]
    public void Reset_clears_both_roles()
    {
        var pair = new PairRoleMachine();
        pair.UserEdited(PairSide.A);
        pair.Reset();
        Assert.Equal(PairSide.None, pair.Input);
    }

    [Fact]
    public void None_is_not_an_edit()
    {
        var pair = new PairRoleMachine();
        pair.UserEdited(PairSide.A);
        pair.UserEdited(PairSide.None);
        Assert.Equal(PairSide.A, pair.Input);
    }
}
