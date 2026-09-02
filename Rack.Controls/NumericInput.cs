using System;
using System.Globalization;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class NumericInput : INumericInput
{
    /// <inheritdoc/>
    public double Step(double value, double delta, double step, double minimum, double maximum) =>
        Clamp(value + delta * step, minimum, maximum);

    /// <inheritdoc/>
    public double Clamp(double value, double minimum, double maximum) =>
        maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);

    /// <inheritdoc/>
    public double Parse(string? text, double fallback, double minimum, double maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Clamp(parsed, minimum, maximum)
            : fallback;
    }

    /// <inheritdoc/>
    public string Format(double value, string? format) =>
        value.ToString(string.IsNullOrEmpty(format) ? "0" : format, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string Widest(double value, double minimum, double maximum, string? format, string? unit)
    {
        string longest = Format(value, format);
        string low = Format(minimum, format);
        string high = Format(maximum, format);

        if (low.Length > longest.Length) longest = low;
        if (high.Length > longest.Length) longest = high;

        return longest + (unit ?? "");
    }
}
