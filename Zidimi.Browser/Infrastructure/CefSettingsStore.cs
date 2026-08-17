using System.Collections;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Bridge between Zidimi's WPF settings model and registered Chromium profile preferences.
/// Every persistent read/write goes through IRequestContext GetPreference/SetPreferenceAsync or
/// CEF content-setting APIs. Zidimi never patches Local State, Preferences, Secure Preferences,
/// Cookies, SQLite databases, or a private settings file.
///
/// CefSharp 150 does not expose CEF's process-global CefPreferenceManager. Local-State-only values
/// such as profile.show_picker_on_startup/profile.last_used therefore remain Chromium-owned and are
/// not shadowed in another store. The shell derives safe runtime behavior from the native profiles.
/// </summary>
internal static class CefSettingsStore
{
    private static readonly object PendingNameGate = new();
    private static readonly Dictionary<string, string> PendingProfileNames = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<GlobalSettings> ReadGlobalAsync()
    {
        var result = new GlobalSettings();
        if (Cef.IsInitialized != true) return result;

        var discovered = ChromiumProfileCatalog.DiscoverProfileIds();
        result.Profiles = discovered.ToList();
        result.CurrentProfile = discovered.FirstOrDefault() ?? UserDataPaths.DefaultProfileId;

        // No private persistence for Chromium Local-State-only picker/last-used values. When more
        // than one native profile exists, showing the picker is the safest Chromium-like default.
        result.ShowProfilePickerOnStartup = result.Profiles.Count > 1;

        var context = Cef.GetGlobalRequestContext();
        if (context == null || context.IsDisposed) return result;

        try
        {
            var selected = AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.SelectedLanguages)
                .ConfigureAwait(false));
            var accepted = AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.AcceptLanguages)
                .ConfigureAwait(false));
            var language = FirstLanguage(selected) ?? FirstLanguage(accepted);
            if (!string.IsNullOrWhiteSpace(language))
                result.DisplayLanguage = LanguageManager.NormalizeUiCode(language);

            var proxy = AsDictionary(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.Proxy)
                .ConfigureAwait(false));
            if (proxy != null && proxy.TryGetValue("mode", out var modeValue) && AsString(modeValue) is { } mode)
                result.UseSystemProxy = mode.Equals("system", StringComparison.OrdinalIgnoreCase);

            AppLogger.Log("CEFPreferences",
                $"Read native Chromium profile state: profile={result.CurrentProfile}, " +
                $"pickerDerived={result.ShowProfilePickerOnStartup}, profiles={result.Profiles.Count}, " +
                $"language={result.DisplayLanguage}.");
        }
        catch (Exception ex)
        {
            AppLogger.Log("CEFPreferences", ex, "Reading registered Chromium preferences through CefSharp.");
        }

        return result;
    }

    public static async Task<ProfileSettings> ReadProfileAsync(
        IRequestContext? context,
        GlobalSettings global,
        string profileId)
    {
        var result = new ProfileSettings();
        if (context == null || context.IsDisposed || Cef.IsInitialized != true) return result;

        try
        {
            if (AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.ProfileName)
                    .ConfigureAwait(false)) is { Length: > 0 } name)
                result.DisplayName = name.Trim();

            var homepageIsNewTab = AsBool(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.HomepageIsNewTabPage)
                .ConfigureAwait(false)) == true;
            if (homepageIsNewTab)
            {
                result.HomePageUrl = "chrome://newtab/";
            }
            else if (AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.Homepage)
                         .ConfigureAwait(false)) is { Length: > 0 } homepage)
            {
                result.HomePageUrl = homepage;
            }

            if (AsBool(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.SearchSuggestEnabled)
                    .ConfigureAwait(false)) is { } suggest)
                result.SearchSuggestEnabled = suggest;

            if (AsInt(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.SessionRestoreOnStartup)
                    .ConfigureAwait(false)) is { } startup)
                result.StartupBehavior = FromChromiumStartupValue(startup);

            var startupUrls = AsStringList(await context.GetPreferenceSafeAsync(
                ChromiumPreferenceKeys.SessionStartupUrls).ConfigureAwait(false));
            if (startupUrls.Count > 0) result.StartupPages = startupUrls;

            var providerName = AsString(await context.GetPreferenceSafeAsync(
                ChromiumPreferenceKeys.DefaultSearchProviderName).ConfigureAwait(false));
            if (!string.IsNullOrWhiteSpace(providerName))
                result.SearchEngine = providerName.Trim();

            var searchUrl = AsString(await context.GetPreferenceSafeAsync(
                ChromiumPreferenceKeys.DefaultSearchProviderSearchUrl).ConfigureAwait(false));
            if (!string.IsNullOrWhiteSpace(searchUrl))
                result.SearchUrlTemplate = searchUrl.Trim();

            if (AsInt(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.BrowserColorScheme)
                    .ConfigureAwait(false)) is { } colorScheme)
                result.Theme = FromChromiumColorScheme(colorScheme);

            var selected = AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.SelectedLanguages)
                .ConfigureAwait(false));
            var accepted = AsString(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.AcceptLanguages)
                .ConfigureAwait(false));
            var language = FirstLanguage(selected) ?? FirstLanguage(accepted);
            if (!string.IsNullOrWhiteSpace(language))
                global.DisplayLanguage = LanguageManager.NormalizeUiCode(language);

            var proxy = AsDictionary(await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.Proxy)
                .ConfigureAwait(false));
            if (proxy != null && proxy.TryGetValue("mode", out var modeValue) && AsString(modeValue) is { } mode)
                global.UseSystemProxy = mode.Equals("system", StringComparison.OrdinalIgnoreCase);

            await CefContentSettingsBridge.RefreshSupportedDefaultsAsync(context, result.SitePermissions)
                .ConfigureAwait(false);

            var pending = GetPendingProfileDisplayName(profileId);
            if (!string.IsNullOrWhiteSpace(pending)) result.DisplayName = pending;
        }
        catch (Exception ex)
        {
            AppLogger.Log("CEFPreferences", ex,
                $"Reading Chromium profile preferences '{profileId}' through CefSharp.");
        }

        return result;
    }

    public static async Task WriteGlobalAsync(GlobalSettings global)
    {
        if (Cef.IsInitialized != true) return;

        IRequestContext? context = null;
        try
        {
            var profileId = UserDataPaths.NormalizeProfileId(global.CurrentProfile);
            if (App.RequestContexts != null)
                context = await App.RequestContexts.GetProfileContextReadyAsync(profileId).ConfigureAwait(false);
            context ??= Cef.GetGlobalRequestContext();
            await WriteProfileRuntimeGlobalsAsync(context, global).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.Log("CEFPreferences", ex, "Writing process choices to registered Chromium profile preferences.");
        }
    }

    public static async Task WriteProfileRuntimeGlobalsAsync(IRequestContext? context, GlobalSettings global)
    {
        if (context == null || context.IsDisposed || Cef.IsInitialized != true) return;

        var language = LanguageManager.NormalizeUiCode(global.DisplayLanguage);
        await SetAsync(context, ChromiumPreferenceKeys.AcceptLanguages, BuildAcceptLanguages(language)).ConfigureAwait(false);
        await SetAsync(context, ChromiumPreferenceKeys.SelectedLanguages, language).ConfigureAwait(false);

        var proxy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["mode"] = global.UseSystemProxy ? "system" : "direct",
        };
        await SetAsync(context, ChromiumPreferenceKeys.Proxy, proxy).ConfigureAwait(false);
    }

    public static async Task WriteProfileAsync(
        IRequestContext? context,
        string profileId,
        ProfileSettings profile,
        GlobalSettings global)
    {
        if (context == null || context.IsDisposed || Cef.IsInitialized != true) return;

        var useNewTabAsHome = string.Equals(profile.HomePageUrl, "chrome://newtab/", StringComparison.OrdinalIgnoreCase);
        await SetAsync(context, ChromiumPreferenceKeys.HomepageIsNewTabPage, useNewTabAsHome).ConfigureAwait(false);
        if (!useNewTabAsHome)
            await SetAsync(context, ChromiumPreferenceKeys.Homepage, profile.HomePageUrl).ConfigureAwait(false);
        await SetAsync(context, ChromiumPreferenceKeys.SearchSuggestEnabled, profile.SearchSuggestEnabled)
            .ConfigureAwait(false);
        await SetAsync(context, ChromiumPreferenceKeys.SessionRestoreOnStartup,
            ToChromiumStartupValue(profile.StartupBehavior)).ConfigureAwait(false);
        await SetAsync(context, ChromiumPreferenceKeys.SessionStartupUrls,
            (profile.StartupPages ?? new List<string>()).Cast<object>().ToList()).ConfigureAwait(false);
        await SetAsync(context, ChromiumPreferenceKeys.BrowserColorScheme,
            ToChromiumColorScheme(profile.Theme)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            var displayName = profile.DisplayName.Trim();
            RememberProfileDisplayName(profileId, displayName);
            await SetAsync(context, ChromiumPreferenceKeys.ProfileName, displayName).ConfigureAwait(false);
            ChromiumProfileCatalog.RememberProfileInfo(profileId, displayName);
        }

        await WriteProfileRuntimeGlobalsAsync(context, global).ConfigureAwait(false);
        await CefContentSettingsBridge.ApplySupportedDefaultsAsync(context, profile.SitePermissions)
            .ConfigureAwait(false);
    }

    public static async Task RefreshProfileAsync(
        IRequestContext? context,
        string profileId,
        ProfileSettings target,
        GlobalSettings global)
    {
        var fresh = await ReadProfileAsync(context, global, profileId).ConfigureAwait(false);
        target.DisplayName = fresh.DisplayName;
        target.HomePageUrl = fresh.HomePageUrl;
        target.SearchEngine = fresh.SearchEngine;
        target.SearchUrlTemplate = fresh.SearchUrlTemplate;
        target.StartupBehavior = fresh.StartupBehavior;
        target.StartupPages = fresh.StartupPages;
        target.SearchSuggestEnabled = fresh.SearchSuggestEnabled;
        target.Theme = fresh.Theme;
        target.SitePermissions = fresh.SitePermissions;
    }

    public static void RememberProfileDisplayName(string profileId, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        lock (PendingNameGate)
            PendingProfileNames[UserDataPaths.NormalizeProfileId(profileId)] = displayName.Trim();
    }

    public static string? GetPendingProfileDisplayName(string profileId)
    {
        lock (PendingNameGate)
            return PendingProfileNames.TryGetValue(UserDataPaths.NormalizeProfileId(profileId), out var name)
                ? name
                : null;
    }

    internal static Dictionary<string, object>? AsDictionary(object? value)
    {
        if (value is IDictionary<string, object> generic)
            return new Dictionary<string, object>(generic, StringComparer.OrdinalIgnoreCase);

        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in dictionary)
                if (item.Key is string key && item.Value != null) result[key] = item.Value;
            return result;
        }
        return null;
    }

    internal static List<string> AsStringList(object? value)
    {
        var result = new List<string>();
        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) result.Add(text);
            return result;
        }
        if (value is IEnumerable enumerable)
            foreach (var item in enumerable)
                if (item is string s && !string.IsNullOrWhiteSpace(s)) result.Add(s);
        return result;
    }

    internal static string? AsString(object? value) => value as string;
    private static bool? AsBool(object? value) => value is bool b ? b : null;
    private static int? AsInt(object? value) => value switch
    {
        int i => i,
        long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
        double d when d is >= int.MinValue and <= int.MaxValue => (int)d,
        _ => null,
    };

    private static async Task SetAsync(IRequestContext context, string name, object value)
    {
        var success = await context.SetPreferenceSafeAsync(name, value).ConfigureAwait(false);
        if (!success) AppLogger.Log("CEFPreferences", $"Chromium did not expose writable preference '{name}'.");
    }

    private static string? FirstLanguage(string? list)
        => string.IsNullOrWhiteSpace(list)
            ? null
            : list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    private static string BuildAcceptLanguages(string language)
    {
        var normalized = language.Replace('_', '-');
        var dash = normalized.IndexOf('-');
        return dash > 0 ? $"{normalized},{normalized[..dash]}" : normalized;
    }

    private static int ToChromiumStartupValue(int behavior) => behavior switch { 1 => 1, 2 => 4, _ => 5 };
    private static int FromChromiumStartupValue(int value) => value switch { 1 => 1, 4 => 2, _ => 0 };

    // Chromium BrowserColorScheme values (theme_service.h): System=0, Light=1, Dark=2.
    // Chromium BrowserColorScheme is used directly by both native WebUI and the Zidimi shell.
    private static int ToChromiumColorScheme(string? theme) => ThemeManager.NormalizeThemeKey(theme) switch
    {
        "light" => 1,
        "dark" => 2,
        _ => 0,
    };

    private static string FromChromiumColorScheme(int value) => value switch
    {
        1 => "light",
        2 => "dark",
        _ => "system",
    };
}
