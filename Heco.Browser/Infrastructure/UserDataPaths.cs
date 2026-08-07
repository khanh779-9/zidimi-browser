using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// User data paths following the CocCoc/Chromium model:
///   %LOCALAPPDATA%\HecoBrowser\Browser\User Data\
///       Local State     (JSON: metadata, profile info_cache)
///       (Default)       (the default profile)
///       (ProfileName)\  (other profiles — folder name cleaned of invalid characters)
///
/// IMPORTANT: the User Data folder is also where CEF/Chromium creates its own profile,
/// so Chromium OWNS these files: `Preferences`, `Secure Preferences`, `Bookmarks`,
/// `History`, `Web Data`, `Login Data`, `Cookies`... The app MUST NOT write to those names
/// (in the past writing over them corrupted the profile and caused "Something went wrong...").
/// All app-specific data is stored under the heco_ prefix:
///   heco_setting.json   AppSettings config (Preferences)
///   heco_bookmarks.json Bookmarks
///   heco_shortcuts.db   Shortcuts
/// CEF uses its own Chromium files. CEF's cache is shared at the root.
/// </summary>
public static class UserDataPaths
{
    public const string DefaultProfileName = "Cá nhân";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HecoBrowser", "Browser", "User Data");

    public static string LocalStatePath => Path.Combine(Root, "Local State");

    /// <summary>Shared cache for all profiles, kept directly in the User Data root (like CocCoc).</summary>
    public static string SharedCacheDir => Path.Combine(Root);

    /// <summary>The on-disk folder name for a profile (Default for the default profile).</summary>
    public static string ProfileFolder(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == DefaultProfileName)
            return "Default";
        return CleanProfileName(profileName);
    }

    public static string ProfileDir(string profileName) => Path.Combine(Root, ProfileFolder(profileName));

    /// <summary>Chromium's browsing history.</summary>
    public static string HistoryFile(string profileName) => Path.Combine(ProfileDir(profileName), "History");

    /// <summary>Chromium's bookmarks (JSON).</summary>
    public static string BookmarksFile(string profileName) => Path.Combine(ProfileDir(profileName), "Bookmarks");

    /// <summary>Chromium's Omnibox suggestion keywords. (SQLite)</summary>
    public static string ShortcutsFile(string profileName) => Path.Combine(ProfileDir(profileName), "Shortcuts");

    /// <summary>Chromium's autofill data (addresses/cards). (SQLite)</summary>
    public static string WebDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "Web Data");

    /// <summary>Chromium's autofill data for synced accounts. (SQLite)</summary>
    public static string AccountWebDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "Account Web Data");

    /// <summary>Chromium's saved passwords. (SQLite)</summary>
    public static string LoginDataFile(string profileName) => Path.Combine(ProfileDir(profileName), "Login Data");

    /// <summary>Chromium's passwords for synced accounts. (SQLite)</summary>
    public static string LoginDataForAccountFile(string profileName) => Path.Combine(ProfileDir(profileName), "Login Data For Account");

    /// <summary>Favicons for saved/visited pages. (SQLite)</summary>
    public static string FaviconsFile(string profileName) => Path.Combine(ProfileDir(profileName), "Favicons");

    /// <summary>Most-visited pages. (SQLite)</summary>
    public static string TopSitesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Top Sites");

    /// <summary>Browser cookies. (SQLite)</summary>
    public static string CookiesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Network", "Cookies");

    /// <summary>Chromium's Preferences configuration. (JSON)</summary>
    public static string ChromiumPreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Preferences");

    /// <summary>Chromium's Secure Preferences configuration. (JSON)</summary>
    public static string SecurePreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "Secure Preferences");

    /// <summary>The app's AppSettings configuration — its own JSON, never written into Chromium's Preferences file.</summary>
    public static string PreferencesFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_setting.json");

    /// <summary>The app's download list — its own SQLite (avoids touching Chromium's downloads table).</summary>
    public static string DownloadsFile(string profileName) => Path.Combine(ProfileDir(profileName), "heco_downloads.db");

    /// <summary>Avatar icon for the profile (.ico)</summary>
    public static string AvatarIconFile(string profileName) => Path.Combine(ProfileDir(profileName), "avatar.ico");

    /// <summary>Cleans a profile name so it can be used as a folder name.</summary>
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
/// Reads/writes "Local State" (JSON like CocCoc). Uses JsonNode so the structure stays extensible.
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
        catch { /* not critical — just metadata */ }
    }

    /// <summary>Registers a profile into Local State's info_cache (like CocCoc).</summary>
    public static void RegisterProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;
        RegisterProfiles(new[] { profileName });
    }

/// <summary>Registers multiple profiles into Local State's info_cache in a single read/write (avoids
/// rewriting the whole file repeatedly when many profiles exist — optimizes startup).</summary>
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
catch { /* not critical — just metadata */ }
    }

    /// <summary>
    /// Moves data from the old layout to the new CocCoc layout (runs once, idempotent).
    /// Old JSON files are moved into the new profile folder with a ".migrate" suffix so the
    /// History/Autofill/Bookmark services can read the JSON, convert it to SQLite, then delete the migrate file.
    ///   1) %LOCALAPPDATA%\HecoBrowser\{bookmarks.json, autofill.json, Cache}        (very old, flat)
    ///   2) %LOCALAPPDATA%\HecoBrowser\User Data\...                               (the previous layout)
    ///   → %LOCALAPPDATA%\HecoBrowser\Browser\User Data\
    /// Each profile's old cache is merged into the shared cache at the root.
    /// </summary>
    public static void MigrateLegacyData()
    {
        try
        {
            var appRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HecoBrowser");

            // 1) Very old flat layout (only the default profile)
            var defaultDir = ProfileDir(DefaultProfileName);
            EnsureProfileDir(DefaultProfileName);
            MigrateFile(Path.Combine(appRoot, "bookmarks.json"), Path.Combine(defaultDir, "heco_bookmarks.json"));
            MigrateFile(Path.Combine(appRoot, "autofill.json"), Path.Combine(defaultDir, "Autofill.migrate"));
            MigrateDir(Path.Combine(appRoot, "Cache"), SharedCacheDir);

            // 2) The previous User Data layout
            var oldRoot = Path.Combine(appRoot, "User Data");
            if (Directory.Exists(oldRoot))
            {
                MigrateFile(Path.Combine(oldRoot, "Local State"), LocalStatePath);

                foreach (var dir in Directory.GetDirectories(oldRoot))
                {
                    var name = Path.GetFileName(dir);
                    var targetDir = Path.Combine(Root, name);
                    Directory.CreateDirectory(targetDir);

                    // Bookmarks stay as JSON (like Chrome) → just rename straight to the heco_* names.
                    MigrateFile(Path.Combine(dir, "Bookmarks.json"), Path.Combine(targetDir, "heco_bookmarks.json"));
                    // History / autofill (previously "Login Data.json") → JSON migrate files for the service to convert to SQLite.
                    MigrateFile(Path.Combine(dir, "History.json"), Path.Combine(targetDir, "History.migrate"));
                    MigrateFile(Path.Combine(dir, "Login Data.json"), Path.Combine(targetDir, "Autofill.migrate"));
                    MigrateFile(Path.Combine(dir, "Preferences.json"), Path.Combine(targetDir, "heco_setting.json"));

                    // Each profile's old cache → merged into the shared cache (only if not already present)
                    var oldCache = Path.Combine(dir, "Cache");
                    if (Directory.Exists(oldCache) && !Directory.Exists(SharedCacheDir))
                        MigrateDir(oldCache, SharedCacheDir);
                }
            }

            RegisterProfile(DefaultProfileName);
        }
        catch { }

        // Move leftover app data still occupying Chromium file names over to the heco_* names,
        // so Chromium can open a clean profile (fixes the "Something went wrong when opening your profile" error).
        MigrateAppDataToHeco();
    }

/// <summary>Scans every profile folder in User Data, moving over old app files (which shared Chromium's
/// names) to the heco_* names. Only moves a file when it is clearly app data (never touches Chromium's files).</summary>
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
                }
                catch { }
            }
        }
        catch { }
    }

/// <summary>Moves Preferences (the app's JSON) → heco_setting.json if it contains app keys.
/// Chromium's Preferences file is left untouched.</summary>
    private static void MigrateAppSettingsFile(string profileDir)
    {
        var source = Path.Combine(profileDir, "Preferences");
        var target = Path.Combine(profileDir, "heco_setting.json");
        if (!File.Exists(source) || File.Exists(target)) return;
        try
        {
            var text = File.ReadAllText(source);
            // App keys (PascalCase) never appear in Chromium's Preferences.
            if (text.Contains("\"HomePageUrl\"") || text.Contains("\"FontSize\"") || text.Contains("\"StartupBehavior\""))
                File.Move(source, target);
        }
        catch { }
    }

/// <summary>Moves the app's array-style JSON Bookmarks → heco_bookmarks.json.
/// Chromium's Bookmarks is a JSON object → left untouched.</summary>
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
