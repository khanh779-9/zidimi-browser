namespace Zidimi.Browser.Models;

public class ExtensionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    /// <summary>Real Chromium runtime id returned by Extensions.loadUnpacked.</summary>
    public string? RuntimeId { get; set; }
    /// <summary>Manifest action/browser_action popup path, relative to the extension root.</summary>
    public string? PopupPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPinned { get; set; }
    public int ManifestVersion { get; set; } = 3;
}
