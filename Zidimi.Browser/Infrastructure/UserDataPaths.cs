using System.IO;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Chromium/CEF User Data paths used by Zidimi.
///
/// Persistence rule:
///  - Chromium/CEF-created stores are the source of truth.
///  - Zidimi must not create mirror databases/settings files (zidimi_*.db/json, profile.json, ...).
///  - Use IRequestContext GetPreference/SetPreference for Chromium profile Preferences.
///  - Do not invent a fallback persistence store when CefSharp does not expose a Chromium pref.
///  - Native databases that CEF does not expose are read-only while Chromium is running.
///
/// A profile directory may be selected/created as the CachePath for a new RequestContext; CEF then
/// creates and owns Preferences, History, Cookies, storage databases and all other persisted files.
/// Zidimi's avatar fallback is generated in memory and does not add a profile-side file.
/// </summary>
public static class UserDataPaths
{
    public const string DefaultProfileId = "Default";

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ZidimiBrowser", "Browser", "User Data");

    public static string RootCacheDir => Root;
    public static string ChromeDebugLogFile => Path.Combine(Root, "chrome_debug.log");

    public static string NormalizeProfileId(string? profileId)
    {
        var value = profileId?.Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "Cá nhân", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            return DefaultProfileId;

        value = value.Replace(Path.DirectorySeparatorChar, '_')
                     .Replace(Path.AltDirectorySeparatorChar, '_');
        foreach (var invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        value = value.Trim().TrimEnd('.');
        return value is "." or ".." || string.IsNullOrWhiteSpace(value)
            ? DefaultProfileId
            : value;
    }

    public static string ProfileFolder(string profileId) => NormalizeProfileId(profileId);
    public static string ProfileDir(string profileId) => Path.Combine(Root, ProfileFolder(profileId));

    // Chromium-created JSON / SQLite / LevelDB roots.
    public static string HistoryFile(string profileId) => Path.Combine(ProfileDir(profileId), "History");
    public static string BookmarksFile(string profileId) => Path.Combine(ProfileDir(profileId), "Bookmarks");
    public static string WebDataFile(string profileId) => Path.Combine(ProfileDir(profileId), "Web Data");
    public static string LoginDataFile(string profileId) => Path.Combine(ProfileDir(profileId), "Login Data");

    /// <summary>Chromium-owned native extension package/cache tree. Zidimi treats it as read-only.</summary>
    public static string ExtensionsDir(string profileId) => Path.Combine(ProfileDir(profileId), "Extensions");

}
