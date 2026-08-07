using CefSharp;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Helper to copy CEF profile data (Preferences, Cookies) between RequestContexts
/// using only CefSharp APIs. CEF handles all persistence automatically —
/// no external libraries or manual file I/O needed.
/// </summary>
public static class CefProfileDataHelper
{
    /// <summary>
    /// Copies all modifiable Preferences from <paramref name="source"/> into <paramref name="target"/>
    /// using <c>GetAllPreferences</c> / <c>SetPreference</c>.
    /// CEF will persist the changes to the target profile's <c>Preferences</c> JSON file automatically.
    /// </summary>
    public static async Task CopyPreferencesAsync(IRequestContext source, IRequestContext target)
    {
        IDictionary<string, object>? prefs = null;

        // GetAllPreferences must run on the CEF UI thread.
        await Cef.UIThreadTaskFactory.StartNew(() =>
        {
            prefs = source.GetAllPreferences(includeDefaults: false);
        });

        if (prefs == null) return;

        // SetPreference can fail silently for read-only keys; we just skip errors.
        foreach (var kvp in prefs)
        {
            target.SetPreference(kvp.Key, kvp.Value, out _);
        }
    }

    /// <summary>
    /// Copies all Cookies from the <paramref name="source"/> context into <paramref name="target"/>.
    /// CEF will persist them to the target profile's <c>Network/Cookies</c> SQLite DB automatically.
    /// </summary>
    public static async Task CopyCookiesAsync(IRequestContext source, IRequestContext target)
    {
        var srcManager = source.GetCookieManager(null);
        var dstManager = target.GetCookieManager(null);
        if (srcManager == null || dstManager == null) return;

        var cookies = await srcManager.VisitAllCookiesAsync();
        if (cookies == null) return;

        foreach (var cookie in cookies)
        {
            string domain = cookie.Domain.TrimStart('.');
            string url = (cookie.Secure ? "https://" : "http://") + domain + cookie.Path;
            await dstManager.SetCookieAsync(url, cookie);
        }
    }

    /// <summary>
    /// Copies both Preferences and Cookies from one profile context to another.
    /// </summary>
    public static async Task CopyAllAsync(IRequestContext source, IRequestContext target)
    {
        await CopyPreferencesAsync(source, target);
        await CopyCookiesAsync(source, target);
    }

    /// <summary>
    /// Applies a set of individual preference key/value pairs to the given context.
    /// Convenience wrapper around <c>ctx.SetPreference</c>.
    /// </summary>
    public static void ApplyPreferences(IRequestContext ctx, IDictionary<string, object> preferences)
    {
        foreach (var kvp in preferences)
        {
            ctx.SetPreference(kvp.Key, kvp.Value, out _);
        }
    }

    /// <summary>
    /// Reads all non-default preferences from the given context.
    /// Must be called from the CEF UI thread or wrapped in <c>Cef.UIThreadTaskFactory</c>.
    /// </summary>
    public static IDictionary<string, object>? ReadPreferences(IRequestContext ctx)
    {
        return ctx.GetAllPreferences(includeDefaults: false);
    }
}
