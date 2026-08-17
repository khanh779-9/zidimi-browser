using CefSharp;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// In-memory profile catalog. Chromium profile identity is discovered from conventional profile
/// cache directories only; display names are read from each profile's own CEF RequestContext via
/// GetPreference("profile.name"). Zidimi never opens Local State/profile.info_cache or Preferences.
/// </summary>
public static class ChromiumProfileCatalog
{
    private static readonly object Gate = new();
    private static Dictionary<string, ProfileInfo> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public sealed class ProfileInfo
    {
        public string Id { get; init; } = UserDataPaths.DefaultProfileId;
        public string DisplayName { get; init; } = UserDataPaths.DefaultProfileId;
        public string UserName { get; init; } = string.Empty;
    }

    public static IReadOnlyList<string> DiscoverProfileIds(IEnumerable<string>? additionalIds = null)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (additionalIds != null)
        {
            foreach (var raw in additionalIds)
            {
                var id = UserDataPaths.NormalizeProfileId(raw);
                if (Directory.Exists(UserDataPaths.ProfileDir(id))) ids.Add(id);
            }
        }

        try
        {
            if (Directory.Exists(UserDataPaths.Root))
            {
                foreach (var directory in Directory.EnumerateDirectories(UserDataPaths.Root))
                {
                    var id = Path.GetFileName(directory);
                    if (IsConventionalProfileId(id)) ids.Add(UserDataPaths.NormalizeProfileId(id));
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Profiles", ex, "Enumerating Chromium profile directories.");
        }

        if (ids.Count == 0) ids.Add(UserDataPaths.DefaultProfileId);

        return ids
            .OrderBy(id => string.Equals(id, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(ProfileSortNumber)
            .ThenBy(id => id, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static async Task RefreshFromCefAsync(IEnumerable<string>? registeredProfileIds = null)
    {
        var ids = DiscoverProfileIds(registeredProfileIds);
        var found = new Dictionary<string, ProfileInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in ids)
        {
            var displayName = CefSettingsStore.GetPendingProfileDisplayName(id);

            if (string.IsNullOrWhiteSpace(displayName) &&
                Cef.IsInitialized == true && App.CefReady && App.RequestContexts != null)
            {
                try
                {
                    var context = await App.RequestContexts.GetProfileContextReadyAsync(id).ConfigureAwait(false);
                    var nativeName = await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.ProfileName)
                        .ConfigureAwait(false);
                    if (CefSettingsStore.AsString(nativeName) is { Length: > 0 } name)
                        displayName = name.Trim();
                }
                catch (Exception ex)
                {
                    AppLogger.Log("Profiles", ex, $"Reading CEF profile name for '{id}'.");
                }
            }

            found[id] = new ProfileInfo
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? DefaultDisplayName(id) : displayName.Trim(),
            };
        }

        lock (Gate) _profiles = found;
    }

    public static IReadOnlyList<ProfileInfo> GetProfiles(IEnumerable<string>? registeredProfileIds = null)
    {
        Dictionary<string, ProfileInfo> cached;
        lock (Gate)
            cached = new Dictionary<string, ProfileInfo>(_profiles, StringComparer.OrdinalIgnoreCase);

        var ids = DiscoverProfileIds(registeredProfileIds);
        var result = new List<ProfileInfo>(ids.Count);
        foreach (var id in ids)
        {
            var pendingName = CefSettingsStore.GetPendingProfileDisplayName(id);
            if (cached.TryGetValue(id, out var info))
            {
                result.Add(new ProfileInfo
                {
                    Id = id,
                    DisplayName = string.IsNullOrWhiteSpace(pendingName) ? info.DisplayName : pendingName.Trim(),
                    UserName = info.UserName,
                });
            }
            else
            {
                result.Add(new ProfileInfo
                {
                    Id = id,
                    DisplayName = string.IsNullOrWhiteSpace(pendingName) ? DefaultDisplayName(id) : pendingName.Trim(),
                });
            }
        }

        return result;
    }

    public static void RememberProfileInfo(string profileId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        var id = UserDataPaths.NormalizeProfileId(profileId);
        lock (Gate)
        {
            _profiles[id] = new ProfileInfo
            {
                Id = id,
                DisplayName = displayName.Trim(),
                UserName = _profiles.TryGetValue(id, out var old) ? old.UserName : string.Empty,
            };
        }
    }

    public static void ForgetProfile(string profileId)
    {
        var id = UserDataPaths.NormalizeProfileId(profileId);
        lock (Gate) _profiles.Remove(id);
    }

    public static string ResolveProfileId(string? idOrDisplayName, IEnumerable<string>? registeredProfileIds = null)
    {
        var raw = idOrDisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return UserDataPaths.DefaultProfileId;

        var profiles = GetProfiles(registeredProfileIds);
        var byId = profiles.FirstOrDefault(p => string.Equals(p.Id, raw, StringComparison.OrdinalIgnoreCase));
        if (byId != null) return byId.Id;

        var byName = profiles.FirstOrDefault(p => string.Equals(p.DisplayName, raw, StringComparison.OrdinalIgnoreCase));
        if (byName != null) return byName.Id;

        if (string.Equals(raw, "Cá nhân", StringComparison.OrdinalIgnoreCase))
            return UserDataPaths.DefaultProfileId;

        return UserDataPaths.NormalizeProfileId(raw);
    }

    public static string GetDisplayName(string? profileId)
    {
        var id = UserDataPaths.NormalizeProfileId(profileId);
        return GetProfiles(new[] { id })
                   .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                   ?.DisplayName
               ?? DefaultDisplayName(id);
    }

    public static string NextProfileId(IEnumerable<string> existingProfileIds)
    {
        var taken = new HashSet<string>(DiscoverProfileIds(existingProfileIds), StringComparer.OrdinalIgnoreCase);
        for (var n = 1; ; n++)
        {
            var candidate = $"Profile {n}";
            if (!taken.Contains(candidate) && !Directory.Exists(UserDataPaths.ProfileDir(candidate)))
                return candidate;
        }
    }

    private static bool IsConventionalProfileId(string? id)
        => !string.IsNullOrWhiteSpace(id) &&
           (string.Equals(id, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase) ||
            (id.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) && ProfileSortNumber(id) != int.MaxValue));

    private static int ProfileSortNumber(string id)
    {
        if (string.Equals(id, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase)) return 0;
        if (id.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(id[8..].Trim(), out var n) && n >= 0)
            return n;
        return int.MaxValue;
    }

    private static string DefaultDisplayName(string id)
        => string.Equals(id, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase) ? "Default" : id;
}
