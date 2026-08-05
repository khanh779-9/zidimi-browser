using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heco.Browser.Models;

public class AppSettings
{
    [JsonIgnore]
    public static AppSettings Current { get; private set; } = new AppSettings();

    [JsonIgnore]
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "HecoBrowser", 
        "settings.json");

    // --- Cài đặt Chung ---
    public string HomePageUrl { get; set; } = "https://duckduckgo.com";
    public string SearchEngine { get; set; } = "DuckDuckGo"; 
    public int StartupBehavior { get; set; } = 0; // 0: Trang mới, 1: Tiếp tục, 2: Tập trang cụ thể
    public bool SearchSuggestEnabled { get; set; } = true;

    // --- Hồ sơ (Profiles) ---
    public System.Collections.Generic.List<string> Profiles { get; set; } = new System.Collections.Generic.List<string> { "Cá nhân" };
    public string CurrentProfile { get; set; } = "Cá nhân";

    // --- Ngôn ngữ ---
    public string DisplayLanguage { get; set; } = "Tiếng Việt";
    public bool AutoTranslate { get; set; } = true;

    // --- Cài đặt Hệ thống ---
    public bool EnableGpu { get; set; } = true;
    public bool RunInBackground { get; set; } = false;
    public bool UseSystemProxy { get; set; } = true;

    // --- Cài đặt Giao diện ---
    public string Theme { get; set; } = "Hệ thống"; 
    public double FontSize { get; set; } = 14;
    public double ZoomLevel { get; set; } = 0.0; // 0 = 100%
    public string? LoggedInUser { get; set; }

    // --- Cài đặt Quyền riêng tư ---
    public bool BlockThirdPartyCookies { get; set; } = true;
    public bool SendDoNotTrack { get; set; } = true;
    public bool SafeBrowsing { get; set; } = true;
    public bool WarnDangerousSites { get; set; } = true;

    // --- Cài đặt Tải xuống ---
    public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public bool AskBeforeSave { get; set; } = true;
    public bool ShowDownloadBar { get; set; } = true;

    // --- Phương thức ---
    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
