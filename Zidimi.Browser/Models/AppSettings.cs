using CefSharp;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Models;

/// <summary>
/// WPF-facing settings facade. Chromium/CEF is the persistent owner: registered browser values
/// use IRequestContext GetPreference/SetPreference and permissions use CEF content-setting APIs.
/// Zidimi never parses/rewrites Chromium settings files and never creates a parallel settings store.
/// </summary>
public static class AppSettings
{
    public static GlobalSettings Global { get; private set; } = new();
    public static ProfileSettings Profile { get; private set; } = new();

    /// <summary>Creates only safe in-memory defaults. Persistent settings are loaded after CEF is ready.</summary>
    public static void InitializeDefaults()
    {
        Global = new GlobalSettings();
        Profile = new ProfileSettings();
        NormalizeGlobalSettings();
        NormalizeProfileSettings();
    }

    /// <summary>
    /// Reads registered Chromium preferences and active profile state after
    /// IBrowserProcessHandler.OnContextInitialized has fired.
    /// </summary>
    public static async Task LoadFromCefAsync()
    {
        if (Cef.IsInitialized != true || !App.CefReady) return;

        Global = await CefSettingsStore.ReadGlobalAsync().ConfigureAwait(false);
        NormalizeGlobalSettings();

        await ChromiumProfileCatalog.RefreshFromCefAsync(Global.Profiles).ConfigureAwait(false);
        NormalizeGlobalSettings();
        await LoadProfileAsync(Global.CurrentProfile).ConfigureAwait(false);
    }

    public static void LoadProfile(string profileId)
        => LoadProfileAsync(profileId).GetAwaiter().GetResult();

    public static async Task LoadProfileAsync(string profileId)
    {
        var resolvedId = ChromiumProfileCatalog.ResolveProfileId(profileId, Global.Profiles);
        Global.CurrentProfile = resolvedId;
        if (!Global.Profiles.Contains(resolvedId, StringComparer.OrdinalIgnoreCase))
            Global.Profiles.Add(resolvedId);

        if (Cef.IsInitialized == true && App.CefReady && App.RequestContexts != null)
        {
            var context = await App.RequestContexts.GetProfileContextReadyAsync(resolvedId).ConfigureAwait(false);
            Profile = await CefSettingsStore.ReadProfileAsync(context, Global, resolvedId).ConfigureAwait(false);
            await ExtensionService.Instance.RefreshFromCefAsync(context, resolvedId).ConfigureAwait(false);
        }
        else
        {
            Profile = new ProfileSettings();
        }

        var pendingName = CefSettingsStore.GetPendingProfileDisplayName(resolvedId);
        if (!string.IsNullOrWhiteSpace(pendingName)) Profile.DisplayName = pendingName;
        NormalizeProfileSettings();
    }

    public static void SaveAll()
    {
        SaveGlobal();
        SaveProfile();
    }

    public static void SaveGlobal()
    {
        NormalizeGlobalSettings();
        if (Cef.IsInitialized != true || !App.CefReady) return;

        CefPreferenceWriteQueue.Enqueue(
            "Chromium global/profile preferences",
            () => CefSettingsStore.WriteGlobalAsync(Global));
    }

    public static void ApplyRuntimeGlobalPreferences() => SaveGlobal();

    public static void SaveProfile()
    {
        NormalizeProfileSettings();
        if (Cef.IsInitialized != true || !App.CefReady || App.RequestContexts == null) return;

        var profileId = UserDataPaths.NormalizeProfileId(Global.CurrentProfile);
        CefSettingsStore.RememberProfileDisplayName(profileId, Profile.DisplayName);

        try
        {
            var context = App.RequestContexts.GetProfileContext(profileId);
            var profileSnapshot = Profile;
            CefPreferenceWriteQueue.Enqueue(
                $"profile Chromium preferences '{profileId}'",
                () => CefSettingsStore.WriteProfileAsync(context, profileId, profileSnapshot, Global));
        }
        catch (Exception ex)
        {
            AppLogger.Log("Settings", ex, $"Applying CEF profile preferences '{profileId}'.");
        }
    }

    /// <summary>Refreshes the active in-memory model from Chromium's live RequestContext.</summary>
    public static async Task RefreshCurrentProfileFromCefAsync()
    {
        if (Cef.IsInitialized != true || !App.CefReady || App.RequestContexts == null) return;
        try
        {
            var profileId = UserDataPaths.NormalizeProfileId(Global.CurrentProfile);
            var context = App.RequestContexts.GetProfileContext(profileId);
            await CefSettingsStore.RefreshProfileAsync(context, profileId, Profile, Global).ConfigureAwait(false);
            NormalizeProfileSettings();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Settings", ex, "Refreshing active profile settings from CEF.");
        }
    }

    /// <summary>
    /// Waits for already-queued CEF preference work while CEF and its RequestContexts are alive.
    /// </summary>
    public static bool DrainPendingCefWrites(TimeSpan timeout)
        => CefPreferenceWriteQueue.Drain(timeout);

    public static string NextProfileName()
    {
        var template = LanguageManager.Instance["Pref_ProfileCount"];
        var taken = new HashSet<string>(
            ChromiumProfileCatalog.GetProfiles(Global.Profiles).Select(p => p.DisplayName),
            StringComparer.OrdinalIgnoreCase);
        for (var n = 1; ; n++)
        {
            var candidate = string.Format(template, n);
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    public static string CurrentProfileDisplayName
        => !string.IsNullOrWhiteSpace(Profile.DisplayName)
            ? Profile.DisplayName.Trim()
            : ChromiumProfileCatalog.GetDisplayName(Global.CurrentProfile);

    private static void NormalizeGlobalSettings()
    {
        Global.DisplayLanguage = LanguageManager.NormalizeUiCode(Global.DisplayLanguage);
        Global.Profiles ??= new List<string>();
        Global.Profiles = Global.Profiles
            .Select(UserDataPaths.NormalizeProfileId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (Global.Profiles.Count == 0) Global.Profiles.Add(UserDataPaths.DefaultProfileId);
        Global.CurrentProfile = UserDataPaths.NormalizeProfileId(Global.CurrentProfile);
        if (!Global.Profiles.Contains(Global.CurrentProfile, StringComparer.OrdinalIgnoreCase))
            Global.Profiles.Insert(0, Global.CurrentProfile);
    }

    private static void NormalizeProfileSettings()
    {
        Profile.DisplayName = Profile.DisplayName?.Trim() ?? string.Empty;
        Profile.Theme = ThemeManager.NormalizeThemeKey(Profile.Theme);
        Profile.SearchEngine = string.IsNullOrWhiteSpace(Profile.SearchEngine) ? SearchEngines.Default : Profile.SearchEngine.Trim();
        Profile.SearchUrlTemplate ??= string.Empty;
        Profile.HomePageUrl = NormalizeHomeUrl(Profile.HomePageUrl);
        Profile.StartupBehavior = Math.Clamp(Profile.StartupBehavior, 0, 2);
        Profile.StartupPages ??= new List<string>();
        Profile.SitePermissions ??= new SitePermissions();
    }

    private static string NormalizeHomeUrl(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return "chrome://newtab/";
        if (string.Equals(text.TrimEnd('/'), "chrome://newtab", StringComparison.OrdinalIgnoreCase))
            return "chrome://newtab/";
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri.AbsoluteUri;
        return "chrome://newtab/";
    }


}
