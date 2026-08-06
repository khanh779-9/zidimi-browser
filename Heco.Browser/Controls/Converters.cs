using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Heco.Browser.Controls;

/// <summary>Bool → Visibility (mặc định Visible/Collapsed). ConverterParameter = "invert" để đảo ngược.</summary>
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

/// <summary>Object → Visibility: null → Collapsed, có giá trị → Visible. ConverterParameter = "invert" để đảo.</summary>
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

/// <summary>Chuỗi nhập vào address bar trở thành URL hợp lệ (add http:// nếu thiếu).</summary>
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

            var engine = Heco.Browser.Models.AppSettings.Profile.SearchEngine;
            var query = Uri.EscapeDataString(s);
            return engine switch
            {
                "DuckDuckGo" => "https://duckduckgo.com/?q=" + query,
                "Bing" => "https://www.bing.com/search?q=" + query,
                "Brave Search" => "https://search.brave.com/search?q=" + query,
                "Yahoo" => "https://search.yahoo.com/search?p=" + query,
                "Yandex" => "https://yandex.com/search/?text=" + query,
                "Baidu" => "https://www.baidu.com/s?wd=" + query,
                "Ecosia" => "https://www.ecosia.org/search?q=" + query,
                "Startpage" => "https://www.startpage.com/sp/search?query=" + query,
                "Qwant" => "https://www.qwant.com/?q=" + query,
                "Ask.com" => "https://www.ask.com/web?q=" + query,
                _ => "https://www.google.com/search?q=" + query
            };
        }
        return "";
    }
}

