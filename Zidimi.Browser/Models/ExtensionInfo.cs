using System.Text.Json.Serialization;

namespace Zidimi.Browser.Models;

/// <summary>
/// In-memory projection of Chromium's installed extension state. Chromium owns the package,
/// registration, enabled/pinned preferences and all extension storage; Zidimi only renders it.
/// </summary>
public class ExtensionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public string? StoreId { get; set; }
    public string? RuntimeId { get; set; }


    public string? PopupPath { get; set; }
    public string? SidePanelPath { get; set; }
    public bool HasToolbarAction { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPinned { get; set; }


    [JsonIgnore]
    public bool IsAvailable { get; set; } = true;
    public int ManifestVersion { get; set; } = 3;
}
