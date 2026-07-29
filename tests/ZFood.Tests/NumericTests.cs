using ZFood.Core;

namespace ZFood.Tests;

public class NumericTests
{
    [Theory]
    [InlineData("128", 128)]
    [InlineData("128.5", 128.5)]
    [InlineData("128,5", 128.5)]
    [InlineData(" 42 ", 42)]
    [InlineData("0", 0)]
    [InlineData("0.0", 0)]
    [InlineData(".5", 0.5)]
    [InlineData(",5", 0.5)]
    public void Parses_valid_input_with_either_decimal_separator(string text, double expected)
    {
        Assert.Equal(expected, Numeric.ParseNonNegative(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-5")]
    [InlineData("- 5")]
    [InlineData("5-")]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e5")]
    [InlineData("0x10")]
    public void Rejects_garbage_negatives_and_exotic_formats(string? text)
    {
        Assert.Null(Numeric.ParseNonNegative(text));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(496.4, "496")]
    [InlineData(496.5, "497")]
    [InlineData(-199.5, "-200")]
    [InlineData(-200.4, "-200")]
    [InlineData(0.4, "0")]
    [InlineData(-0.4, "0")]
    [InlineData(-0.0, "0")]
    [InlineData(1234, "1234")]
    public void Formats_whole_numbers_rounding_half_away_from_zero(double value, string expected)
    {
        Assert.Equal(expected, Numeric.FormatWhole(value));
    }

    [Theory]
    [InlineData(3.1, "3.10")]
    [InlineData(4.4642857142857135, "4.46")]
    [InlineData(0, "0.00")]
    public void Formats_density_with_two_invariant_decimals(double value, string expected)
    {
        Assert.Equal(expected, Numeric.FormatDensity(value));
    }

    [Theory]
    [InlineData(800, "800")]
    [InlineData(799.5, "799.5")]
    [InlineData(0.12345, "0.1235")]
    public void Formats_editable_values_compactly(double value, string expected)
    {
        Assert.Equal(expected, Numeric.FormatEditable(value));
    }
}
