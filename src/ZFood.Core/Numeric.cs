using System.Globalization;

namespace ZFood.Core;

/// <summary>
/// Numeric input parsing and output formatting rules shared by the whole app.
/// Input accepts "." or "," as the decimal separator and never accepts negative
/// values (nothing the user enters is physically negative). All output uses
/// invariant "." formatting so copied values paste cleanly anywhere.
/// </summary>
public static class Numeric
{
    /// <summary>
    /// Parses a user-entered non-negative number. Returns null for empty,
    /// unparseable, negative, or non-finite input.
    /// </summary>
    public static double? ParseNonNegative(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim().Replace(',', '.');
        if (t.Contains('-'))
            return null;

        if (!double.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return null;

        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            return null;

        return value;
    }

    /// <summary>
    /// Parses a displayed number for copying. Unlike user input, displayed
    /// values may carry a leading hyphen-minus (the water delta, a net weight
    /// below tare). Returns null for empty text, placeholder dashes, or
    /// anything else that is not a finite number.
    /// </summary>
    public static double? ParseDisplayed(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var t = text.Trim().Replace(',', '.');
        if (!double.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    }

    /// <summary>
    /// Formats grams or calories for display and copy: whole number, half
    /// rounded away from zero, explicit sign when negative, invariant culture.
    /// A small negative value that rounds to zero renders "0", never "-0".
    /// </summary>
    public static string FormatWhole(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        if (rounded == 0)
            rounded = 0; // normalize negative zero
        return rounded.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>Formats calorie density (cal/g) with two decimals, invariant culture.</summary>
    public static string FormatDensity(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a value for transfer into an editable field, keeping up to four
    /// decimals so chained calculations do not accumulate rounding error.
    /// </summary>
    public static string FormatEditable(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}
