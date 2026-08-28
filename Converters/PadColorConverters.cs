using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JingleBox2.Converters;

/// <summary>
/// A hex colour as it is stored, such as "#E53935", and the colour a picker works in.
/// </summary>
/// <remarks>
/// A blank or unreadable string becomes a neutral grey rather than nothing, so the picker always
/// has a colour to show: a pad that has never been given one still has to be drawn.
/// </remarks>
public sealed class HexColorToColorConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly HexColorToColorConverter Instance = new();

    /// <summary>What is stored, as a colour.</summary>
    private const byte Grey = 128;

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try { return Color.Parse(s); }
            catch (Exception) { }
        }

        return Color.FromRgb(Grey, Grey, Grey);
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        return "";
    }
}

/// <summary>
/// How big a swatch is drawn: the one the pad is wearing grows, and the rest stay as they are.
/// </summary>
/// <remarks>
/// Size rather than a ring on its own, because a row of swatches is read at a glance and one that
/// is simply bigger is the thing the eye lands on. <see cref="IsSelectedColorConverter"/> draws
/// the ring over it, and the two are used together.
/// </remarks>
public sealed class SelectedSwatchSizeConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly SelectedSwatchSizeConverter Instance = new();

    /// <summary>How wide the swatch the pad is wearing is drawn.</summary>
    private const double Selected   = 32.0;

    /// <summary>How wide every other one is.</summary>
    private const double Unselected = 22.0;

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string ?? "";
        var swatch  = parameter as string ?? "";
        return string.Equals(current, swatch, StringComparison.OrdinalIgnoreCase)
            ? Selected
            : Unselected;
    }

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// The ring round the swatch the pad is wearing, and nothing round the rest.
/// </summary>
public sealed class IsSelectedColorConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly IsSelectedColorConverter Instance = new();

    /// <summary>How thick the ring is drawn.</summary>
    private const double Ring = 3;

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string ?? "";
        var swatch  = parameter as string ?? "";
        return string.Equals(current, swatch, StringComparison.OrdinalIgnoreCase)
            ? new Avalonia.Thickness(Ring)
            : new Avalonia.Thickness(0);
    }

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Nothing at all where the binding gave null, and the value itself otherwise.
/// </summary>
/// <remarks>
/// A local null is a value like any other and wins over the theme, so a pad with no colour of its
/// own bound straight to its background would lose the theme's brush and its hover and checked
/// states with it. Answering unset stands the binding down and lets the styles have it back.
/// </remarks>
public sealed class NullToUnsetConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly NullToUnsetConverter Instance = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? AvaloniaProperty.UnsetValue;

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
