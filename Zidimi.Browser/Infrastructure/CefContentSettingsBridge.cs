using CefSharp;
using CefSharp.Enums;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// CEF-first bridge for the subset of Zidimi's site-permission defaults that have a stable public
/// <see cref="ContentSettingTypes"/> equivalent. Chromium remains the canonical persistence layer
/// for these settings; <see cref="SitePermissions"/> is retained as the browser-shell fallback for
/// permission types that CEF does not currently expose as a content setting.
///
/// Do not expand this map just because a similarly-named Chromium preference happens to exist.
/// Only add entries backed by a public CEF ContentSettingTypes value so a Chromium upgrade cannot
/// silently turn an app setting into an unsafe/undefined preference write.
/// </summary>
public static class CefContentSettingsBridge
{
    private static readonly IReadOnlyDictionary<string, ContentSettingTypes> SupportedDefaults =
        new Dictionary<string, ContentSettingTypes>(StringComparer.Ordinal)
        {
            [nameof(SitePermissions.Camera)] = ContentSettingTypes.MediaStreamCamera,
            [nameof(SitePermissions.Microphone)] = ContentSettingTypes.MediaStreamMic,
            [nameof(SitePermissions.Geolocation)] = ContentSettingTypes.Geolocation,
            [nameof(SitePermissions.Notifications)] = ContentSettingTypes.Notifications,
            [nameof(SitePermissions.Clipboard)] = ContentSettingTypes.ClipboardReadWrite,
            [nameof(SitePermissions.MidiSysex)] = ContentSettingTypes.MidiSysex,
            [nameof(SitePermissions.MultipleDownloads)] = ContentSettingTypes.AutomaticDownloads,
            [nameof(SitePermissions.ProtectedMedia)] = ContentSettingTypes.ProtectedMediaIdentifier,
        };

    public static bool TryGetContentType(string propertyName, out ContentSettingTypes type)
        => SupportedDefaults.TryGetValue(propertyName, out type);

    /// <summary>
    /// Refreshes the in-memory shell model from Chromium's live content-setting store. This keeps
    /// the Settings UI synchronized when an internal page/extension changed a permission after
    /// Zidimi initially loaded the profile through CEF.
    /// </summary>
    public static async Task RefreshSupportedDefaultsAsync(IRequestContext? context, SitePermissions settings)
    {
        if (context is null || context.IsDisposed || Cef.IsInitialized != true) return;

        foreach (var (propertyName, contentType) in SupportedDefaults)
        {
            try
            {
                var value = await CefProfileDataHelper.GetContentSettingAsync(
                    context, string.Empty, string.Empty, contentType).ConfigureAwait(false);
                var property = typeof(SitePermissions).GetProperty(propertyName);
                if (property == null) continue;
                property.SetValue(settings, value switch
                {
                    ContentSettingValues.Allow => ContentPermission.Allow,
                    ContentSettingValues.Block => ContentPermission.Block,
                    _ => ContentPermission.Ask,
                });
            }
            catch (Exception ex)
            {
                AppLogger.Log("ContentSettings", ex, $"Reading default {contentType}.");
            }
        }

        try
        {
            var popups = await CefProfileDataHelper.GetContentSettingAsync(
                context, string.Empty, string.Empty, ContentSettingTypes.Popups).ConfigureAwait(false);
            settings.BlockPopups = popups == ContentSettingValues.Block;
        }
        catch (Exception ex)
        {
            AppLogger.Log("ContentSettings", ex, "Reading default popup setting.");
        }
    }

    public static async Task ApplySupportedDefaultsAsync(IRequestContext? context, SitePermissions settings)
    {
        if (context is null || context.IsDisposed || Cef.IsInitialized != true) return;

        foreach (var (propertyName, contentType) in SupportedDefaults)
        {
            var property = typeof(SitePermissions).GetProperty(propertyName);
            if (property?.GetValue(settings) is not ContentPermission permission) continue;
            await SetDefaultAsync(context, contentType, permission).ConfigureAwait(false);
        }

        // Pop-ups have a public CEF content setting too. Zidimi still owns routing (new tab vs
        // blocked popup), while Chromium receives the same profile default for consistency with
        // internal pages/extensions that consult content settings directly.
        await SetPopupBlockingAsync(context, settings.BlockPopups).ConfigureAwait(false);
    }

    public static async Task SetDefaultAsync(
        IRequestContext context,
        ContentSettingTypes type,
        ContentPermission permission)
    {
        try
        {
            await CefProfileDataHelper.SetContentSettingAsync(
                context,
                string.Empty,
                string.Empty,
                type,
                permission switch
                {
                    ContentPermission.Allow => ContentSettingValues.Allow,
                    ContentPermission.Block => ContentSettingValues.Block,
                    _ => ContentSettingValues.Default,
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ContentSettings", ex, $"Setting default {type}={permission}.");
        }
    }

    public static async Task SetPopupBlockingAsync(IRequestContext context, bool block)
    {
        try
        {
            await CefProfileDataHelper.SetContentSettingAsync(
                context,
                string.Empty,
                string.Empty,
                ContentSettingTypes.Popups,
                block ? ContentSettingValues.Block : ContentSettingValues.Default)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ContentSettings", ex, $"Setting default popup blocking={block}.");
        }
    }
}
