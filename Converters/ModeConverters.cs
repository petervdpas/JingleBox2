using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JingleBox2.Converters;

/// <summary>
/// Whether the bound string is the one named in the parameter, ignoring case.
/// </summary>
/// <remarks>
/// For a page where one of several modes is on and every control asks the same question about a
/// different answer. The comparison ignores case because what is bound is a mode's name rather
/// than a key, and a name that reads right is worth more than one that compares fast.
/// </remarks>
public sealed class StringEqualsConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly StringEqualsConverter Instance = new();

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Something asked it for anything but a bool.</exception>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(bool))
            throw new InvalidOperationException($"StringEqualsConverter can only convert to bool");

        var strValue = value as string ?? "";
        var strParam = parameter as string ?? "";
        return string.Equals(strValue, strParam, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// The colour a mode button wears: lit when its mode is the one that is on, and grey otherwise.
/// </summary>
public sealed class ModeToColorConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly ModeToColorConverter Instance = new();

    /// <summary>The mode that is on.</summary>
    private const string Active = "#3B82F6";

    /// <summary>Every other mode.</summary>
    private const string Inactive = "#4B5563";

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = value as string ?? "";
        var expectedMode = parameter as string ?? "";

        if (string.Equals(mode, expectedMode, StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Color.Parse(Active));
        else
            return new SolidColorBrush(Color.Parse(Inactive));
    }

    /// <inheritdoc/>
    /// <remarks>A one way conversion: what it answers cannot be turned back into what it was given.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
