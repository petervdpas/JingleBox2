using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JingleBox2.Converters;

/// <summary>
/// A colour written as text, as something that can be painted with.
/// </summary>
/// <remarks>
/// The machines' colours live on <see cref="Tracker.Machine"/>, which knows nothing about
/// drawing and should not: a colour is part of what a machine is, the way its name is. This is
/// the one place that turns those strings into brushes, so the rack, the picker, the panel and
/// the song's list all read the same fact the same way.
/// </remarks>
public sealed class HexBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    /// <param name="value">The colour as it is stored, such as "#E53935".</param>
    /// <param name="targetType">Unused: what comes back is a brush whatever was asked for.</param>
    /// <param name="parameter">
    /// How much of it to use, 0 to 1, for the washes a list row is tinted with. Left out, the
    /// colour is given whole.
    /// </param>
    /// <param name="culture">Unused: a hex colour reads the same everywhere.</param>
    /// <returns>The brush, or nothing at all where the string is blank or unreadable.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || hex.Length == 0) return AvaloniaProperty.UnsetValue;

        try
        {
            return new SolidColorBrush(Color.Parse(hex), Opacity(parameter));
        }
        catch (FormatException)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

    /// <summary>How see-through to paint it, taken from the parameter, and solid without one.</summary>
    private static double Opacity(object? parameter) =>
        parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)
            ? Math.Clamp(amount, 0, 1)
            : 1;

    /// <inheritdoc/>
    /// <remarks>A one way conversion: the opacity is applied on the way out and cannot be undone.</remarks>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A brush is not turned back into the colour it was written as.");
}
