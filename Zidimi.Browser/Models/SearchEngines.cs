using System;
using System.Linq;

namespace Zidimi.Browser.Models;

/// <summary>
/// Fallback URL resolver for Zidimi's WPF omnibox. Chromium remains the owner of the selected
/// default search provider (TemplateURLService/Web Data). Zidimi reads the provider name through
/// CEF when available and uses these known templates only to turn WPF omnibox text into a URL.
/// </summary>
public static class SearchEngines
{
    /// <summary>Known Chromium providers that the WPF omnibox can resolve without its own database.</summary>
    public static readonly string[] All =
    {
        "DuckDuckGo", "Google", "Bing", "Brave Search", "Yahoo", "Yandex",
        "Baidu", "Ecosia", "Startpage", "Qwant", "Ask.com",
    };

    /// <summary>Safe fallback only when Chromium does not expose a recognizable provider name.</summary>
    public const string Default = "DuckDuckGo";

    /// <summary>Normalizes a value that exists in All; invalid values fall back to Default (DuckDuckGo).</summary>
    public static string Normalize(string? engine)
    {
        if (string.IsNullOrWhiteSpace(engine)) return Default;
        return All.FirstOrDefault(item => item.Equals(engine.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    /// <summary>Builds the search URL for an engine given a query (already escaped).</summary>
    public static string BuildUrl(string engine, string escapedQuery)
        => Normalize(engine) switch
        {
            "DuckDuckGo" => "https://duckduckgo.com/?q=" + escapedQuery,
            "Google" => "https://www.google.com/search?q=" + escapedQuery,
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


    /// <summary>
    /// Uses a Chromium default_search_provider.search_url template when CEF exposes one.
    /// Unsupported TemplateURL tokens deliberately fall back to the known-provider resolver.
    /// </summary>
    public static string BuildFromChromiumTemplate(string? template, string engine, string rawQuery)
    {
        var escaped = Uri.EscapeDataString(rawQuery ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(template) && template.Contains("{searchTerms}", StringComparison.Ordinal))
        {
            var candidate = template
                .Replace("{searchTerms}", escaped, StringComparison.Ordinal)
                .Replace("{inputEncoding}", "UTF-8", StringComparison.Ordinal)
                .Replace("{outputEncoding}", "UTF-8", StringComparison.Ordinal);

            // TemplateURL supports many provider-specific replacement tokens. If any remain,
            // leave expansion to Chromium by falling back instead of generating a malformed URL.
            if (!candidate.Contains('{') &&
                Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                return uri.AbsoluteUri;
        }

        return BuildUrl(engine, escaped);
    }
}
