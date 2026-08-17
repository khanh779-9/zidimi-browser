using CefSharp;
using CefSharp.Enums;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Helper to copy CEF profile data (Preferences, Cookies) between RequestContexts
/// using only CefSharp APIs. CEF handles all persistence automatically —
/// no external libraries or manual file I/O needed.
/// </summary>
public static class CefProfileDataHelper
{
    private static readonly string[] PortablePreferenceKeys =
    {
        ChromiumPreferenceKeys.Homepage,
        ChromiumPreferenceKeys.HomepageIsNewTabPage,
        ChromiumPreferenceKeys.SessionRestoreOnStartup,
        ChromiumPreferenceKeys.SessionStartupUrls,
        ChromiumPreferenceKeys.SearchSuggestEnabled,
        ChromiumPreferenceKeys.AcceptLanguages,
        ChromiumPreferenceKeys.SelectedLanguages,
        ChromiumPreferenceKeys.BrowserColorScheme,
        ChromiumPreferenceKeys.DefaultFontSize,
        ChromiumPreferenceKeys.DefaultFixedFontSize,
        ChromiumPreferenceKeys.DefaultZoomLevel,
        ChromiumPreferenceKeys.CookieControlsMode,
        ChromiumPreferenceKeys.EnableDoNotTrack,
        ChromiumPreferenceKeys.SafeBrowsingEnabled,
        ChromiumPreferenceKeys.DownloadDefaultDirectory,
        ChromiumPreferenceKeys.DownloadPromptForDownload,
        ChromiumPreferenceKeys.Proxy,
    };

    /// <summary>
    /// Copies only an allow-list of portable Chromium preferences through CEF GetPreference/
    /// SetPreference. Never bulk-serializes GetAllPreferences: protected/deep values such as
    /// extensions.settings, profile identity and Chromium-internal dictionaries are intentionally
    /// excluded so copying a profile cannot corrupt extension registration or Secure Preferences.
    /// </summary>
    public static async Task CopyPreferencesAsync(IRequestContext source, IRequestContext target)
    {
        foreach (var key in PortablePreferenceKeys)
        {
            var value = await source.GetPreferenceSafeAsync(key).ConfigureAwait(false);
            if (value == null) continue;
            await target.SetPreferenceSafeAsync(key, value).ConfigureAwait(false);
        }
    }

    /// <summary>Copies browsing cookies exclusively through Chromium's CEF CookieManager.</summary>
    public static async Task CopyCookiesAsync(IRequestContext source, IRequestContext target)
    {
        var srcManager = await source.GetCookieManagerAsync().ConfigureAwait(false);
        var dstManager = await target.GetCookieManagerAsync().ConfigureAwait(false);
        if (srcManager == null || srcManager.IsDisposed || dstManager == null || dstManager.IsDisposed) return;

        var cookies = await srcManager.VisitAllCookiesAsync().ConfigureAwait(false);
        if (cookies == null) return;

        foreach (var cookie in cookies)
        {
            if (cookie == null || string.IsNullOrWhiteSpace(cookie.Domain)) continue;
            var domain = cookie.Domain.TrimStart('.');
            var url = (cookie.Secure ? "https://" : "http://") + domain +
                      (string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path);
            await dstManager.SetCookieAsync(url, cookie).ConfigureAwait(false);
        }

        await dstManager.FlushStoreAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Copies profile data exposed safely through public CefSharp APIs: allow-listed preferences
    /// and cookies. Chromium extension/internal state is never copied or rewritten.
    /// </summary>
    public static async Task CopyAllAsync(IRequestContext source, IRequestContext target)
    {
        await CopyPreferencesAsync(source, target).ConfigureAwait(false);
        await CopyCookiesAsync(source, target).ConfigureAwait(false);
    }

    public static Task<ContentSettingValues> GetContentSettingAsync(
        IRequestContext ctx, string requestingUrl, string topLevelUrl, ContentSettingTypes contentType)
        => Cef.UIThreadTaskFactory.StartNew(
            () => ctx.GetContentSetting(requestingUrl, topLevelUrl, contentType));

    public static Task SetContentSettingAsync(
        IRequestContext ctx, string requestingUrl, string topLevelUrl,
        ContentSettingTypes contentType, ContentSettingValues value)
        => Cef.UIThreadTaskFactory.StartNew(
            () => ctx.SetContentSetting(requestingUrl, topLevelUrl, contentType, value));
}

public static class CefPreferenceExtensions
{
    /// <summary>
    /// Async preference read for hot/UI paths. Prefer this over the synchronous compatibility
    /// wrapper when the caller can await; CEF requires preference access on its UI thread.
    /// </summary>
    public static async Task<object?> GetPreferenceSafeAsync(this IRequestContext? ctx, string name)
    {
        if (ctx == null || Cef.IsInitialized != true) return null;
        try
        {
            if (Cef.CurrentlyOnThread(CefThreadIds.TID_UI))
                return ctx.GetPreference(name);
            return await Cef.UIThreadTaskFactory.StartNew(() => ctx.GetPreference(name)).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Async preference write through CefSharp's native RequestContext SetPreferenceAsync helper.
    /// Chromium validates the key/value on its UI thread and owns persistence in the disk-backed
    /// context; Zidimi never mirrors the result into Preferences/Secure Preferences itself.
    /// </summary>
    public static async Task<bool> SetPreferenceSafeAsync(this IRequestContext? ctx, string name, object? value)
    {
        if (ctx == null || ctx.IsDisposed || Cef.IsInitialized != true) return false;
        try
        {
            var response = await ctx.SetPreferenceAsync(name, value!).ConfigureAwait(false);
            if (!response.Success && !string.IsNullOrWhiteSpace(response.ErrorMessage))
                AppLogger.Log("Preferences", $"CEF rejected '{name}': {response.ErrorMessage}");
            return response.Success;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Preferences", ex, $"Setting Chromium preference '{name}'.");
            return false;
        }
    }

    /// <summary>
    /// Synchronous compatibility wrapper for small, infrequent Settings UI reads. New hot paths
    /// should use GetPreferenceSafeAsync so WPF is not blocked waiting for the CEF UI thread.
    /// </summary>
    public static object? GetPreferenceSafe(this IRequestContext? ctx, string name)
    {
        if (ctx == null || Cef.IsInitialized != true) return null;
        try
        {
            if (Cef.CurrentlyOnThread(CefThreadIds.TID_UI))
            {
                return ctx.GetPreference(name);
            }
            var targetCtx = ctx;
            return Cef.UIThreadTaskFactory.StartNew(() => targetCtx.GetPreference(name)).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

}
