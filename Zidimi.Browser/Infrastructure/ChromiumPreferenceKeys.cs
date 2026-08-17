namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Chromium preference names currently consumed by Zidimi's shell UI.
///
/// Keep string keys centralized here instead of scattering them across Views/Handlers. Preference
/// availability is Chromium-version dependent: callers must go through CefPreferenceExtensions,
/// which checks CanSetPreference before a write and safely ignores unsupported/read-only keys.
///
/// Only registered profile preferences belong here. Process-global Local State keys are deliberately
/// excluded because CefSharp 150 does not expose CEF's global CefPreferenceManager in managed code.
/// </summary>
internal static class ChromiumPreferenceKeys
{
    // Standard Chromium profile preferences used by Zidimi's settings UI. Writes always go
    // through IRequestContext.SetPreference; protected preferences must never be patched in JSON.
    public const string ProfileName = "profile.name";
    public const string Homepage = "homepage";
    public const string HomepageIsNewTabPage = "homepage_is_newtabpage";
    public const string SessionRestoreOnStartup = "session.restore_on_startup";
    public const string SessionStartupUrls = "session.startup_urls";
    public const string SearchSuggestEnabled = "search.suggest_enabled";
    public const string DefaultSearchProviderName = "default_search_provider.name";
    public const string DefaultSearchProviderSearchUrl = "default_search_provider.search_url";
    public const string ExtensionSettings = "extensions.settings";
    public const string PinnedExtensions = "extensions.pinned_extensions";
    public const string AcceptLanguages = "intl.accept_languages";
    public const string SelectedLanguages = "intl.selected_languages";
    // Chromium BrowserColorScheme: System=0, Light=1, Dark=2.
    public const string BrowserColorScheme = "browser.theme.color_scheme2";

    public const string DefaultFontSize = "webkit.webprefs.default_font_size";
    public const string DefaultFixedFontSize = "webkit.webprefs.default_fixed_font_size";
    public const string DefaultZoomLevel = "partition.default_zoom_level";

    public const string CookieControlsMode = "profile.cookie_controls_mode";
    public const string EnableDoNotTrack = "enable_do_not_track";
    public const string SafeBrowsingEnabled = "safebrowsing.enabled";

    public const string DownloadDefaultDirectory = "download.default_directory";
    public const string DownloadPromptForDownload = "download.prompt_for_download";

    // Per-profile proxy configuration. Keep proxy mutable at runtime; do not shadow it with
    // --no-proxy-server or another command-line switch.
    public const string Proxy = "proxy";
}
