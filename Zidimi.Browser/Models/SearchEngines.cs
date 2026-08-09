using System;
using System.Linq;

namespace Zidimi.Browser.Models;

/// <summary>
/// Single source of truth for the list of search engines, the default engine, and how to build search URLs.
/// Everywhere (address bar, autocomplete, settings) must share this to avoid default engine drift.
/// </summary>
public static class SearchEngines
{
    /// <summary>Order matches how they appear in the settings dropdown.</summary>
    public static readonly string[] All =
    {
        "DuckDuckGo", "Google", "Bing", "Brave Search", "Yahoo", "Yandex",
        "Baidu", "Ecosia", "Startpage", "Qwant", "Ask.com",
    };

    /// <summary>The app's default search engine (matches the default HomePageUrl in ProfileSettings).</summary>
    public const string Default = "DuckDuckGo";

    /// <summary>Normalizes a value that exists in All; invalid values fall back to Default (DuckDuckGo).</summary>
    public static string Normalize(string? engine)
        => string.IsNullOrWhiteSpace(engine) ? Default
         : All.Contains(engine) ? engine
         : Default;

    public static int IndexOf(string? engine) => Array.IndexOf(All, Normalize(engine));

    /// <summary>Builds the search URL for an engine given a query (already escaped).</summary>
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


    public static string GetEngineUrl(string engine)
    {
        return Normalize(engine) switch
        {
            "DuckDuckGo" => "https://duckduckgo.com/",
            "Google" => "https://www.google.com/",
            "Bing" => "https://www.bing.com/",
            "Brave Search" => "https://search.brave.com/",
            "Yahoo" => "https://search.yahoo.com/",
            "Yandex" => "https://yandex.com/",
            "Baidu" => "https://www.baidu.com/",
            "Ecosia" => "https://www.ecosia.org/",
            "Startpage" => "https://www.startpage.com/",
            "Qwant" => "https://www.qwant.com/",
            "Ask.com" => "https://www.ask.com/",
            _ => "https://duckduckgo.com/",
        };
    }
}