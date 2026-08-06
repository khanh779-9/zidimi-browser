using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heco.Browser.Models;

public static class AppSettings
{
    public static GlobalSettings Global { get; private set; } = new GlobalSettings();
    public static ProfileSettings Profile { get; private set; } = new ProfileSettings();

    private static readonly string GlobalSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "HecoBrowser", 
        "settings.json");

    private static string ProfileSettingsFilePath(string profileName) => 
        Infrastructure.UserDataPaths.PreferencesFile(profileName);

    private static string NormalizeLanguageCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "vi-VN";
        var v = value.Trim();
        if (v.Length <= 10 && v.Contains("-")) return v;
        var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            if (File.Exists(GlobalSettingsFilePath))
            {
                var json = File.ReadAllText(GlobalSettingsFilePath);
                Global = JsonSerializer.Deserialize<GlobalSettings>(json) ?? new GlobalSettings();

                // MIGRATION: If old monolithic settings.json, move profile data
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("HomePageUrl", out _))
                {
                    var legacyProfileSettings = JsonSerializer.Deserialize<ProfileSettings>(json) ?? new ProfileSettings();
                    legacyProfileSettings.Theme = Infrastructure.ThemeManager.NormalizeThemeKey(legacyProfileSettings.Theme);
                    
                    var profilePath = ProfileSettingsFilePath(Global.CurrentProfile);
                    Infrastructure.UserDataPaths.EnsureProfileDir(Global.CurrentProfile);
                    File.WriteAllText(profilePath, JsonSerializer.Serialize(legacyProfileSettings, new JsonSerializerOptions { WriteIndented = true }));
                    
                    // Save global to remove profile keys
                    SaveGlobal();
                }
            }

            Global.DisplayLanguage = NormalizeLanguageCode(Global.DisplayLanguage);
        }
        catch
        {
            Global = new GlobalSettings();
        }

        LoadProfile(Global.CurrentProfile);
    }

    public static void LoadProfile(string profileName)
    {
        Global.CurrentProfile = profileName;
        try
        {
            var path = ProfileSettingsFilePath(profileName);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                Profile = JsonSerializer.Deserialize<ProfileSettings>(json) ?? new ProfileSettings();
                Profile.Theme = Infrastructure.ThemeManager.NormalizeThemeKey(Profile.Theme);
            }
            else
            {
                Profile = new ProfileSettings();
            }
        }
        catch
        {
            Profile = new ProfileSettings();
        }
    }

    public static void SaveAll()
    {
        SaveGlobal();
        SaveProfile();
    }

    public static void SaveGlobal()
    {
        try
        {
            var dir = Path.GetDirectoryName(GlobalSettingsFilePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(Global, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GlobalSettingsFilePath, json);
        }
        catch { }
    }

    public static void SaveProfile()
    {
        try
        {
            var path = ProfileSettingsFilePath(Global.CurrentProfile);
            Infrastructure.UserDataPaths.EnsureProfileDir(Global.CurrentProfile);
            var json = JsonSerializer.Serialize(Profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }
}
