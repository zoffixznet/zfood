using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.Tests;

public class LogViewModelTests
{
    private static LogEntry Entry(DateTimeOffset ts, string result = "496")
        => new()
        {
            Ts = ts,
            Panel = LogPanel.Portion,
            Unit = LogEntryFactory.PortionUnit,
            Result = result,
            ResultUnit = "cal",
            Equation = "250 g = 775 cal (3.10 cal/g) · eaten 160 g",
            Inputs = new Dictionary<string, string> { ["servingG"] = "250" },
        };

    [Fact]
    public void Rows_show_the_full_creation_date_and_time_with_seconds()
    {
        // Local wall-clock time, so the expected text is timezone-independent.
        var ts = new DateTimeOffset(new DateTime(2026, 7, 20, 14, 40, 12));
        var vm = new LogViewModel(new CalculationLog(sink: null, existing: new[] { Entry(ts) }));

        Assert.Equal("2026-07-20 14:40:12", Assert.Single(vm.Recent).Time);
        Assert.Equal("2026-07-20 14:40:12", Assert.Single(vm.Drawer).Time);
    }

    [Fact]
    public void Strip_holds_the_newest_three_and_the_drawer_everything_newest_first()
    {
        var t0 = new DateTimeOffset(new DateTime(2026, 7, 20, 10, 0, 0));
        var entries = Enumerable.Range(0, 5).Select(i => Entry(t0.AddMinutes(i), result: i.ToString()));
        var vm = new LogViewModel(new CalculationLog(sink: null, existing: entries.ToArray()));

        Assert.Equal(new[] { "4 cal", "3 cal", "2 cal" }, vm.Recent.Select(r => r.Result));
        Assert.Equal(new[] { "4 cal", "3 cal", "2 cal", "1 cal", "0 cal" }, vm.Drawer.Select(r => r.Result));
    }

    [Fact]
    public void Rows_carry_the_bare_result_for_copying()
    {
        var vm = new LogViewModel(new CalculationLog(
            sink: null, existing: new[] { Entry(DateTimeOffset.Now, result: "-200") }));

        Assert.Equal("-200", Assert.Single(vm.Recent).CopyText);
        Assert.Equal("-200 cal", Assert.Single(vm.Recent).Result);
    }
}
