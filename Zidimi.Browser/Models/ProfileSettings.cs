using System;
using System.Collections.Generic;
using System.IO;

namespace Zidimi.Browser.Models;

public class ProfileSettings
{
    public string HomePageUrl { get; set; } = "https://duckduckgo.com";
    public string SearchEngine { get; set; } = "DuckDuckGo"; 
    public int StartupBehavior { get; set; } = 0; // 0: New page, 1: Continue, 2: Specific set of pages
    public List<string> StartupPages { get; set; } = new();
    public List<string> LastSessionTabs { get; set; } = new();
    public bool SearchSuggestEnabled { get; set; } = true;

    public string Theme { get; set; } = "classic"; // classic / system / dark / light
    public bool ShowDownloadBar { get; set; } = true;

    public SitePermissions SitePermissions { get; set; } = new();

    public List<ExtensionInfo> Extensions { get; set; } = new();
}
