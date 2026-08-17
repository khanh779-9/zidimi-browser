using System.Text.Json;
using System.Text.Json.Nodes;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Read-only catalog / toolbar bridge over Chromium's native extension installation.
/// Chromium owns installation, uninstall, enabled state, package layout, Secure Preferences,
/// Extension State and Local Extension Settings. Zidimi never downloads CRX files, stages a
/// parallel package directory, rewrites extension preferences, or deletes Chromium extension data.
/// </summary>
public sealed class ExtensionService
{
    private static readonly Lazy<ExtensionService> LazyInstance = new(() => new ExtensionService());
    public static ExtensionService Instance => LazyInstance.Value;

    private readonly object _gate = new();
    private readonly List<ExtensionInfo> _extensions = new();
    private string _catalogProfileId = string.Empty;

    private ExtensionService() { }

    public event EventHandler? ExtensionsChanged;

    public IEnumerable<ExtensionInfo> InstalledExtensions
    {
        get
        {
            lock (_gate)
            {
                EnsureFilesystemCatalogLoaded();
                return _extensions.ToList();
            }
        }
    }

    public IEnumerable<ExtensionInfo> PinnedExtensions
        => InstalledExtensions.Where(e => e.IsPinned && e.IsEnabled && IsExtensionAvailable(e)).ToList();

    /// <summary>
    /// Reads extensions.settings and extensions.pinned_extensions through CEF. No Chromium file is
    /// parsed directly for registration state; filesystem access is limited to installed manifest/icon
    /// resources that Chromium itself placed in the profile Extensions tree.
    /// </summary>
    public async Task RefreshFromCefAsync(IRequestContext? context, string profileId)
    {
        if (context == null || context.IsDisposed || Cef.IsInitialized != true) return;
        profileId = UserDataPaths.NormalizeProfileId(profileId);

        try
        {
            var rawSettings = await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.ExtensionSettings)
                .ConfigureAwait(false);
            var settings = CefSettingsStore.AsDictionary(rawSettings);
            var pinnedRaw = await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.PinnedExtensions)
                .ConfigureAwait(false);
            var pinned = new HashSet<string>(CefSettingsStore.AsStringList(pinnedRaw), StringComparer.OrdinalIgnoreCase);

            var found = new List<ExtensionInfo>();
            if (settings != null)
            {
                foreach (var (runtimeId, rawEntry) in settings)
                {
                    var entry = CefSettingsStore.AsDictionary(rawEntry);
                    if (entry == null) continue;

                    var enabled = true;
                    if (entry.TryGetValue("state", out var stateObj) && TryToInt(stateObj, out var state))
                        enabled = state == 1;
                    if (entry.TryGetValue("disable_reasons", out var reasonsObj) && HasAnyValue(reasonsObj))
                        enabled = false;

                    var pathHint = entry.TryGetValue("path", out var pathObj)
                        ? CefSettingsStore.AsString(pathObj)
                        : null;
                    if (!TryResolveNativeExtensionRoot(profileId, runtimeId, pathHint, out var root) ||
                        !TryReadManifestMetadata(root, out var metadata))
                        continue;

                    var ext = new ExtensionInfo
                    {
                        Id = runtimeId,
                        RuntimeId = runtimeId,
                        StoreId = runtimeId,
                        Path = root,
                        IsEnabled = enabled,
                        IsPinned = pinned.Contains(runtimeId),
                        IsAvailable = true,
                    };
                    ApplyMetadata(ext, metadata);
                    found.Add(ext);
                }
            }

            // Some Chromium builds expose only a subset of extensions.settings through the managed
            // preference API. Merge a read-only filesystem catalog so toolbar metadata still renders;
            // native preference entries always win for enabled/pinned state.
            MergeFilesystemCatalog(found, profileId, pinned);

            lock (_gate)
            {
                _extensions.Clear();
                _extensions.AddRange(found
                    .GroupBy(e => e.RuntimeId ?? e.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.IsEnabled).First())
                    .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase));
                _catalogProfileId = profileId;
            }

            NotifyExtensionsChanged();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Extensions", ex, "Reading native Chromium extension state through CefSharp.");
        }
    }

    public void RefreshForCurrentProfile()
    {
        lock (_gate)
        {
            _catalogProfileId = string.Empty;
            EnsureFilesystemCatalogLoaded();
        }
        NotifyExtensionsChanged();
    }

    public bool IsExtensionAvailable(ExtensionInfo ext)
        => ext != null && TryResolveNativeExtensionRoot(
            AppSettings.Global.CurrentProfile,
            ext.RuntimeId ?? ext.Id,
            ext.Path,
            out _);

    /// <summary>
    /// Native Chrome runtime already owns installed extensions. This method only refreshes the
    /// managed toolbar catalog after a browser exists; no parallel package loader is involved.
    /// </summary>
    public Task EnsureProfileRuntimeLoadedAsync(IChromiumWebBrowserBase browser)
    {
        if (browser == null || browser.IsDisposed) return Task.CompletedTask;
        var context = App.RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile);
        return RefreshFromCefAsync(context, AppSettings.Global.CurrentProfile);
    }

    public Task RefreshRuntimeStateAsync(IChromiumWebBrowserBase? browser)
    {
        var context = App.RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile);
        return RefreshFromCefAsync(context, AppSettings.Global.CurrentProfile);
    }

    /// <summary>Resolve action.default_popup/side_panel.default_path from Chromium's installed package.</summary>
    public (bool success, string message, string? popupUrl) ResolveDefaultAction(ExtensionInfo ext)
    {
        if (ext == null) return (false, LanguageManager.Instance["Ext_Unavailable"], null);
        if (!ext.IsEnabled) return (false, LanguageManager.Instance["Ext_DisabledAction"], null);

        var runtimeId = ext.RuntimeId ?? ext.Id;
        if (!TryResolveNativeExtensionRoot(AppSettings.Global.CurrentProfile, runtimeId, ext.Path, out var root))
            return (false, LanguageManager.Instance["Ext_FilesMissing"], null);

        if (TryReadManifestMetadata(root, out var metadata)) ApplyMetadata(ext, metadata);
        var actionPath = !string.IsNullOrWhiteSpace(ext.PopupPath) ? ext.PopupPath : ext.SidePanelPath;
        if (string.IsNullOrWhiteSpace(actionPath))
            return (false, LanguageManager.Instance["Ext_ActionNoPopup"], null);

        var relative = actionPath.Replace('\\', '/').TrimStart('/');
        var pathOnly = relative.Split('?', '#')[0];
        if (ResolveSafeManifestFile(root, pathOnly) == null)
            return (false, LanguageManager.Instance["Ext_ActionPopupMissing"], null);

        ext.Path = root;
        ext.RuntimeId = runtimeId;
        return (true, string.Empty, $"chrome-extension://{runtimeId}/{relative}");
    }

    /// <summary>
    /// Ask Chromium to trigger an action that has no popup. The DevTools Extensions domain talks to
    /// the native extension runtime; it does not create or persist a Zidimi extension registry.
    /// </summary>
    public async Task<(bool success, string message)> TriggerToolbarActionAsync(
        ExtensionInfo ext, IChromiumWebBrowserBase? activeBrowser)
    {
        if (ext == null) return (false, LanguageManager.Instance["Ext_Unavailable"]);
        if (!ext.IsEnabled) return (false, LanguageManager.Instance["Ext_DisabledAction"]);
        if (!ext.HasToolbarAction) return (false, LanguageManager.Instance["Ext_ActionNoPopup"]);
        if (activeBrowser == null || activeBrowser.IsDisposed || !activeBrowser.IsBrowserInitialized)
            return (false, LanguageManager.Instance["Ext_ActionUnavailable"]);

        var runtimeId = ext.RuntimeId ?? ext.Id;
        if (string.IsNullOrWhiteSpace(runtimeId))
            return (false, LanguageManager.Instance["Ext_ActionUnavailable"]);

        try
        {
            using var client = activeBrowser.GetDevToolsClient();
            var target = await client.Target.GetTargetInfoAsync().ConfigureAwait(false);
            var targetId = target?.TargetInfo?.TargetId;
            if (string.IsNullOrWhiteSpace(targetId))
                return (false, LanguageManager.Instance["Ext_ActionUnavailable"]);

            await client.ExecuteDevToolsMethodAsync("Extensions.triggerAction",
                new Dictionary<string, object> { ["id"] = runtimeId, ["targetId"] = targetId })
                .ConfigureAwait(false);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionAction", ex, $"Triggering native Chromium action for '{ext.Name}'.");
            return (false, LanguageManager.Instance["Ext_ActionUnavailable"]);
        }
    }

    /// <summary>Persist toolbar pin order in Chromium's extensions.pinned_extensions preference.</summary>
    public void TogglePinned(ExtensionInfo ext, bool pin)
    {
        if (ext == null) return;
        var runtimeId = ext.RuntimeId ?? ext.Id;
        if (string.IsNullOrWhiteSpace(runtimeId)) return;

        lock (_gate) ext.IsPinned = pin;
        NotifyExtensionsChanged();

        if (Cef.IsInitialized != true || !App.CefReady || App.RequestContexts == null) return;
        var profileId = UserDataPaths.NormalizeProfileId(AppSettings.Global.CurrentProfile);
        var context = App.RequestContexts.GetProfileContext(profileId);
        CefPreferenceWriteQueue.Enqueue($"Chromium pinned extensions '{profileId}'", async () =>
        {
            var currentRaw = await context.GetPreferenceSafeAsync(ChromiumPreferenceKeys.PinnedExtensions)
                .ConfigureAwait(false);
            var ids = CefSettingsStore.AsStringList(currentRaw);
            ids.RemoveAll(id => string.Equals(id, runtimeId, StringComparison.OrdinalIgnoreCase));
            if (pin) ids.Add(runtimeId);
            var success = await context.SetPreferenceSafeAsync(
                ChromiumPreferenceKeys.PinnedExtensions, ids.Cast<object>().ToList()).ConfigureAwait(false);
            if (!success)
                AppLogger.Log("Extensions", "Chromium rejected extensions.pinned_extensions update.");
        });
    }

    private void EnsureFilesystemCatalogLoaded()
    {
        var profileId = UserDataPaths.NormalizeProfileId(AppSettings.Global.CurrentProfile);
        if (string.Equals(_catalogProfileId, profileId, StringComparison.OrdinalIgnoreCase)) return;

        var found = new List<ExtensionInfo>();
        MergeFilesystemCatalog(found, profileId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _extensions.Clear();
        _extensions.AddRange(found.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase));
        _catalogProfileId = profileId;
    }

    private static void MergeFilesystemCatalog(
        List<ExtensionInfo> target,
        string profileId,
        HashSet<string> pinned)
    {
        var root = UserDataPaths.ExtensionsDir(profileId);
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var idDir in Directory.EnumerateDirectories(root))
            {
                var runtimeId = Path.GetFileName(idDir);
                if (string.IsNullOrWhiteSpace(runtimeId)) continue;
                if (!TryResolveNativeExtensionRoot(profileId, runtimeId, null, out var extensionRoot)) continue;
                if (!TryReadManifestMetadata(extensionRoot, out var metadata)) continue;

                var existing = target.FirstOrDefault(e =>
                    string.Equals(e.RuntimeId, runtimeId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Path = extensionRoot;
                    ApplyMetadata(existing, metadata);
                    continue;
                }

                var ext = new ExtensionInfo
                {
                    Id = runtimeId,
                    RuntimeId = runtimeId,
                    StoreId = runtimeId,
                    Path = extensionRoot,
                    IsEnabled = true,
                    IsPinned = pinned.Contains(runtimeId),
                    IsAvailable = true,
                };
                ApplyMetadata(ext, metadata);
                target.Add(ext);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Extensions", ex, $"Reading Chromium extension package metadata from '{root}'.");
        }
    }

    private static bool TryResolveNativeExtensionRoot(
        string profileId,
        string runtimeId,
        string? pathHint,
        out string root)
    {
        root = string.Empty;
        profileId = UserDataPaths.NormalizeProfileId(profileId);

        foreach (var candidate in CandidatePaths(profileId, runtimeId, pathHint))
        {
            if (TryResolveManifestDirectory(candidate, out root)) return true;
        }
        return false;
    }

    private static IEnumerable<string> CandidatePaths(string profileId, string runtimeId, string? pathHint)
    {
        if (!string.IsNullOrWhiteSpace(pathHint))
        {
            yield return pathHint;
            if (!Path.IsPathRooted(pathHint)) yield return Path.Combine(UserDataPaths.ProfileDir(profileId), pathHint);
        }

        var idRoot = Path.Combine(UserDataPaths.ExtensionsDir(profileId), runtimeId);
        yield return idRoot;
        if (Directory.Exists(idRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(idRoot).OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                yield return dir;
        }
    }

    private static bool TryResolveManifestDirectory(string? candidate, out string root)
    {
        root = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            var full = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(full, "manifest.json"))) { root = full; return true; }
            if (!Directory.Exists(full)) return false;

            var child = Directory.EnumerateDirectories(full)
                .Where(d => File.Exists(Path.Combine(d, "manifest.json")))
                .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (child == null) return false;
            root = Path.GetFullPath(child);
            return true;
        }
        catch { return false; }
    }

    private sealed record ManifestMetadata(
        string Name, string Version, string Description, int ManifestVersion,
        string? IconPath, string? PopupPath, string? SidePanelPath, bool HasToolbarAction);

    private static bool TryReadManifestMetadata(string folderPath, out ManifestMetadata metadata)
    {
        metadata = null!;
        try
        {
            var manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath)) return false;
            var node = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(manifestPath));
            if (node == null) return false;

            var name = GetJsonString(node, "name") ?? Path.GetFileName(folderPath);
            var version = GetJsonString(node, "version") ?? "1.0";
            var description = GetJsonString(node, "description") ?? string.Empty;
            var manifestVersion = node["manifest_version"]?.GetValue<int>() ?? 3;

            if (name.StartsWith("__MSG_", StringComparison.Ordinal) && name.EndsWith("__", StringComparison.Ordinal))
                name = ResolveLocaleString(folderPath, name, node) ?? Path.GetFileName(folderPath);
            if (description.StartsWith("__MSG_", StringComparison.Ordinal) && description.EndsWith("__", StringComparison.Ordinal))
                description = ResolveLocaleString(folderPath, description, node) ?? string.Empty;

            metadata = new ManifestMetadata(
                name, version, description, manifestVersion,
                ResolveIconPath(folderPath, node), ResolvePopupPath(node),
                ResolveSidePanelPath(node), HasToolbarAction(node));
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionManifest", ex, folderPath);
            return false;
        }
    }

    private static void ApplyMetadata(ExtensionInfo ext, ManifestMetadata meta)
    {
        ext.Name = meta.Name;
        ext.Version = meta.Version;
        ext.Description = meta.Description;
        ext.ManifestVersion = meta.ManifestVersion;
        ext.IconPath = meta.IconPath;
        ext.PopupPath = meta.PopupPath;
        ext.SidePanelPath = meta.SidePanelPath;
        ext.HasToolbarAction = meta.HasToolbarAction;
    }

    private static string? GetJsonString(JsonObject node, string key)
        => node.TryGetPropertyValue(key, out var value) && value != null ? value.ToString() : null;

    private static bool HasToolbarAction(JsonObject node)
        => node.ContainsKey("action") || node.ContainsKey("browser_action") || node.ContainsKey("page_action");

    private static string? ResolvePopupPath(JsonObject node)
    {
        foreach (var key in new[] { "action", "browser_action", "page_action" })
            if (node.TryGetPropertyValue(key, out var actionNode) && actionNode is JsonObject action &&
                action.TryGetPropertyValue("default_popup", out var popup) && popup != null &&
                !string.IsNullOrWhiteSpace(popup.ToString()))
                return popup.ToString().Trim().Replace('\\', '/').TrimStart('/');
        return null;
    }

    private static string? ResolveSidePanelPath(JsonObject node)
    {
        if (node.TryGetPropertyValue("side_panel", out var side) && side is JsonObject panel &&
            panel.TryGetPropertyValue("default_path", out var path) && path != null &&
            !string.IsNullOrWhiteSpace(path.ToString()))
            return path.ToString().Trim().Replace('\\', '/').TrimStart('/');
        return null;
    }

    private static string? ResolveIconPath(string rootDir, JsonObject node)
    {
        foreach (var key in new[] { "action", "browser_action", "page_action" })
            if (node.TryGetPropertyValue(key, out var actionNode) && actionNode is JsonObject action &&
                action.TryGetPropertyValue("default_icon", out var actionIcon))
            {
                var icon = ResolveIconNode(rootDir, actionIcon);
                if (icon != null) return icon;
            }
        return node.TryGetPropertyValue("icons", out var icons) ? ResolveIconNode(rootDir, icons) : null;
    }

    private static string? ResolveIconNode(string rootDir, JsonNode? iconNode)
    {
        if (iconNode == null) return null;
        if (iconNode is JsonObject obj)
        {
            foreach (var size in new[] { "128", "64", "48", "32", "24", "19", "16" })
                if (obj.TryGetPropertyValue(size, out var value) && value != null &&
                    ResolveSafeManifestFile(rootDir, value.ToString()) is { } full)
                    return full;
            foreach (var value in obj.Select(x => x.Value?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                if (ResolveSafeManifestFile(rootDir, value!) is { } full) return full;
            return null;
        }
        return ResolveSafeManifestFile(rootDir, iconNode.ToString());
    }

    private static string? ResolveSafeManifestFile(string rootDir, string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            relativePath = Uri.UnescapeDataString(relativePath.Trim())
                .Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(rootDir);
            var full = Path.GetFullPath(Path.Combine(root, relativePath));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
        }
        catch { return null; }
    }

    private static string? ResolveLocaleString(string rootDir, string msgKey, JsonObject manifest)
    {
        try
        {
            var keyName = msgKey.Replace("__MSG_", string.Empty).Replace("__", string.Empty);
            var localesDir = Path.Combine(rootDir, "_locales");
            if (!Directory.Exists(localesDir)) return null;

            var candidates = new List<string>();
            var defaultLocale = GetJsonString(manifest, "default_locale");
            if (!string.IsNullOrWhiteSpace(defaultLocale)) candidates.Add(defaultLocale);
            var ui = LanguageManager.Instance.CurrentLanguage?.Code ?? AppSettings.Global.DisplayLanguage;
            candidates.Add(ui.Replace('-', '_'));
            candidates.Add(ui.Split('-')[0]);
            candidates.AddRange(new[] { "en_US", "en" });

            var dirs = Directory.GetDirectories(localesDir);
            foreach (var locale in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = dirs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), locale, StringComparison.OrdinalIgnoreCase));
                if (dir != null && ReadLocaleMessage(dir, keyName) is { } value) return value;
            }
            foreach (var dir in dirs)
                if (ReadLocaleMessage(dir, keyName) is { } value) return value;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Extensions", ex, $"Resolving extension locale message '{msgKey}'.");
        }
        return null;
    }

    private static string? ReadLocaleMessage(string localeDir, string keyName)
    {
        var file = Path.Combine(localeDir, "messages.json");
        if (!File.Exists(file)) return null;
        var root = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(file));
        if (root != null && root.TryGetPropertyValue(keyName, out var item) && item is JsonObject obj &&
            obj.TryGetPropertyValue("message", out var message) && message != null)
            return message.ToString();
        return null;
    }

    private static bool TryToInt(object? value, out int number)
    {
        switch (value)
        {
            case int i: number = i; return true;
            case long l when l is >= int.MinValue and <= int.MaxValue: number = (int)l; return true;
            case double d when d is >= int.MinValue and <= int.MaxValue: number = (int)d; return true;
            default: number = 0; return false;
        }
    }

    private static bool HasAnyValue(object? value)
    {
        if (value is string text) return !string.IsNullOrWhiteSpace(text);
        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            try { return enumerator.MoveNext(); }
            finally { (enumerator as IDisposable)?.Dispose(); }
        }
        return false;
    }

    private void NotifyExtensionsChanged()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(new Action(() => ExtensionsChanged?.Invoke(this, EventArgs.Empty)));
            return;
        }
        ExtensionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
