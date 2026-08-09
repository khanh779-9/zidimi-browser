using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Zidimi.Browser.Controls;

/// <summary>Bool → Visibility (Visible/Collapsed by default). Use ConverterParameter = "invert" to flip it.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool bb && bb;
        if (parameter is string s && s == "invert") b = !b;
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

/// <summary>Turns the string entered in the address bar into a valid URL (prepends http:// when missing).</summary>
public sealed class StringToUrlConverter : IValueConverter{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            s = s.Trim();
            if (string.IsNullOrEmpty(s)) return "";
            if (Uri.IsWellFormedUriString(s, UriKind.Absolute)) return s;
            if (s.Contains('.') && !s.Contains(' '))
                return "https://" + s;

var engine = Zidimi.Browser.Models.AppSettings.Profile.SearchEngine;
            return Zidimi.Browser.Models.SearchEngines.BuildUrl(engine, Uri.EscapeDataString(s));
        }
        return "";
    }
}

