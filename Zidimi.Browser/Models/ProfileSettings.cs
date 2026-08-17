namespace Zidimi.Browser.Models;

/// <summary>
/// In-memory view of an active Chromium profile. Persistent browser fields are read/written through
/// IRequestContext preferences/content settings. Pure WPF-shell choices are session-only instead of
/// being disguised as browser cookies or serialized into a Zidimi-owned store.
/// </summary>
public class ProfileSettings
{
    public string DisplayName { get; set; } = string.Empty;
    public string HomePageUrl { get; set; } = "chrome://newtab/";
    public string SearchEngine { get; set; } = "DuckDuckGo";
    public string SearchUrlTemplate { get; set; } = string.Empty;
    public int StartupBehavior { get; set; }
    public List<string> StartupPages { get; set; } = new();
    public bool SearchSuggestEnabled { get; set; } = true;

    // Persisted through Chromium's browser.theme.color_scheme2 profile preference.
    public string Theme { get; set; } = "system";

    public SitePermissions SitePermissions { get; set; } = new();
}
