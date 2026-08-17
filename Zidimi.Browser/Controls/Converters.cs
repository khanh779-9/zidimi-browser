using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Zidimi.Browser.Controls;

/// <summary>Bool → Visibility (Visible/Collapsed by default). Use ConverterParameter = "invert" to flip it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = parameter as string;
        var b = mode?.Equals("notempty", StringComparison.OrdinalIgnoreCase) == true
            ? value is string text && !string.IsNullOrWhiteSpace(text)
            : value is bool flag && flag;

        if (mode?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Object → Visibility: null → Collapsed, a value → Visible. Use ConverterParameter = "invert" to flip it.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vis = value == null ? Visibility.Collapsed : Visibility.Visible;
        if (parameter is string s && s == "invert")
            vis = vis == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        return vis;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
