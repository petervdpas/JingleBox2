using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace JingleBox2.Converters;

/// <summary>
/// How many more times, as the words somebody would use rather than as a number.
/// </summary>
/// <remarks>
/// The menu offers counts and a bare "1" beside a bare "2" reads as a choice of patterns rather
/// than as a choice of how many. One more time is "once more", and the rest say so plainly.
/// </remarks>
public sealed class TimesMoreConverter : IValueConverter
{
    /// <summary>The one of these there needs to be, for XAML to point at.</summary>
    public static readonly TimesMoreConverter Instance = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not int times
            ? ""
            : times <= 1
                ? "Once more"
                : times.ToString(CultureInfo.InvariantCulture) + " more times";

    /// <inheritdoc/>
    /// <remarks>A one way conversion: the words cannot be turned back into the number.</remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
