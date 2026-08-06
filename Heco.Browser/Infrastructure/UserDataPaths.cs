using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Đường dẫn dữ liệu người dùng theo mô hình giống CocCoc/Chromium:
///   %LOCALAPPDATA%\HecoBrowser\User Data\
///       Local State        (JSON: metadata, profile info_cache)
///       Default\           (profile mặc định)
///           History.json, Bookmarks.json, Login Data.json, Cache\
///       &lt;ProfileName&gt;\  (các profile khác — tên được làm sạch ký tự không hợp lệ)
///           History.json, Bookmarks.json, Login Data.json, Cache\
/// Dữ liệu lịch sử / bookmark / autofill được lưu theo từng profile riêng biệt.
/// </summary>
public static class UserDataPaths
{
    public const string DefaultProfileName = "Cá nhân";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HecoBrowser", "User Data");

    public static string LocalStatePath => Path.Combine(Root, "Local State");

    /// <summary>Tên thư mục trên đĩa cho một profile (Default cho profile mặc định).</summary>
    public static string ProfileFolder(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == DefaultProfileName)
            return "Default";
        return CleanProfileName(profileName);
    }

    public static string ProfileDir(string profileName) => Path.Combine(Root, ProfileFolder(profileName));

    public static string HistoryFile(string profileName) => Path.Combine(ProfileDir(profileName), "History.json");

    public static string BookmarksFile(string profileName) => Path.Combine(ProfileDir(profileName), "Bookmarks.json");

    /// <summary>File autofill (mật khẩu / thẻ / địa chỉ) — đặt tên giống Chromium "Login Data".</summary>
    public static string AutofillFile(string profileName) => Path.Combine(ProfileDir(profileName), "Login Data.json");

    public static string CacheDir(string profileName) => Path.Combine(ProfileDir(profileName), "Cache");

    public static string PreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Preferences.json");

    /// <summary>Làm sạch tên profile để dùng làm tên thư mục.</summary>
    private static string CleanProfileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var clean = sb.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(clean) ? "Profile" : clean;
    }

    public static void EnsureProfileDir(string profileName)
    {
        var dir = ProfileDir(profileName);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(CacheDir(profileName));
    }

    /// <summary>
    /// Ghi/đọc "Local State" (JSON giống CocCoc). Dùng JsonNode để giữ cấu trúc mở rộng được.
    /// </summary>
    public static void UpdateLocalState(Action<System.Text.Json.Nodes.JsonObject> mutate)
    {
        try
        {
            Directory.CreateDirectory(Root);
            var path = LocalStatePath;
            System.Text.Json.Nodes.JsonObject? root;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                root = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json) as System.Text.Json.Nodes.JsonObject
                       ?? new System.Text.Json.Nodes.JsonObject();
            }
            else
            {
                root = new System.Text.Json.Nodes.JsonObject();
            }

            mutate(root);
            File.WriteAllText(path, JsonSerializer.Serialize(root,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* không nghiêm trọng — chỉ là metadata */ }
    }

    /// <summary>Đăng ký một profile vào info_cache của Local State (như CocCoc).</summary>
    public static void RegisterProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;
        var folder = ProfileFolder(profileName);
        UpdateLocalState(root =>
        {
            var profile = (System.Text.Json.Nodes.JsonObject?)root["profile"]
                          ?? (System.Text.Json.Nodes.JsonObject)(root["profile"] = new System.Text.Json.Nodes.JsonObject());
            var infoCache = (System.Text.Json.Nodes.JsonObject?)profile["info_cache"]
                            ?? (System.Text.Json.Nodes.JsonObject)(profile["info_cache"] = new System.Text.Json.Nodes.JsonObject());
            if (!infoCache.ContainsKey(folder))
            {
                infoCache[folder] = new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = profileName,
                    ["user_name"] = profileName,
                };
            }
        });
    }

    /// <summary>Di chuyển dữ liệu từ bố cục cũ sang User Data (chạy 1 lần).</summary>
    public static void MigrateLegacyData()
    {
        try
        {
            var oldRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HecoBrowser");

            // Dữ liệu cũ chỉ có cho profile mặc định.
            var defaultDir = ProfileDir(DefaultProfileName);
            EnsureProfileDir(DefaultProfileName);

            MigrateFile(Path.Combine(oldRoot, "bookmarks.json"), BookmarksFile(DefaultProfileName));
            MigrateFile(Path.Combine(oldRoot, "autofill.json"), AutofillFile(DefaultProfileName));

            // Cache CEF cũ -> User Data\Default\Cache
            var oldCache = Path.Combine(oldRoot, "Cache");
            var newCache = CacheDir(DefaultProfileName);
            if (Directory.Exists(oldCache) && !Directory.Exists(newCache))
            {
                try { Directory.Move(oldCache, newCache); }
                catch { }
            }

            RegisterProfile(DefaultProfileName);
        }
        catch { }
    }

    private static void MigrateFile(string source, string target)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target);
            }
        }
        catch { }
    }
}
