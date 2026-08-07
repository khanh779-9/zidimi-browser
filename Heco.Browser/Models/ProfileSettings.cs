using System;
using System.Collections.Generic;
using System.IO;

namespace Heco.Browser.Models;

public class ProfileSettings
{
    public string HomePageUrl { get; set; } = "https://duckduckgo.com";
    public string SearchEngine { get; set; } = "DuckDuckGo"; 
    public int StartupBehavior { get; set; } = 0; // 0: New page, 1: Continue, 2: Specific set of pages
    public List<string> StartupPages { get; set; } = new();
    public List<string> LastSessionTabs { get; set; } = new();
    public bool SearchSuggestEnabled { get; set; } = true;

    public string Theme { get; set; } = "system"; // system / dark / light
    public double FontSize { get; set; } = 14;
    public double ZoomLevel { get; set; } = 0.0; // 0 = 100%

    public bool BlockThirdPartyCookies { get; set; } = true;
    public bool SendDoNotTrack { get; set; } = true;
    public bool SafeBrowsing { get; set; } = true;
    public bool WarnDangerousSites { get; set; } = true;

    public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public bool AskBeforeSave { get; set; } = true;
    public bool ShowDownloadBar { get; set; } = true;

    public SitePermissions SitePermissions { get; set; } = new();
}
