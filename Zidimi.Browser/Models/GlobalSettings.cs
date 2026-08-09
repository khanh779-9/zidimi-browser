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
    public string? LoggedInUser { get; set; }
}
