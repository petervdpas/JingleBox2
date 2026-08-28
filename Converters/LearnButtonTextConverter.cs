using System;
using Avalonia.Data.Converters;

namespace JingleBox2.Converters;

/// <summary>
/// What the learn button says: whether it is waiting for you to touch something, or offering to.
/// </summary>
public sealed class LearnButtonTextConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly LearnButtonTextConverter Instance = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? "Learning..." : "Learn";

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}