using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.DevTools;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

public sealed class ExtensionService
{
    private static readonly Lazy<ExtensionService> _instance = new(() => new ExtensionService());
    public static ExtensionService Instance => _instance.Value;

    private ExtensionService() { }

    private readonly SemaphoreSlim _runtimeGate = new(1, 1);

    public event EventHandler? ExtensionsChanged;

    public IEnumerable<ExtensionInfo> InstalledExtensions => AppSettings.Profile.Extensions;
    public IEnumerable<ExtensionInfo> PinnedExtensions => AppSettings.Profile.Extensions.Where(e => e.IsPinned);

    public void LoadProfileExtensions(IRequestContext? context)
    {
        if (context == null || context.IsDisposed) return;

        // Re-read manifest metadata every time a profile context is opened. Older Zidimi
        // versions saved only name/version and therefore existing installs could have a null
        // IconPath forever until they were reinstalled.
        RefreshStoredMetadata();
    }

    private void NotifyExtensionsChanged()
    {
        ExtensionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshForCurrentProfile()
    {
        RefreshStoredMetadata();
        NotifyExtensionsChanged();
    }

    private void RefreshStoredMetadata()
    {
        var changed = false;
        foreach (var ext in AppSettings.Profile.Extensions.ToList())
        {
            if (string.IsNullOrWhiteSpace(ext.Path) || !Directory.Exists(ext.Path))
                continue;

            if (TryReadManifestMetadata(ext.Path, out var meta))
            {
                changed |= ApplyMetadata(ext, meta, preserveEnabled: true);
            }
        }

        if (changed)
            AppSettings.SaveProfile();
    }

    public static string? ExtractExtensionId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(input.Trim(), @"\b([a-p]{32})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    public async System.Threading.Tasks.Task<(bool success, string message, ExtensionInfo? ext)> DownloadAndInstallFromWebStoreAsync(string input, IRequestContext? context)
    {
        var extId = ExtractExtensionId(input);
        if (string.IsNullOrEmpty(extId))
        {
            return (false, LanguageManager.Instance["Ext_InvalidId"], null);
        }

        string? stagingDir = null;
        string? backupDir = null;
        try
        {
            var crxUrl = IsTrustedWebStoreCrxUrl(input) ? input : BuildWebStoreCrxUrl(extId);
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(BuildDownloadUserAgent());

            var crxBytes = await http.GetByteArrayAsync(crxUrl).ConfigureAwait(false);
            if (crxBytes.Length == 0)
            {
                return (false, LanguageManager.Instance["Ext_DownloadFailed"], null);
            }

            var extensionRoot = Path.Combine(
                UserDataPaths.ProfileDir(AppSettings.Global.CurrentProfile), "Extensions");
            Directory.CreateDirectory(extensionRoot);

            var destDir = Path.Combine(extensionRoot, extId);
            stagingDir = Path.Combine(extensionRoot, $".{extId}.{Guid.NewGuid():N}.tmp");

            if (!UnpackCrx(crxBytes, stagingDir) || !HasValidManifest(stagingDir))
            {
                return (false, LanguageManager.Instance["Ext_UnpackFailed"], null);
            }

            // Swap only after the new package is fully validated. If moving the new
            // directory fails, restore the old installation instead of leaving the
            // extension missing or half-updated.
            if (Directory.Exists(destDir))
            {
                backupDir = Path.Combine(extensionRoot, $".{extId}.{Guid.NewGuid():N}.bak");
                Directory.Move(destDir, backupDir);
            }

            try
            {
                Directory.Move(stagingDir, destDir);
                stagingDir = null;
            }
            catch
            {
                if (!Directory.Exists(destDir) && !string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
                {
                    Directory.Move(backupDir, destDir);
                    backupDir = null;
                }
                throw;
            }

            if (!string.IsNullOrEmpty(backupDir) && Directory.Exists(backupDir))
            {
                try
                {
                    Directory.Delete(backupDir, recursive: true);
                    backupDir = null;
                }
                catch (Exception cleanupEx)
                {
                    AppLogger.Log("ExtensionInstall", cleanupEx, "Could not remove extension backup directory.");
                }
            }

            return LoadUnpackedExtension(destDir, context);
        }
        catch (HttpRequestException)
        {
            return (false, LanguageManager.Instance["Ext_DownloadFailed"], null);
        }
        catch (TaskCanceledException)
        {
            return (false, LanguageManager.Instance["Ext_DownloadFailed"], null);
        }
        catch (InvalidDataException)
        {
            return (false, LanguageManager.Instance["Ext_UnpackFailed"], null);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionInstall", ex);
            return (false, ex.Message, null);
        }
        finally
        {
            if (!string.IsNullOrEmpty(stagingDir) && Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, recursive: true); } catch { }
            }
        }
    }

    private static bool IsTrustedWebStoreCrxUrl(string input)
    {
        return Uri.TryCreate(input, UriKind.Absolute, out var uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals("clients2.google.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Equals("/service/update2/crx", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildWebStoreCrxUrl(string extId)
    {
        var chromiumVersion = Cef.IsInitialized == true && !string.IsNullOrWhiteSpace(Cef.ChromiumVersion)
            ? Cef.ChromiumVersion
            : "150.0.0.0";
        const string arch = "x86";
        const string osArch = "x86";
        const string naclArch = "x86-32";
        var x = Uri.EscapeDataString($"id={extId}&uc");

        return "https://clients2.google.com/service/update2/crx"
            + $"?response=redirect&os=win&arch={arch}&os_arch={osArch}&nacl_arch={naclArch}"
            + $"&prod=chromecrx&prodversion={Uri.EscapeDataString(chromiumVersion)}"
            + $"&acceptformat=crx2,crx3&x={x}";
    }

    private static string BuildDownloadUserAgent()
    {
        var chromiumVersion = Cef.IsInitialized == true && !string.IsNullOrWhiteSpace(Cef.ChromiumVersion)
            ? Cef.ChromiumVersion
            : "150.0.0.0";
        const string platform = "Win32; x86";
        return $"Mozilla/5.0 (Windows NT 10.0; {platform}) AppleWebKit/537.36 "
            + $"(KHTML, like Gecko) Chrome/{chromiumVersion} Safari/537.36";
    }


    private static bool HasValidManifest(string extensionDir)
    {
        try
        {
            var manifestPath = Path.Combine(extensionDir, "manifest.json");
            if (!File.Exists(manifestPath)) return false;

            var manifest = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(manifestPath));
            return manifest != null
                && manifest["manifest_version"] != null
                && manifest["name"] != null
                && manifest["version"] != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool UnpackCrx(byte[] crxBytes, string destinationDir)
    {
        if (!TryGetCrxZipOffset(crxBytes, out var zipOffset)) return false;

        var destinationRoot = Path.GetFullPath(destinationDir);
        Directory.CreateDirectory(destinationRoot);
        var destinationPrefix = destinationRoot.EndsWith(Path.DirectorySeparatorChar)
            ? destinationRoot
            : destinationRoot + Path.DirectorySeparatorChar;

        using var ms = new MemoryStream(crxBytes, zipOffset, crxBytes.Length - zipOffset, writable: false);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!fullPath.Equals(destinationRoot, StringComparison.OrdinalIgnoreCase)
                && !fullPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Extension archive contains an invalid path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(fileStream);
        }

        return true;
    }

    private static bool TryGetCrxZipOffset(ReadOnlySpan<byte> crxBytes, out int zipOffset)
    {
        zipOffset = 0;
        if (crxBytes.Length < 12
            || crxBytes[0] != (byte)'C'
            || crxBytes[1] != (byte)'r'
            || crxBytes[2] != (byte)'2'
            || crxBytes[3] != (byte)'4')
        {
            return false;
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(crxBytes.Slice(4, 4));
        long offset;

        if (version == 2)
        {
            if (crxBytes.Length < 16) return false;
            var publicKeyLength = BinaryPrimitives.ReadUInt32LittleEndian(crxBytes.Slice(8, 4));
            var signatureLength = BinaryPrimitives.ReadUInt32LittleEndian(crxBytes.Slice(12, 4));
            offset = 16L + publicKeyLength + signatureLength;
        }
        else if (version == 3)
        {
            var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(crxBytes.Slice(8, 4));
            offset = 12L + headerLength;
        }
        else
        {
            return false;
        }

        if (offset < 0 || offset > int.MaxValue || offset + 4 > crxBytes.Length) return false;

        zipOffset = (int)offset;
        return crxBytes[zipOffset] == 0x50
            && crxBytes[zipOffset + 1] == 0x4B
            && crxBytes[zipOffset + 2] == 0x03
            && crxBytes[zipOffset + 3] == 0x04;
    }

    public (bool success, string message, ExtensionInfo? ext) LoadUnpackedExtension(string folderPath, IRequestContext? context)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return (false, LanguageManager.Instance["Ext_InvalidManifest"], null);

        try
        {
            folderPath = Path.GetFullPath(folderPath);
            if (!TryReadManifestMetadata(folderPath, out var meta))
                return (false, LanguageManager.Instance["Ext_InvalidManifest"], null);

            // Keep the old metadata id stable so existing pin state/settings continue to work.
            string id = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(
                System.Text.Encoding.UTF8.GetBytes(folderPath.ToLowerInvariant()))).Substring(0, 16);

            var existing = AppSettings.Profile.Extensions.FirstOrDefault(e =>
                e.Id == id || string.Equals(e.Path, folderPath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new ExtensionInfo
                {
                    Id = id,
                    Path = folderPath,
                    IsEnabled = true
                };
                AppSettings.Profile.Extensions.Add(existing);
            }

            ApplyMetadata(existing, meta, preserveEnabled: true);
            existing.Path = folderPath;

            // Web Store installs are stored in a directory whose name is the 32-char store id.
            // Keep it as a hint until Chromium returns the real runtime id from loadUnpacked.
            var folderId = ExtractExtensionId(Path.GetFileName(folderPath));
            if (string.IsNullOrWhiteSpace(existing.RuntimeId) && !string.IsNullOrWhiteSpace(folderId))
                existing.RuntimeId = folderId;

            AppSettings.SaveProfile();
            NotifyExtensionsChanged();
            return (true, LanguageManager.Instance["Ext_LoadedSuccess"], existing);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionManifest", ex);
            return (false, ex.Message, null);
        }
    }

    /// <summary>
    /// Load every enabled extension into Chromium's real extension runtime. The caller must be a
    /// Chrome-style HwndHost/WinForms browser that has already created its IBrowser.
    /// </summary>
    public async Task EnsureProfileRuntimeLoadedAsync(IChromiumWebBrowserBase browser)
    {
        if (browser == null || browser.IsDisposed || browser.BrowserCore == null)
            return;

        RefreshStoredMetadata();
        foreach (var ext in AppSettings.Profile.Extensions.Where(x => x.IsEnabled).ToList())
            await EnsureExtensionRuntimeLoadedAsync(ext, browser).ConfigureAwait(false);
    }

    public async Task<(bool success, string message, string? runtimeId)> EnsureExtensionRuntimeLoadedAsync(
        ExtensionInfo ext, IChromiumWebBrowserBase browser)
    {
        if (ext == null || !ext.IsEnabled || string.IsNullOrWhiteSpace(ext.Path) || !Directory.Exists(ext.Path))
            return (false, "Extension path is invalid or disabled.", null);
        if (browser == null || browser.IsDisposed || browser.BrowserCore == null)
            return (false, "Chromium browser is not initialized.", null);

        await _runtimeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var client = browser.BrowserCore.GetDevToolsClient();
            var normalizedPath = NormalizePath(ext.Path);

            // Avoid loading the same unpacked extension once per tab.
            var listed = await client.ExecuteDevToolsMethodAsync("Extensions.getExtensions").ConfigureAwait(false);
            if (listed.Success && !string.IsNullOrWhiteSpace(listed.ResponseAsJsonString))
            {
                using var doc = JsonDocument.Parse(listed.ResponseAsJsonString);
                if (doc.RootElement.TryGetProperty("extensions", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var path = item.TryGetProperty("path", out var p) ? p.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(path) && string.Equals(NormalizePath(path), normalizedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            var id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                SaveRuntimeId(ext, id);
                                return (true, string.Empty, id);
                            }
                        }
                    }
                }
            }

            var parameters = new Dictionary<string, object>
            {
                ["path"] = Path.GetFullPath(ext.Path),
                ["enableInIncognito"] = false
            };
            var result = await client.ExecuteDevToolsMethodAsync("Extensions.loadUnpacked", parameters).ConfigureAwait(false);
            if (!result.Success || string.IsNullOrWhiteSpace(result.ResponseAsJsonString))
                return (false, "Chromium did not load the extension.", null);

            using (var doc = JsonDocument.Parse(result.ResponseAsJsonString))
            {
                var id = doc.RootElement.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(id))
                    return (false, "Chromium loaded the extension but returned no runtime id.", null);

                SaveRuntimeId(ext, id);
                AppLogger.Log("ExtensionRuntime", $"Loaded {ext.Name} ({id}) from {ext.Path}");
                return (true, string.Empty, id);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionRuntime", ex, $"Unable to load {ext.Name}.");
            return (false, ex.Message, null);
        }
        finally
        {
            _runtimeGate.Release();
        }
    }

    /// <summary>Equivalent to clicking the extension action in Chromium's own toolbar.</summary>
    public async Task<(bool success, string message)> TriggerDefaultActionAsync(
        ExtensionInfo ext, IChromiumWebBrowserBase browser)
    {
        var loaded = await EnsureExtensionRuntimeLoadedAsync(ext, browser).ConfigureAwait(false);
        if (!loaded.success || string.IsNullOrWhiteSpace(loaded.runtimeId))
            return (false, loaded.message);

        try
        {
            using var client = browser.BrowserCore!.GetDevToolsClient();
            var target = await client.ExecuteDevToolsMethodAsync("Target.getTargetInfo").ConfigureAwait(false);
            if (!target.Success || string.IsNullOrWhiteSpace(target.ResponseAsJsonString))
                return (false, "Unable to resolve the current Chromium tab target.");

            string? targetId = null;
            using (var doc = JsonDocument.Parse(target.ResponseAsJsonString))
            {
                if (doc.RootElement.TryGetProperty("targetInfo", out var info) &&
                    info.TryGetProperty("targetId", out var idNode))
                    targetId = idNode.GetString();
            }

            if (string.IsNullOrWhiteSpace(targetId))
                return (false, "Unable to resolve the current Chromium tab target.");

            var args = new Dictionary<string, object>
            {
                ["id"] = loaded.runtimeId,
                ["targetId"] = targetId
            };
            var response = await client.ExecuteDevToolsMethodAsync("Extensions.triggerAction", args).ConfigureAwait(false);
            return response.Success
                ? (true, string.Empty)
                : (false, "Chromium rejected the extension action.");
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionAction", ex, $"Unable to trigger {ext.Name}.");
            return (false, ex.Message);
        }
    }

    public void ToggleExtension(ExtensionInfo ext, bool enable, IRequestContext? context)
    {
        ext.IsEnabled = enable;
        AppSettings.SaveProfile();
        NotifyExtensionsChanged();
    }

    public void TogglePinned(ExtensionInfo ext, bool pin)
    {
        ext.IsPinned = pin;
        AppSettings.SaveProfile();
        NotifyExtensionsChanged();
    }

    public void RemoveExtension(ExtensionInfo ext, IRequestContext? context)
    {
        AppSettings.Profile.Extensions.RemoveAll(e => e.Id == ext.Id);
        AppSettings.SaveProfile();
        NotifyExtensionsChanged();
    }

    private static void SaveRuntimeId(ExtensionInfo ext, string runtimeId)
    {
        if (string.Equals(ext.RuntimeId, runtimeId, StringComparison.Ordinal)) return;
        ext.RuntimeId = runtimeId;
        AppSettings.SaveProfile();
        Instance.NotifyExtensionsChanged();
    }

    private sealed record ManifestMetadata(
        string Name, string Version, string Description, int ManifestVersion,
        string? IconPath, string? PopupPath);

    private static bool TryReadManifestMetadata(string folderPath, out ManifestMetadata metadata)
    {
        metadata = null!;
        try
        {
            var manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath)) return false;
            var node = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(manifestPath));
            if (node == null) return false;

            string name = GetJsonString(node, "name") ?? Path.GetFileName(folderPath);
            string version = GetJsonString(node, "version") ?? "1.0";
            string description = GetJsonString(node, "description") ?? string.Empty;
            int manifestVersion = node["manifest_version"]?.GetValue<int>() ?? 3;

            if (name.StartsWith("__MSG_", StringComparison.Ordinal) && name.EndsWith("__", StringComparison.Ordinal))
                name = ResolveLocaleString(folderPath, name, node) ?? Path.GetFileName(folderPath);
            if (description.StartsWith("__MSG_", StringComparison.Ordinal) && description.EndsWith("__", StringComparison.Ordinal))
                description = ResolveLocaleString(folderPath, description, node) ?? string.Empty;

            metadata = new ManifestMetadata(
                name, version, description, manifestVersion,
                ResolveIconPath(folderPath, node),
                ResolvePopupPath(node));
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionManifest", ex, folderPath);
            return false;
        }
    }

    private static bool ApplyMetadata(ExtensionInfo ext, ManifestMetadata meta, bool preserveEnabled)
    {
        var changed = false;
        changed |= SetIfDifferent(ext.Name, meta.Name, v => ext.Name = v);
        changed |= SetIfDifferent(ext.Version, meta.Version, v => ext.Version = v);
        changed |= SetIfDifferent(ext.Description, meta.Description, v => ext.Description = v);
        changed |= SetOptionalIfDifferent(ext.IconPath, meta.IconPath, v => ext.IconPath = v);
        changed |= SetOptionalIfDifferent(ext.PopupPath, meta.PopupPath, v => ext.PopupPath = v);
        if (ext.ManifestVersion != meta.ManifestVersion) { ext.ManifestVersion = meta.ManifestVersion; changed = true; }
        if (!preserveEnabled && !ext.IsEnabled) { ext.IsEnabled = true; changed = true; }
        return changed;
    }

    private static bool SetIfDifferent(string oldValue, string newValue, Action<string> setter)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return false;
        setter(newValue);
        return true;
    }

    private static bool SetOptionalIfDifferent(string? oldValue, string? newValue, Action<string?> setter)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return false;
        setter(newValue);
        return true;
    }

    private static string? GetJsonString(JsonObject node, string key)
    {
        if (node.TryGetPropertyValue(key, out var val) && val != null)
            return val.ToString();
        return null;
    }

    private static string? ResolvePopupPath(JsonObject node)
    {
        foreach (var key in new[] { "action", "browser_action", "page_action" })
        {
            if (node.TryGetPropertyValue(key, out var actionNode) && actionNode is JsonObject actionObj &&
                actionObj.TryGetPropertyValue("default_popup", out var popupNode) && popupNode != null)
            {
                var value = popupNode.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(value)) return value.Replace('\\', '/').TrimStart('/');
            }
        }
        return null;
    }

    private static string? ResolveIconPath(string rootDir, JsonObject node)
    {
        // Toolbar/action icon is the correct icon for a pinned extension. Only fall back to
        // the general manifest icon if the extension does not define an action icon.
        foreach (var key in new[] { "action", "browser_action", "page_action" })
        {
            if (node.TryGetPropertyValue(key, out var actionNode) && actionNode is JsonObject actionObj &&
                actionObj.TryGetPropertyValue("default_icon", out var actionIcon))
            {
                var path = ResolveIconNode(rootDir, actionIcon);
                if (path != null) return path;
            }
        }

        if (node.TryGetPropertyValue("icons", out var iconsNode))
            return ResolveIconNode(rootDir, iconsNode);
        return null;
    }

    private static string? ResolveIconNode(string rootDir, JsonNode? iconNode)
    {
        if (iconNode == null) return null;
        if (iconNode is JsonObject obj)
        {
            foreach (var size in new[] { "128", "64", "48", "32", "24", "19", "16" })
            {
                if (obj.TryGetPropertyValue(size, out var value) && value != null)
                {
                    var full = ResolveSafeManifestFile(rootDir, value.ToString());
                    if (full != null) return full;
                }
            }
            foreach (var value in obj.Select(x => x.Value?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var full = ResolveSafeManifestFile(rootDir, value!);
                if (full != null) return full;
            }
            return null;
        }
        return ResolveSafeManifestFile(rootDir, iconNode.ToString());
    }

    private static string? ResolveSafeManifestFile(string rootDir, string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            relativePath = Uri.UnescapeDataString(relativePath.Trim()).Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(rootDir);
            var full = Path.GetFullPath(Path.Combine(root, relativePath));
            var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(full) ? full : null;
        }
        catch { return null; }
    }

    private static string? ResolveLocaleString(string rootDir, string msgKey, JsonObject manifest)
    {
        try
        {
            var keyName = msgKey.Replace("__MSG_", "").Replace("__", "");
            var localesDir = Path.Combine(rootDir, "_locales");
            if (!Directory.Exists(localesDir)) return null;

            var localeNames = new List<string>();
            var defaultLocale = GetJsonString(manifest, "default_locale");
            if (!string.IsNullOrWhiteSpace(defaultLocale)) localeNames.Add(defaultLocale);
            localeNames.AddRange(new[] { "vi", "vi_VN", "en", "en_US" });

            var dirs = Directory.GetDirectories(localesDir);
            foreach (var locale in localeNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = dirs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), locale, StringComparison.OrdinalIgnoreCase));
                var value = dir == null ? null : ReadLocaleMessage(dir, keyName);
                if (value != null) return value;
            }

            foreach (var dir in dirs)
            {
                var value = ReadLocaleMessage(dir, keyName);
                if (value != null) return value;
            }
        }
        catch { }
        return null;
    }

    private static string? ReadLocaleMessage(string localeDir, string keyName)
    {
        var msgFile = Path.Combine(localeDir, "messages.json");
        if (!File.Exists(msgFile)) return null;
        var root = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(msgFile));
        if (root != null && root.TryGetPropertyValue(keyName, out var itemNode) && itemNode is JsonObject itemObj &&
            itemObj.TryGetPropertyValue("message", out var msgVal) && msgVal != null)
            return msgVal.ToString();
        return null;
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return path; }
    }
}
