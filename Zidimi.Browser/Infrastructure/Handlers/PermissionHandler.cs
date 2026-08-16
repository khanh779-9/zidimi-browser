using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using CefSharp;
using CefSharp.Handler;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Handles permission requests from the page. First the profile's default content
/// permission policy (SitePermissions) for each requested capability is checked; only
/// when the policy is "Ask" (the default) is a user Allow/Deny prompt shown, using the
/// app's own UI (ZidimiMessageBox) rather than CEF's hidden default prompt.
/// </summary>
public sealed class ZidimiPermissionHandler : CefSharp.Handler.PermissionHandler
{
    protected override bool OnRequestMediaAccessPermission(IWebBrowser chromiumWebBrowser, IBrowser browser,
        IFrame frame, string requestingOrigin, MediaAccessPermissionType requestedPermissions,
        IMediaAccessCallback callback)
    {
        var policy = AppSettings.Profile.SitePermissions;

        bool allow = false;
        bool block = false;
        bool unresolved = false;

        if ((requestedPermissions & MediaAccessPermissionType.AudioCapture) != 0)
        {
            if (policy.Microphone == ContentPermission.Allow) allow = true;
            else if (policy.Microphone == ContentPermission.Block) block = true;
            else unresolved = true;
        }
        if ((requestedPermissions & MediaAccessPermissionType.VideoCapture) != 0)
        {
            if (policy.Camera == ContentPermission.Allow) allow = true;
            else if (policy.Camera == ContentPermission.Block) block = true;
            else unresolved = true;
        }

        if (block)
        {
            using (callback) callback.Cancel();
            return true;
        }

        if (allow && !unresolved)
        {
            using (callback) callback.Continue(requestedPermissions);
            return true;
        }

        var name = DescribeMedia(requestedPermissions);
        var question = Localize("Perm_Dialog_Media", requestingOrigin, name);
        var title = Localize("Perm_MediaTitle");

        // Never wait synchronously on WPF from a CEF callback thread. The CEF callback
        // is designed to be completed later, so return immediately and show our UI async.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            using (callback)
            {
                try
                {
                    var allowed = ShowPermissionDialog(question, title);
                    PersistException(chromiumWebBrowser, requestingOrigin,
                        GetCefPrefKey(requestedPermissions), allowed);

                    if (allowed) callback.Continue(requestedPermissions);
                    else callback.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The request may have been dismissed by CEF while the WPF dialog
                    // was queued (for example because the frame navigated/closed).
                }
            }
        });

        return true;
    }

    protected override bool OnShowPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser,
        ulong promptId, string requestingOrigin, PermissionRequestType requestedPermissions,
        IPermissionPromptCallback callback)
    {
        var policy = AppSettings.Profile.SitePermissions;

        bool block = false;
        bool allow = false;
        bool unresolved = false;

        void Check(PermissionRequestType flag, ContentPermission permission)
        {
            if ((requestedPermissions & flag) == 0) return;
            if (permission == ContentPermission.Block) block = true;
            else if (permission == ContentPermission.Allow) allow = true;
            else unresolved = true;
        }

        Check(PermissionRequestType.Geolocation, policy.Geolocation);
        Check(PermissionRequestType.Notifications, policy.Notifications);
        Check(PermissionRequestType.CameraStream, policy.Camera);
        Check(PermissionRequestType.MicStream, policy.Microphone);
        Check(PermissionRequestType.Clipboard, policy.Clipboard);
        Check(PermissionRequestType.PointerLock, policy.PointerLock);
        Check(PermissionRequestType.MidiSysex, policy.MidiSysex);
        Check(PermissionRequestType.FileSystemAccess, policy.FileSystemAccess);
        Check(PermissionRequestType.IdleDetection, policy.IdleDetection);
        Check(PermissionRequestType.LocalFonts, policy.LocalFonts);
        Check(PermissionRequestType.MultipleDownloads, policy.MultipleDownloads);
        Check(PermissionRequestType.WindowManagement, policy.WindowManagement);
        Check(PermissionRequestType.KeyboardLock, policy.KeyboardLock);
        Check(PermissionRequestType.ProtectedMediaIdentifier, policy.ProtectedMedia);
        Check(PermissionRequestType.HandTracking, policy.HandTracking);
        Check(PermissionRequestType.CameraPanTiltZoom, policy.CameraPanTiltZoom);
        Check(PermissionRequestType.CapturedSurfaceControl, policy.CapturedSurfaceControl);
        Check(PermissionRequestType.StorageAccess, policy.StorageAccess);
        Check(PermissionRequestType.TopLevelStorageAccess, policy.TopLevelStorageAccess);
        Check(PermissionRequestType.DiskQuota, policy.DiskQuota);
        Check(PermissionRequestType.VrSession, policy.VrSession);
        Check(PermissionRequestType.ArSession, policy.ArSession);
        Check(PermissionRequestType.RegisterProtocolHandler, policy.RegisterProtocolHandler);
        Check(PermissionRequestType.WebAppInstallation, policy.WebAppInstallation);
        Check(PermissionRequestType.IdentityProvider, policy.IdentityProvider);
        Check(PermissionRequestType.LocalNetworkAccess, policy.LocalNetworkAccess);
        Check(PermissionRequestType.LocalNetwork, policy.LocalNetwork);
        Check(PermissionRequestType.LoopbackNetwork, policy.LoopbackNetwork);

        if (block)
        {
            using (callback) callback.Continue(PermissionRequestResult.Deny);
            return true;
        }

        if (allow && !unresolved)
        {
            using (callback) callback.Continue(PermissionRequestResult.Accept);
            return true;
        }

        var names = DescribeRequestedTypes(requestedPermissions);
        var question = Localize("Perm_Dialog_Prompt", requestingOrigin, names);
        var title = Localize("Perm_GenericTitle");

        AppLogger.Log("Permission",
            $"Prompt requested. Origin={requestingOrigin}, Types={requestedPermissions}, PromptId={promptId}");

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            using (callback)
            {
                try
                {
                    var allowed = ShowPermissionDialog(question, title);
                    PersistException(chromiumWebBrowser, requestingOrigin,
                        GetCefPrefKey(requestedPermissions), allowed);
                    callback.Continue(allowed ? PermissionRequestResult.Accept : PermissionRequestResult.Deny);
                }
                catch (ObjectDisposedException)
                {
                    // The prompt may have been dismissed by CEF while the WPF dialog
                    // was queued (for example because the frame navigated/closed).
                }
            }
        });

        return true;
    }

    protected override void OnDismissPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser,
        ulong promptId, PermissionRequestResult result)
    {
        // The UI has closed — nothing more to handle.
    }

    private static bool ShowPermissionDialog(string question, string title)
    {
        var result = ZidimiMessageBox.Show(
            question,
            title,
            ZidimiMessageBoxButton.YesNo,
            ZidimiMessageBoxImage.Question,
            Application.Current?.MainWindow);
        return result == ZidimiMessageBoxResult.Yes;
    }

    private static string DescribeMedia(MediaAccessPermissionType perms)
    {
        var parts = new List<string>();
        var l = LanguageManager.Instance;
        if ((perms & MediaAccessPermissionType.AudioCapture) != 0) parts.Add(l["Perm_Microphone"]);
        if ((perms & MediaAccessPermissionType.VideoCapture) != 0) parts.Add(l["Perm_Camera"]);
        if ((perms & MediaAccessPermissionType.DesktopAudioCapture) != 0) parts.Add(l["Perm_DesktopAudio"]);
        if ((perms & MediaAccessPermissionType.DesktopVideoCapture) != 0) parts.Add(l["Perm_DesktopVideo"]);
        return parts.Count == 0 ? l["Perm_Media"] : string.Join(", ", parts);
    }

    private static string DescribeRequestedTypes(PermissionRequestType types)
    {
        var l = LanguageManager.Instance;
        var labelMap = new Dictionary<PermissionRequestType, string>
        {
            [PermissionRequestType.Geolocation] = l["Perm_Location"],
            [PermissionRequestType.Notifications] = l["Perm_Notifications"],
            [PermissionRequestType.CameraStream] = l["Perm_Camera"],
            [PermissionRequestType.MicStream] = l["Perm_Microphone"],
            [PermissionRequestType.Clipboard] = l["Perm_Clipboard"],
            [PermissionRequestType.PointerLock] = l["Perm_PointerLock"],
            [PermissionRequestType.MidiSysex] = l["Perm_Midi"],
            [PermissionRequestType.ProtectedMediaIdentifier] = l["Perm_ProtectedMedia"],
            [PermissionRequestType.IdleDetection] = l["Perm_IdleDetection"],
            [PermissionRequestType.FileSystemAccess] = l["Perm_FileSystem"],
            [PermissionRequestType.LocalFonts] = l["Perm_LocalFonts"],
            [PermissionRequestType.MultipleDownloads] = l["Perm_MultipleDownloads"],
            [PermissionRequestType.WindowManagement] = l["Perm_WindowManagement"],
            [PermissionRequestType.KeyboardLock] = l["Perm_KeyboardLock"],
            [PermissionRequestType.HandTracking] = l["Perm_HandTracking"],
            [PermissionRequestType.CameraPanTiltZoom] = l["Perm_CameraPanTilt"],
            [PermissionRequestType.CapturedSurfaceControl] = l["Perm_CapturedSurface"],
            [PermissionRequestType.StorageAccess] = l["Perm_StorageAccess"],
            [PermissionRequestType.TopLevelStorageAccess] = l["Perm_TopLevelStorage"],
            [PermissionRequestType.DiskQuota] = l["Perm_DiskQuota"],
            [PermissionRequestType.VrSession] = l["Perm_Vr"],
            [PermissionRequestType.ArSession] = l["Perm_Ar"],
            [PermissionRequestType.RegisterProtocolHandler] = l["Perm_ProtocolHandler"],
            [PermissionRequestType.WebAppInstallation] = l["Perm_WebAppInstall"],
            [PermissionRequestType.IdentityProvider] = l["Perm_IdentityProvider"],
            [PermissionRequestType.LocalNetworkAccess] = l["Perm_LocalNetworkAccess"],
            [PermissionRequestType.LocalNetwork] = l["Perm_LocalNetwork"],
            [PermissionRequestType.LoopbackNetwork] = l["Perm_LoopbackNetwork"],
        };

        var selected = new List<string>();
        foreach (var (flag, label) in labelMap)
        {
            if ((types & flag) != 0) selected.Add(label);
        }
        return selected.Count == 0
            ? l["Perm_UnknownRequest"]
            : string.Join(", ", selected);
    }

    private static string Localize(string key, params string[] args)
        => string.Format(LanguageManager.Instance[key], args);

    private static string? GetCefPrefKey(MediaAccessPermissionType perms)
    {
        if ((perms & MediaAccessPermissionType.AudioCapture) != 0) return "media_stream_mic";
        if ((perms & MediaAccessPermissionType.VideoCapture) != 0) return "media_stream_camera";
        return null;
    }

    private static string? GetCefPrefKey(PermissionRequestType type)
    {
        // Maps a few common types, returns null if it shouldn't or can't be persisted this way.
        if ((type & PermissionRequestType.Geolocation) != 0) return "geolocation";
        if ((type & PermissionRequestType.Notifications) != 0) return "notifications";
        if ((type & PermissionRequestType.CameraStream) != 0) return "media_stream_camera";
        if ((type & PermissionRequestType.MicStream) != 0) return "media_stream_mic";
        if ((type & PermissionRequestType.Clipboard) != 0) return "clipboard";
        if ((type & PermissionRequestType.PointerLock) != 0) return "mouselock";
        if ((type & PermissionRequestType.MidiSysex) != 0) return "midi_sysex";
        if ((type & PermissionRequestType.MultipleDownloads) != 0) return "automatic_downloads";
        if ((type & PermissionRequestType.WindowManagement) != 0) return "window_placement";
        if ((type & PermissionRequestType.ProtectedMediaIdentifier) != 0) return "protected_media_identifier";
        if ((type & PermissionRequestType.IdleDetection) != 0) return "idle_detection";
        if ((type & PermissionRequestType.FileSystemAccess) != 0) return "file_system_write_guard";
        if ((type & PermissionRequestType.LocalFonts) != 0) return "local_fonts";
        if ((type & PermissionRequestType.ArSession) != 0) return "ar";
        if ((type & PermissionRequestType.VrSession) != 0) return "vr";
        return null;
    }

    private static void PersistException(IWebBrowser browser, string origin, string? prefKey, bool allow)
    {
        if (prefKey == null) return;
        try
        {
            var ctx = browser.GetBrowserHost().RequestContext;
            if (ctx == null) return;
            var fullKey = "profile.content_settings.exceptions." + prefKey;

            var exceptions = ctx.GetPreferenceSafe(fullKey);
            var dict = exceptions as IDictionary<string, object> ?? new System.Dynamic.ExpandoObject() as IDictionary<string, object>;

            var uri = new Uri(origin);
            string port = uri.Port > 0 ? uri.Port.ToString() : (uri.Scheme == "https" ? "443" : "80");
            string originPattern = $"{uri.Scheme}://{uri.Host}:{port},*";

            var settingNode = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;
            settingNode["setting"] = allow ? 1 : 2;
            settingNode["last_modified"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            dict[originPattern] = settingNode;
            ctx.SetPreferenceSafe(fullKey, dict);
        }
        catch { }
    }
}