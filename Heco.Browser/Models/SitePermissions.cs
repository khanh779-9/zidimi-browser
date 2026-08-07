using System;

namespace Heco.Browser.Models;

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
/// Per-capability default permission policies tied to a profile. Stored in the
/// app's own settings; Chromium's Preferences file stays untouched.
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

    /// <summary>When set, pop-ups (window.open / target=_blank) are blocked entirely.</summary>
    public bool BlockPopups { get; set; } = false;
}