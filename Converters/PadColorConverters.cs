using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JingleBox2.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#E53935") to an Avalonia Color and back.
/// Used by the ColorPicker in PadsView.
/// Empty/null string produces a neutral grey so the picker always shows a valid color.
/// </summary>
public sealed class HexColorToColorConverter : IValueConverter
{
    public static readonly HexColorToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try { return Color.Parse(s); }
            catch { /* fall through */ }
        }

        // Default: neutral grey shown when no color is configured
        return Color.FromRgb(128, 128, 128);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        return "";
    }
}

/// <summary>
/// Returns a larger size (double) when the bound PadColor matches the ConverterParameter,
/// otherwise the base size. Used to grow the selected swatch.
/// </summary>
public sealed class SelectedSwatchSizeConverter : IValueConverter
{
    public static readonly SelectedSwatchSizeConverter Instance = new();

    private const double Selected   = 32.0;
    private const double Unselected = 22.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string ?? "";
        var swatch  = parameter as string ?? "";
        return string.Equals(current, swatch, StringComparison.OrdinalIgnoreCase)
            ? Selected
            : Unselected;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns a BorderThickness of 3 when the bound PadColor matches the ConverterParameter
/// (swatch color), 0 otherwise. Used to draw a selection ring on the active swatch.
/// </summary>
public sealed class IsSelectedColorConverter : IValueConverter
{
    public static readonly IsSelectedColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string ?? "";
        var swatch  = parameter as string ?? "";
        return string.Equals(current, swatch, StringComparison.OrdinalIgnoreCase)
            ? new Avalonia.Thickness(3)
            : new Avalonia.Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns AvaloniaProperty.UnsetValue for null, otherwise passes the value through.
/// Used on the ToggleButton Background binding so that when PadBackground is null
/// the Avalonia style system takes over (showing theme PadBrush / hover / checked states).
/// </summary>
public sealed class NullToUnsetConverter : IValueConverter
{
    public static readonly NullToUnsetConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? AvaloniaProperty.UnsetValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
