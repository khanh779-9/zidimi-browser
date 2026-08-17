using System;

namespace Zidimi.Browser.Models;

/// <summary>
/// Default policy applied when a site requests a capability through the browser
/// (maps roughly to Chromium's profile.content_settings).
/// </summary>
public enum ContentPermission
{
    /// <summary>Always ask the user (the default).</summary>
    Ask,
    /// <summary>Automatically allow without prompting.</summary>
    Allow,
    /// <summary>Automatically block without prompting.</summary>
    Block
}

/// <summary>
/// Browser-shell permission defaults tied to a profile.
///
/// CefContentSettingsBridge mirrors the subset with a stable public CEF content-setting API into
/// the profile IRequestContext (camera, microphone, location, notifications, etc.). The remaining
/// values live here as a deliberate fallback for PermissionRequestType capabilities that CEF does
/// not expose as a safe/stable ContentSettingTypes value. Chromium-owned Preferences files are
/// never edited directly.
/// </summary>
public class SitePermissions
{
    public ContentPermission Camera { get; set; } = ContentPermission.Ask;
    public ContentPermission Microphone { get; set; } = ContentPermission.Ask;
    public ContentPermission Geolocation { get; set; } = ContentPermission.Ask;
    public ContentPermission Notifications { get; set; } = ContentPermission.Ask;
    public ContentPermission Clipboard { get; set; } = ContentPermission.Ask;
    public ContentPermission PointerLock { get; set; } = ContentPermission.Ask;
    public ContentPermission MidiSysex { get; set; } = ContentPermission.Ask;
    public ContentPermission FileSystemAccess { get; set; } = ContentPermission.Ask;
    public ContentPermission IdleDetection { get; set; } = ContentPermission.Ask;
    public ContentPermission LocalFonts { get; set; } = ContentPermission.Ask;
    public ContentPermission MultipleDownloads { get; set; } = ContentPermission.Ask;
    public ContentPermission WindowManagement { get; set; } = ContentPermission.Ask;
    public ContentPermission KeyboardLock { get; set; } = ContentPermission.Ask;
    public ContentPermission ProtectedMedia { get; set; } = ContentPermission.Ask;
    public ContentPermission HandTracking { get; set; } = ContentPermission.Ask;
    public ContentPermission CameraPanTiltZoom { get; set; } = ContentPermission.Ask;
    public ContentPermission CapturedSurfaceControl { get; set; } = ContentPermission.Ask;
    public ContentPermission StorageAccess { get; set; } = ContentPermission.Ask;
    public ContentPermission TopLevelStorageAccess { get; set; } = ContentPermission.Ask;
    public ContentPermission DiskQuota { get; set; } = ContentPermission.Ask;
    public ContentPermission VrSession { get; set; } = ContentPermission.Ask;
    public ContentPermission ArSession { get; set; } = ContentPermission.Ask;
    public ContentPermission RegisterProtocolHandler { get; set; } = ContentPermission.Ask;
    public ContentPermission WebAppInstallation { get; set; } = ContentPermission.Ask;
    public ContentPermission IdentityProvider { get; set; } = ContentPermission.Ask;
    public ContentPermission LocalNetworkAccess { get; set; } = ContentPermission.Ask;
    public ContentPermission LocalNetwork { get; set; } = ContentPermission.Ask;
    public ContentPermission LoopbackNetwork { get; set; } = ContentPermission.Ask;

    /// <summary>When set, unrequested pop-ups are blocked; user-initiated new tabs stay in Zidimi.</summary>
    public bool BlockPopups { get; set; } = false;
}