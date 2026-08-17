using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Central router for Zidimi's native browser pages. These URLs are shown in the
/// omnibox but are rendered by WPF, not sent to Chromium as a network request.
/// </summary>
public static class InternalUrlRouter
{
    public const string Scheme = "zidimi";
    public const string SettingsHost = "settings";
    public const string SettingsRoot = "zidimi://settings/";

    public readonly record struct Route(TabKind Kind, string Url, string? SettingsSection = null);

    private static readonly IReadOnlyDictionary<string, string> SettingsSlugToSection =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["general"] = "General",
            ["profiles"] = "Profiles",
            ["autofill"] = "Autofill",
            ["default-browser"] = "DefaultBrowser",
            ["privacy"] = "Privacy",
            ["site-permissions"] = "SitePermissions",
            ["appearance"] = "Appearance",
            ["search"] = "Search",
            // /downloads is the Downloads manager. Keep the settings section distinct.
            ["download-settings"] = "Downloads",
            ["languages"] = "Languages",
            ["system"] = "System",
            ["about"] = "About",
        };

    private static readonly IReadOnlyDictionary<string, string> SettingsSectionToSlug =
        SettingsSlugToSection.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static bool IsInternalUrl(string? value)
        => TryParse(value, out _);

    public static bool TryParse(string? value, out Route route)
    {
        route = default;
        var raw = value?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return false;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals(SettingsHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var slug = uri.AbsolutePath.Trim('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(slug))
        {
            route = new Route(TabKind.Settings, UrlForSettingsSection("Profiles"), "Profiles");
            return true;
        }

        switch (slug)
        {
            case "history":
                route = new Route(TabKind.History, UrlForKind(TabKind.History));
                return true;
            case "downloads":
                route = new Route(TabKind.Downloads, UrlForKind(TabKind.Downloads));
                return true;
            case "bookmarks":
                route = new Route(TabKind.Bookmarks, UrlForKind(TabKind.Bookmarks));
                return true;
            case "extensions":
                route = new Route(TabKind.Extensions, UrlForKind(TabKind.Extensions));
                return true;
        }

        if (!SettingsSlugToSection.TryGetValue(slug, out var section)) return false;
        route = new Route(TabKind.Settings, $"{SettingsRoot}{slug}", section);
        return true;
    }

    public static string UrlForKind(TabKind kind) => kind switch
    {
        TabKind.Settings => UrlForSettingsSection("Profiles"),
        TabKind.History => $"{SettingsRoot}history",
        TabKind.Bookmarks => $"{SettingsRoot}bookmarks",
        TabKind.Downloads => $"{SettingsRoot}downloads",
        TabKind.Extensions => $"{SettingsRoot}extensions",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Web tabs do not have a Zidimi internal URL."),
    };

    public static string UrlForSettingsSection(string? section)
    {
        var normalized = string.IsNullOrWhiteSpace(section) ? "Profiles" : section.Trim();
        return SettingsSectionToSlug.TryGetValue(normalized, out var slug)
            ? $"{SettingsRoot}{slug}"
            : $"{SettingsRoot}profiles";
    }

    public static string TitleFor(Route route) => route.Kind switch
    {
        TabKind.Settings => LanguageManager.Instance["Tab_SettingsTitle"],
        TabKind.History => LanguageManager.Instance["Tab_HistoryTitle"],
        TabKind.Bookmarks => LanguageManager.Instance["Tab_BookmarksTitle"],
        TabKind.Downloads => LanguageManager.Instance["Tab_DownloadsTitle"],
        TabKind.Extensions => LanguageManager.Instance["Tab_ExtensionsTitle"],
        _ => LanguageManager.Instance["Browser_ZidimiBrowser"],
    };
}
