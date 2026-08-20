using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JingleBox2.Converters;

public sealed class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (targetType != typeof(bool))
            throw new InvalidOperationException($"StringEqualsConverter can only convert to bool");

        var strValue = value as string ?? "";
        var strParam = parameter as string ?? "";
        return string.Equals(strValue, strParam, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ModeToColorConverter : IValueConverter
{
    public static readonly ModeToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = value as string ?? "";
        var expectedMode = parameter as string ?? "";

        if (string.Equals(mode, expectedMode, StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Color.Parse("#3B82F6")); // Blue for active
        else
            return new SolidColorBrush(Color.Parse("#4B5563")); // Grey for inactive
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
