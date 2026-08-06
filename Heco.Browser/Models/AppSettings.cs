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
    public System.Collections.Generic.List<string> StartupPages { get; set; } = new();
    public System.Collections.Generic.List<string> LastSessionTabs { get; set; } = new();
    public bool SearchSuggestEnabled { get; set; } = true;

    // --- Hồ sơ (Profiles) ---
    public System.Collections.Generic.List<string> Profiles { get; set; } = new System.Collections.Generic.List<string> { "Cá nhân" };
    public string CurrentProfile { get; set; } = "Cá nhân";

    // --- Ngôn ngữ ---
    public string DisplayLanguage { get; set; } = "vi-VN";
    public bool AutoTranslate { get; set; } = true;

    // --- Cài đặt Hệ thống ---
    public bool EnableGpu { get; set; } = true;
    public bool EnhanceVideos { get; set; } = true;
    public bool RunInBackground { get; set; } = false;
    public bool UseSystemProxy { get; set; } = true;

    // --- Cài đặt Giao diện ---
    public string Theme { get; set; } = "system"; // system / dark / light (key ổn định, không theo ngôn ngữ) 
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
    /// <summary>Chuẩn hoá DisplayLanguage: chấp nhận mã ("vi-VN") hoặc tên cũ ("Tiếng Việt").</summary>
    private static string NormalizeLanguageCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "vi-VN";
        var v = value.Trim();
        if (v.Length <= 10 && v.Contains("-")) return v; // đã là mã như "vi-VN", "zh-CN"
        var map = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Tiếng Việt"] = "vi-VN",
            ["Vietnamese"] = "vi-VN",
            ["English"] = "en-US",
            ["Tiếng Anh"] = "en-US",
            ["Chinese"] = "zh-CN",
            ["Tiếng Trung"] = "zh-CN",
            ["German"] = "de-DE",
            ["Tiếng Đức"] = "de-DE",
            ["French"] = "fr-FR",
            ["Tiếng Pháp"] = "fr-FR",
            ["Italian"] = "it-IT",
            ["Tiếng Ý"] = "it-IT",
            ["Russian"] = "ru-RU",
            ["Tiếng Nga"] = "ru-RU",
        };
        return map.TryGetValue(v, out var code) ? code : "vi-VN";
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }

            // Migrate legacy: DisplayLanguage trước đây lưu tên ngôn ngữ ("Tiếng Việt")
            // → chuẩn hoá về mã ngôn ngữ ("vi-VN"). Làm trước vì LanguageManager đọc giá trị này.
            Current.DisplayLanguage = NormalizeLanguageCode(Current.DisplayLanguage);

            // Migrate legacy: Theme trước đây lưu label theo ngôn ngữ ("Hệ thống", "Tối"...)
            // → chuẩn hoá về key ổn định để không vỡ khi đổi ngôn ngữ.
            Current.Theme = Infrastructure.ThemeManager.NormalizeThemeKey(Current.Theme);
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
