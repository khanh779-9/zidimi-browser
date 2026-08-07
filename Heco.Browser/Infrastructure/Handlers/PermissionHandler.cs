using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using CefSharp;
using CefSharp.Handler;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Handles permission requests from the page. First the profile's default content
/// permission policy (SitePermissions) for each requested capability is checked; only
/// when the policy is "Ask" (the default) is a user Allow/Deny prompt shown, using the
/// app's own UI (HecoMessageBox) rather than CEF's hidden default prompt.
/// </summary>
public sealed class HecoPermissionHandler : CefSharp.Handler.PermissionHandler
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
        var result = HecoMessageBoxResult.No;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            result = HecoMessageBox.Show(
                question,
                title,
                HecoMessageBoxButton.YesNo,
                HecoMessageBoxImage.Question,
                Application.Current.MainWindow);
        });
        return result == HecoMessageBoxResult.Yes;
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
}