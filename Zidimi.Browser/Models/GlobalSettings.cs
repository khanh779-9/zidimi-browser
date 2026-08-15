using System.Collections.Generic;

namespace Zidimi.Browser.Models;

public class GlobalSettings
{
    public string CurrentProfile { get; set; } = "Cá nhân";
    public List<string> Profiles { get; set; } = new List<string> { "Cá nhân" };
    public string DisplayLanguage { get; set; } = "vi-VN";
    public bool EnableGpu { get; set; } = true;
    public bool EnhanceVideos { get; set; } = true;
    public bool RunInBackground { get; set; } = false;
    public bool UseSystemProxy { get; set; } = true;

    // ---- Advanced CEF configuration (consumed by CefConfigurator) ----
    /// <summary>Apply conservative stability mitigations (GPU crash-limit, occluded-window handling, ...).</summary>
    public bool StableRendering { get; set; } = true;
    /// <summary>Keep background tabs running at full speed instead of Chromium throttling.</summary>
    public bool DisableBackgroundThrottling { get; set; } = false;
    /// <summary>Run CEF without the sandbox (e.g. on restricted/VPN setups). Security trade-off.</summary>
    public bool DisableSandbox { get; set; } = false;
    /// <summary>Write a detailed CEF log (cef-debug.log next to the user data).</summary>
    public bool CefLogEnabled { get; set; } = false;
    /// <summary>Chrome DevTools port (0 = off). Requires a restart to take effect.</summary>
    public int RemoteDebuggingPort { get; set; } = 0;
    /// <summary>Maximum number of renderer processes (0 = auto by Chromium).</summary>
    public int RendererProcessLimit { get; set; } = 0;
    /// <summary>V8 heap size in MB per renderer (0 = Chromium default). Helps heavy sites like the Web Store.</summary>
    public int MaxJsHeapSizeMb { get; set; } = 0;
    /// <summary>Custom User-Agent string. Empty = CEF default (modern Chrome UA).</summary>
    public string? UserAgentOverride { get; set; }

    public string? LoggedInUser { get; set; }
}
