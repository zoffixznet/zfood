using ZFood.App.ViewModels;

namespace ZFood.Tests;

public class PortionViewModelTests
{
    [Fact]
    public void Edits_offer_the_computed_eaten_member_for_the_live_clipboard()
    {
        var vm = new PortionViewModel();
        var copies = new List<string>();
        vm.AutoCopy += copies.Add;

        vm.ServingGramsText = "56";
        vm.ServingCaloriesText = "250";
        Assert.Empty(copies); // no eaten member typed yet, nothing to offer

        vm.EatenGramsText = "128"; // grams entry offers the calories
        Assert.Equal("571", copies.Last());

        vm.EatenCaloriesText = "250"; // calories entry offers the grams
        Assert.Equal("56", copies.Last());

        // A serving edit re-derives the sticky computed member and offers it.
        copies.Clear();
        vm.ServingGramsText = "112";
        Assert.Equal("112", copies.Last()); // 250 cal at 250 cal / 112 g
    }

    [Fact]
    public void Placeholder_partners_are_never_offered_to_the_live_clipboard()
    {
        var vm = new PortionViewModel();
        var copies = new List<string>();
        vm.AutoCopy += copies.Add;

        // The serving pair is missing, so the partner degrades to a dash.
        vm.EatenGramsText = "128";
        Assert.Empty(copies);

        // A zero serving weight blanks the density and both eaten directions.
        vm.ServingGramsText = "0";
        vm.ServingCaloriesText = "250";
        Assert.Empty(copies);

        vm.EatenCaloriesText = "250"; // grams direction cannot compute either
        Assert.Empty(copies);
    }
}
