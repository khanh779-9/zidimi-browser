using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Đường dẫn dữ liệu người dùng theo mô hình giống CocCoc/Chromium:
///   %LOCALAPPDATA%\HecoBrowser\Browser\User Data\
///       Local State        (JSON: metadata, profile info_cache)
///       Cache\             (cache dùng chung cho mọi profile — giống CocCoc)
///       Default\           (profile mặc định)
///           History.json, Bookmarks.json, Login Data.json, Preferences.json
///       &lt;ProfileName&gt;\  (các profile khác — tên được làm sạch ký tự không hợp lệ)
///           History.json, Bookmarks.json, Login Data.json, Preferences.json
/// Cache CEF được chia sẻ ở root; mỗi profile chỉ chứa dữ liệu JSON riêng của nó.
/// </summary>
public static class UserDataPaths
{
    public const string DefaultProfileName = "Cá nhân";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HecoBrowser", "Browser", "User Data");

    public static string LocalStatePath => Path.Combine(Root, "Local State");

    /// <summary>Cache dùng chung cho mọi profile, nằm ngay trong User Data root (giống CocCoc).</summary>
    public static string SharedCacheDir => Path.Combine(Root);

    /// <summary>Tên thư mục trên đĩa cho một profile (Default cho profile mặc định).</summary>
    public static string ProfileFolder(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == DefaultProfileName)
            return "Default";
        return CleanProfileName(profileName);
    }

    public static string ProfileDir(string profileName) => Path.Combine(Root, ProfileFolder(profileName));

    /// <summary>Lịch sử duyệt web — SQLite theo schema Chrome (bảng urls/visits/meta).</summary>
    public static string HistoryFile(string profileName) => Path.Combine(ProfileDir(profileName), "History");

    /// <summary>Bookmark — JSON (giống Chrome: file Bookmarks, không phải SQLite).</summary>
    public static string BookmarksFile(string profileName) => Path.Combine(ProfileDir(profileName), "Bookmarks");

    /// <summary>Từ khoá gợi ý Omnibox (Shortcuts) — SQLite theo schema Chrome.</summary>
    public static string ShortcutsFile(string profileName) => Path.Combine(ProfileDir(profileName), "Shortcuts");

    /// <summary>Autofill form/địa chỉ/thẻ (Web Data) — SQLite theo schema Chrome.</summary>
    public static string WebDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "Web Data");

    /// <summary>Mật khẩu đã lưu (Login Data) — SQLite theo schema Chrome.</summary>
    public static string LoginDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "Login Data");

    public static string PreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Preferences");

    /// <summary>Avatar icon for the profile (.ico)</summary>
    public static string AvatarIconFile(string profileName) => Path.Combine(ProfileDir(profileName), "avatar.ico");

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
        Directory.CreateDirectory(ProfileDir(profileName));
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

    /// <summary>
    /// Di chuyển dữ liệu từ bố cục cũ sang bố cục CocCoc mới (chạy 1 lần, idempotent).
    /// File JSON cũ được đưa vào thư mục profile mới với hậu tố ".migrate" để những service
    /// (History/Autofill/Bookmark) đọc JSON, chuyển sang SQLite, rồi xoá file migrate đi.
    ///   1) %LOCALAPPDATA%\HecoBrowser\{bookmarks.json, autofill.json, Cache}        (rất cũ, phẳng)
    ///   2) %LOCALAPPDATA%\HecoBrowser\User Data\...                                  (bố cục trước đó)
    ///   → %LOCALAPPDATA%\HecoBrowser\Browser\User Data\
    /// Cache cũ của từng profile được gộp vào Cache chung ở root.
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            var appRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HecoBrowser");

            // 1) Bố cục phẳng rất cũ (chỉ có profile mặc định)
            var defaultDir = ProfileDir(DefaultProfileName);
            EnsureProfileDir(DefaultProfileName);
            MigrateFile(Path.Combine(appRoot, "bookmarks.json"), Path.Combine(defaultDir, "Bookmarks"));
            MigrateFile(Path.Combine(appRoot, "autofill.json"), Path.Combine(defaultDir, "Autofill.migrate"));
            MigrateDir(Path.Combine(appRoot, "Cache"), SharedCacheDir);

            // 2) Bố cục User Data trước đây
            var oldRoot = Path.Combine(appRoot, "User Data");
            if (Directory.Exists(oldRoot))
            {
                MigrateFile(Path.Combine(oldRoot, "Local State"), LocalStatePath);

                foreach (var dir in Directory.GetDirectories(oldRoot))
                {
                    var name = Path.GetFileName(dir);
                    var targetDir = Path.Combine(Root, name);
                    Directory.CreateDirectory(targetDir);

                    // Bookmarks giữ JSON (giống Chrome) → rename thẳng.
                    MigrateFile(Path.Combine(dir, "Bookmarks.json"), Path.Combine(targetDir, "Bookmarks"));
                    // History / autofill (trước đây là "Login Data.json") → file migrate JSON để service chuyển sang SQLite.
                    MigrateFile(Path.Combine(dir, "History.json"), Path.Combine(targetDir, "History.migrate"));
                    MigrateFile(Path.Combine(dir, "Login Data.json"), Path.Combine(targetDir, "Autofill.migrate"));
                    MigrateFile(Path.Combine(dir, "Preferences.json"), Path.Combine(targetDir, "Preferences"));

                    // Cache riêng của profile cũ → gộp vào cache chung (chỉ nếu chưa có)
                    var oldCache = Path.Combine(dir, "Cache");
                    if (Directory.Exists(oldCache) && !Directory.Exists(SharedCacheDir))
                        MigrateDir(oldCache, SharedCacheDir);
                }
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

    private static void MigrateDir(string source, string target)
    {
        try
        {
            if (Directory.Exists(source) && !Directory.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Directory.Move(source, target);
            }
        }
        catch { }
    }
}
