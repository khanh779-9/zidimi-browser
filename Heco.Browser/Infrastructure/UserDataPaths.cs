using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Đường dẫn dữ liệu người dùng theo mô hình giống CocCoc/Chromium:
///   %LOCALAPPDATA%\HecoBrowser\Browser\User Data\
///       Local State        (JSON: metadata, profile info_cache)
///       Default\           (profile mặc định)
///       &lt;ProfileName&gt;\  (các profile khác — tên được làm sạch ký tự không hợp lệ)
///
/// QUAN TRỌNG: thư mục User Data cũng là nơi CEF/Chromium tạo profile của riêng nó,
/// nên Chromium SỞ HỮU các file `Preferences`, `Secure Preferences`, `Bookmarks`,
/// `History`, `Web Data`, `Login Data`, `Cookies`... App KHÔNG được ghi vào những tên
/// này (trước đây ghi trùng → làm hỏng profile → lỗi "Something went wrong...").
/// Mọi dữ liệu riêng của app lưu dưới tiền tố heco_:
///   heco_setting.json   ↔ Preferences (cấu hình AppSettings)
///   heco_bookmarks.json ↔ Bookmarks
///   heco_history.db     ↔ History
///   heco_autofill.db    ↔ Web Data
///   heco_login.db       ↔ Login Data
///   heco_shortcuts.db   ↔ Shortcuts
/// CEF dùng các file Chromium riêng của nó. Cache CEF được chia sẻ ở root.
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

    /// <summary>Lịch sử duyệt web của app — SQLite riêng (không đụng file History của Chromium).</summary>
    public static string HistoryFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_history.db");

    /// <summary>Bookmark của app — JSON riêng (không đụng file Bookmarks của Chromium).</summary>
    public static string BookmarksFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_bookmarks.json");

    /// <summary>Từ khoá gợi ý Omnibox của app — SQLite riêng.</summary>
    public static string ShortcutsFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_shortcuts.db");

    /// <summary>Autofill của app (địa chỉ/thẻ) — SQLite riêng.</summary>
    public static string WebDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_autofill.db");

    /// <summary>Mật khẩu đã lưu của app — SQLite riêng.</summary>
    public static string LoginDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_login.db");

    /// <summary>Cấu hình AppSettings của app — JSON riêng, không ghi vào file Preferences của Chromium.</summary>
    public static string PreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_setting.json");

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
        RegisterProfiles(new[] { profileName });
    }

    /// <summary>Đăng ký nhiều profile vào info_cache của Local State trong một lần đọc/ghi (tránh
    /// ghi lại toàn bộ file nhiều lần khi có nhiều profile — tối ưu khởi động).</summary>
    public static void RegisterProfiles(IEnumerable<string> profileNames)
    {
        try
        {
            var names = profileNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            if (names.Count == 0) return;

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

            var profile = (System.Text.Json.Nodes.JsonObject?)root["profile"]
                          ?? (System.Text.Json.Nodes.JsonObject)(root["profile"] = new System.Text.Json.Nodes.JsonObject());
            var infoCache = (System.Text.Json.Nodes.JsonObject?)profile["info_cache"]
                            ?? (System.Text.Json.Nodes.JsonObject)(profile["info_cache"] = new System.Text.Json.Nodes.JsonObject());

            foreach (var name in names)
            {
                var folder = ProfileFolder(name);
                if (infoCache.ContainsKey(folder)) continue;
                infoCache[folder] = new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = name,
                    ["user_name"] = name,
                };
            }

            File.WriteAllText(path, JsonSerializer.Serialize(root,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* không nghiêm trọng — chỉ là metadata */ }
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
            MigrateFile(Path.Combine(appRoot, "bookmarks.json"), Path.Combine(defaultDir, "heco_bookmarks.json"));
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

                    // Bookmarks giữ JSON (giống Chrome) → rename thẳng sang tên heco_*.
                    MigrateFile(Path.Combine(dir, "Bookmarks.json"), Path.Combine(targetDir, "heco_bookmarks.json"));
                    // History / autofill (trước đây là "Login Data.json") → file migrate JSON để service chuyển sang SQLite.
                    MigrateFile(Path.Combine(dir, "History.json"), Path.Combine(targetDir, "History.migrate"));
                    MigrateFile(Path.Combine(dir, "Login Data.json"), Path.Combine(targetDir, "Autofill.migrate"));
                    MigrateFile(Path.Combine(dir, "Preferences.json"), Path.Combine(targetDir, "heco_setting.json"));

                    // Cache riêng của profile cũ → gộp vào cache chung (chỉ nếu chưa có)
                    var oldCache = Path.Combine(dir, "Cache");
                    if (Directory.Exists(oldCache) && !Directory.Exists(SharedCacheDir))
                        MigrateDir(oldCache, SharedCacheDir);
                }
            }

            RegisterProfile(DefaultProfileName);
        }
        catch { }

        // Di chuyển dữ liệu app còn sót lại đang chiếm tên file Chromium sang tên heco_*,
        // để Chromium mở được profile sạch (fix lỗi "Something went wrong when opening your profile").
        MigrateAppDataToHeco();
    }

    /// <summary>Quét mọi thư mục profile trong User Data, di chuyển file app cũ (tên trùng Chromium)
    /// sang tên heco_*. Chỉ di chuyển khi file rõ ràng là dữ liệu của app (không đụng file của Chromium).</summary>
    public static void MigrateAppDataToHeco()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            foreach (var dir in Directory.EnumerateDirectories(Root))
            {
                try
                {
                    var profileDir = Path.Combine(Root, Path.GetFileName(dir));
                    MigrateAppSettingsFile(profileDir);
                    MigrateAppBookmarksFile(profileDir);
                    MigrateAppSqliteFile(profileDir, "History", "heco_history.db", "24");
                    MigrateAppSqliteFile(profileDir, "Web Data", "heco_autofill.db", "102");
                    MigrateAppSqliteFile(profileDir, "Login Data", "heco_login.db", "102");
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>Di chuyển Preferences (JSON của app) → heco_setting.json nếu nó chứa key của app.
    /// File Preferences của Chromium thì để nguyên.</summary>
    private static void MigrateAppSettingsFile(string profileDir)
    {
        var source = Path.Combine(profileDir, "Preferences");
        var target = Path.Combine(profileDir, "heco_setting.json");
        if (!File.Exists(source) || File.Exists(target)) return;
        try
        {
            var text = File.ReadAllText(source);
            // Key app dùng (PascalCase) không xuất hiện trong Preferences của Chromium.
            if (text.Contains("\"HomePageUrl\"") || text.Contains("\"FontSize\"") || text.Contains("\"StartupBehavior\""))
                File.Move(source, target);
        }
        catch { }
    }

    /// <summary>Di chuyển Bookmarks dạng mảng JSON của app → heco_bookmarks.json.
    /// Bookmarks của Chromium là object JSON → để nguyên.</summary>
    private static void MigrateAppBookmarksFile(string profileDir)
    {
        var source = Path.Combine(profileDir, "Bookmarks");
        var target = Path.Combine(profileDir, "heco_bookmarks.json");
        if (!File.Exists(source) || File.Exists(target)) return;
        try
        {
            var text = File.ReadAllText(source).TrimStart();
            if (text.StartsWith("["))
                File.Move(source, target);
        }
        catch { }
    }

    /// <summary>Di chuyển SQLite của app (có meta version = phiên bản app) sang tên heco_*.
    /// SQLite của Chromium (version khác / không có meta) thì để nguyên.</summary>
    private static void MigrateAppSqliteFile(string profileDir, string sourceName, string targetName, string appVersion)
    {
        var source = Path.Combine(profileDir, sourceName);
        var target = Path.Combine(profileDir, targetName);
        if (!File.Exists(source) || File.Exists(target)) return;
        try
        {
            if (ReadMetaVersion(source) == appVersion)
                File.Move(source, target);
        }
        catch { }
    }

    private static string? ReadMetaVersion(string dbPath)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM meta WHERE key='version' LIMIT 1;";
            var v = cmd.ExecuteScalar();
            return v?.ToString();
        }
        catch { return null; }
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
