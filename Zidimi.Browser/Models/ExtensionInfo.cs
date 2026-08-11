namespace Zidimi.Browser.Models;

public class ExtensionInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int ManifestVersion { get; set; } = 3;
}
