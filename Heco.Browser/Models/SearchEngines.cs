using System;
using System.Linq;

namespace Heco.Browser.Models;

/// <summary>
/// Nguồn duy nhất chứa danh sách công cụ tìm kiếm, engine mặc định và cách dựng URL tìm kiếm.
/// Mọi nơi (address bar, autocomplete, cài đặt) phải dùng chung để tránh lệch engine mặc định.
/// </summary>
public static class SearchEngines
{
    /// <summary>Thứ tự đúng như hiển thị trong dropdown cài đặt.</summary>
    public static readonly string[] All =
    {
        "DuckDuckGo", "Google", "Bing", "Brave Search", "Yahoo", "Yandex",
        "Baidu", "Ecosia", "Startpage", "Qwant", "Ask.com",
    };

    /// <summary>Công cụ tìm kiếm mặc định của app (khớp HomePageUrl mặc định trong ProfileSettings).</summary>
    public const string Default = "DuckDuckGo";

    /// <summary>Chuẩn hoá giá trị tồn tại trong All; giá trị không hợp lệ → Default (DuckDuckGo).</summary>
    public static string Normalize(string? engine)
        => string.IsNullOrWhiteSpace(engine) ? Default
         : All.Contains(engine) ? engine
         : Default;

    public static int IndexOf(string? engine) => Array.IndexOf(All, Normalize(engine));

    /// <summary>Dựng URL tìm kiếm theo engine cho một truy vấn (query đã escape).</summary>
    public static string BuildUrl(string engine, string escapedQuery)
        => Normalize(engine) switch
        {
            "DuckDuckGo" => "https://duckduckgo.com/?q=" + escapedQuery,
            "Bing" => "https://www.bing.com/search?q=" + escapedQuery,
            "Brave Search" => "https://search.brave.com/search?q=" + escapedQuery,
            "Yahoo" => "https://search.yahoo.com/search?p=" + escapedQuery,
            "Yandex" => "https://yandex.com/search/?text=" + escapedQuery,
            "Baidu" => "https://www.baidu.com/s?wd=" + escapedQuery,
            "Ecosia" => "https://www.ecosia.org/search?q=" + escapedQuery,
            "Startpage" => "https://www.startpage.com/sp/search?query=" + escapedQuery,
            "Qwant" => "https://www.qwant.com/?q=" + escapedQuery,
            "Ask.com" => "https://www.ask.com/web?q=" + escapedQuery,
            _ => "https://duckduckgo.com/?q=" + escapedQuery,
        };
}