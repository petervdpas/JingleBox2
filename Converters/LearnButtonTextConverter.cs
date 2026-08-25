using System;
using Avalonia.Data.Converters;

namespace JingleBox2.Converters;

public sealed class LearnButtonTextConverter : IValueConverter
{
    public static readonly LearnButtonTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? "Learning..." : "Learn";

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}