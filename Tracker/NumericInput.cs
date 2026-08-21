using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// The value logic behind a number field: stepping, clamping, and reading typed text.
/// Pure, so the control above it stays about input handling.
/// </summary>
public static class NumericInput
{
    /// <summary>Steps a value and keeps it in range. Returns the value unchanged at a limit.</summary>
    public static double Step(double value, double delta, double step, double minimum, double maximum) =>
        Clamp(value + delta * step, minimum, maximum);

    public static double Clamp(double value, double minimum, double maximum) =>
        maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);

    /// <summary>
    /// Reads what the user typed. Anything unparseable leaves the value alone rather than
    /// resetting it to zero, so a stray keystroke does not wipe a tempo.
    /// </summary>
    public static double Parse(string? text, double fallback, double minimum, double maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Clamp(parsed, minimum, maximum)
            : fallback;
    }

    public static string Format(double value, string? format) =>
        value.ToString(string.IsNullOrEmpty(format) ? "0" : format, CultureInfo.InvariantCulture);
}
