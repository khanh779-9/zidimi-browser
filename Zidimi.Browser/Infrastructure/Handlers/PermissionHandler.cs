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
        var allowed = AskUser(requestingOrigin, question, Localize("Perm_MediaTitle"));

        PersistException(chromiumWebBrowser, requestingOrigin, GetCefPrefKey(requestedPermissions), allowed);

        if (!allowed)
        {
            using (callback) callback.Cancel();
            return true;
        }

        using (callback) callback.Continue(requestedPermissions);
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

        Action<PermissionRequestType, ContentPermission> check = (flag, p) =>
        {
            if ((requestedPermissions & flag) == 0) return;
            if (p == ContentPermission.Block) block = true;
            else if (p == ContentPermission.Allow) allow = true;
            else unresolved = true;
        };

        check(PermissionRequestType.Geolocation, policy.Geolocation);
        check(PermissionRequestType.Notifications, policy.Notifications);
        check(PermissionRequestType.CameraStream, policy.Camera);
        check(PermissionRequestType.MicStream, policy.Microphone);
        check(PermissionRequestType.Clipboard, policy.Clipboard);
        check(PermissionRequestType.PointerLock, policy.PointerLock);
        check(PermissionRequestType.MidiSysex, policy.MidiSysex);
        check(PermissionRequestType.FileSystemAccess, policy.FileSystemAccess);
        check(PermissionRequestType.IdleDetection, policy.IdleDetection);
        check(PermissionRequestType.LocalFonts, policy.LocalFonts);
        check(PermissionRequestType.MultipleDownloads, policy.MultipleDownloads);
        check(PermissionRequestType.WindowManagement, policy.WindowManagement);
        check(PermissionRequestType.KeyboardLock, policy.KeyboardLock);
        check(PermissionRequestType.ProtectedMediaIdentifier, policy.ProtectedMedia);
        check(PermissionRequestType.HandTracking, policy.HandTracking);
        check(PermissionRequestType.CameraPanTiltZoom, policy.CameraPanTiltZoom);
        check(PermissionRequestType.CapturedSurfaceControl, policy.CapturedSurfaceControl);
        check(PermissionRequestType.StorageAccess, policy.StorageAccess);
        check(PermissionRequestType.TopLevelStorageAccess, policy.TopLevelStorageAccess);
        check(PermissionRequestType.DiskQuota, policy.DiskQuota);
        check(PermissionRequestType.VrSession, policy.VrSession);
        check(PermissionRequestType.ArSession, policy.ArSession);
        check(PermissionRequestType.RegisterProtocolHandler, policy.RegisterProtocolHandler);
        check(PermissionRequestType.WebAppInstallation, policy.WebAppInstallation);
        check(PermissionRequestType.IdentityProvider, policy.IdentityProvider);
        check(PermissionRequestType.LocalNetworkAccess, policy.LocalNetworkAccess);
        check(PermissionRequestType.LocalNetwork, policy.LocalNetwork);
        check(PermissionRequestType.LoopbackNetwork, policy.LoopbackNetwork);

        // If any requested capability is set to Block, deny the whole request.
        if (block)
        {
            using (callback) callback.Continue(PermissionRequestResult.Deny);
            return true;
        }

        // If every requested capability is explicitly Allow, grant without prompting.
        // If any requested capability is still Ask (unresolved), fall through to the prompt.
        if (allow && !unresolved)
        {
            using (callback) callback.Continue(PermissionRequestResult.Accept);
            return true;
        }

        var names = DescribeRequestedTypes(requestedPermissions);
        var question = Localize("Perm_Dialog_Prompt", requestingOrigin, names);
        var allowed = AskUser(requestingOrigin, question, Localize("Perm_GenericTitle"));

        PersistException(chromiumWebBrowser, requestingOrigin, GetCefPrefKey(requestedPermissions), allowed);

        if (!allowed)
        {
            using (callback) callback.Continue(PermissionRequestResult.Deny);
            return true;
        }

        using (callback) callback.Continue(PermissionRequestResult.Accept);
        return true;
    }

    protected override void OnDismissPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser,
        ulong promptId, PermissionRequestResult result)
    {
        // The UI has closed — nothing more to handle.
    }

    private static bool AskUser(string origin, string question, string title)
    {
        var result = ZidimiMessageBoxResult.No;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            result = ZidimiMessageBox.Show(
                question,
                title,
                ZidimiMessageBoxButton.YesNo,
                ZidimiMessageBoxImage.Question,
                Application.Current.MainWindow);
        });
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