using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JingleBox2.Views;

/// <summary>Bolds an instrument's track label when it actually has one.</summary>
public sealed class TrackWeight : IValueConverter
{
    public static readonly TrackWeight Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.SemiBold : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
