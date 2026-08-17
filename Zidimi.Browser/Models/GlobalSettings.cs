using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Models;

/// <summary>
/// Process-wide shell state. Persistent browser values map to registered Chromium preferences.
/// Local-State-only values are never duplicated into a Zidimi file/cookie; current profile and the
/// WPF picker/background choices therefore remain runtime/derived state when CefSharp has no API.
/// </summary>
public class GlobalSettings
{
    public string CurrentProfile { get; set; } = UserDataPaths.DefaultProfileId;
    public List<string> Profiles { get; set; } = new() { UserDataPaths.DefaultProfileId };
    public string DisplayLanguage { get; set; } = "vi-VN";
    public bool ShowProfilePickerOnStartup { get; set; }
    public bool UseSystemProxy { get; set; } = true;
}
