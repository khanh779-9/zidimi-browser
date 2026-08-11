using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

public sealed class ExtensionService
{
    private static readonly Lazy<ExtensionService> _instance = new(() => new ExtensionService());
    public static ExtensionService Instance => _instance.Value;

    private ExtensionService() { }

    public IEnumerable<ExtensionInfo> InstalledExtensions => AppSettings.Profile.Extensions;

    public void LoadProfileExtensions(IRequestContext? context)
    {
        if (context == null || context.IsDisposed) return;
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

        try
        {
            var crxUrl = $"https://clients2.google.com/service/update2/crx?response=redirect&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64&prod=chromecrx&prodversion=130.0&acceptformat=crx2,crx3&x=id%3D{extId}%26uc";
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

            var crxBytes = await http.GetByteArrayAsync(crxUrl);
            if (crxBytes == null || crxBytes.Length == 0)
            {
                return (false, LanguageManager.Instance["Ext_DownloadFailed"], null);
            }

            var destDir = Path.Combine(UserDataPaths.ProfileDir(AppSettings.Global.CurrentProfile), "Extensions", extId);
            if (Directory.Exists(destDir))
            {
                try { Directory.Delete(destDir, true); } catch { }
            }

            if (!UnpackCrx(crxBytes, destDir))
            {
                return (false, LanguageManager.Instance["Ext_UnpackFailed"], null);
            }

            return LoadUnpackedExtension(destDir, context);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    private static bool UnpackCrx(byte[] crxBytes, string destinationDir)
    {
        int zipOffset = -1;
        for (int i = 0; i < crxBytes.Length - 4; i++)
        {
            if (crxBytes[i] == 0x50 && crxBytes[i + 1] == 0x4B && crxBytes[i + 2] == 0x03 && crxBytes[i + 3] == 0x04)
            {
                zipOffset = i;
                break;
            }
        }

        if (zipOffset < 0) return false;

        Directory.CreateDirectory(destinationDir);
        using var ms = new MemoryStream(crxBytes, zipOffset, crxBytes.Length - zipOffset);
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!fullPath.StartsWith(Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
                continue; // Guard against zip slip path traversal

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
            }
            else
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (dir != null) Directory.CreateDirectory(dir);
                using var entryStream = entry.Open();
                using var fileStream = File.Create(fullPath);
                entryStream.CopyTo(fileStream);
            }
        }
        return true;
    }

    public (bool success, string message, ExtensionInfo? ext) LoadUnpackedExtension(string folderPath, IRequestContext? context)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return (false, LanguageManager.Instance["Ext_InvalidManifest"], null);
        }

        var manifestPath = Path.Combine(folderPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return (false, LanguageManager.Instance["Ext_InvalidManifest"], null);
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var node = JsonSerializer.Deserialize<JsonObject>(json);
            if (node == null)
            {
                return (false, LanguageManager.Instance["Ext_InvalidManifest"], null);
            }

            string name = GetJsonString(node, "name") ?? Path.GetFileName(folderPath);
            string version = GetJsonString(node, "version") ?? "1.0";
            string description = GetJsonString(node, "description") ?? string.Empty;
            int manifestVersion = node["manifest_version"]?.GetValue<int>() ?? 3;

            // Resolve localization if placeholder (e.g. __MSG_appName__)
            if (name.StartsWith("__MSG_") && name.EndsWith("__"))
            {
                name = ResolveLocaleString(folderPath, name) ?? Path.GetFileName(folderPath);
            }
            if (description.StartsWith("__MSG_") && description.EndsWith("__"))
            {
                description = ResolveLocaleString(folderPath, description) ?? string.Empty;
            }

            string? iconPath = ResolveIconPath(folderPath, node);

            // Generate unique ID based on path
            string id = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(folderPath.ToLowerInvariant()))).Substring(0, 16);

            var existing = AppSettings.Profile.Extensions.FirstOrDefault(e => e.Id == id || string.Equals(e.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Name = name;
                existing.Version = version;
                existing.Description = description;
                existing.IconPath = iconPath;
                existing.ManifestVersion = manifestVersion;
                existing.IsEnabled = true;
            }
            else
            {
                existing = new ExtensionInfo
                {
                    Id = id,
                    Name = name,
                    Version = version,
                    Description = description,
                    Path = folderPath,
                    IconPath = iconPath,
                    ManifestVersion = manifestVersion,
                    IsEnabled = true
                };
                AppSettings.Profile.Extensions.Add(existing);
            }

            AppSettings.SaveProfile();

            return (true, LanguageManager.Instance["Ext_LoadedSuccess"], existing);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public void ToggleExtension(ExtensionInfo ext, bool enable, IRequestContext? context)
    {
        ext.IsEnabled = enable;
        AppSettings.SaveProfile();
    }

    public void RemoveExtension(ExtensionInfo ext, IRequestContext? context)
    {
        AppSettings.Profile.Extensions.RemoveAll(e => e.Id == ext.Id);
        AppSettings.SaveProfile();
    }

    private static string? GetJsonString(JsonObject node, string key)
    {
        if (node.TryGetPropertyValue(key, out var val) && val != null)
        {
            return val.ToString();
        }
        return null;
    }

    private static string? ResolveIconPath(string rootDir, JsonObject node)
    {
        if (!node.TryGetPropertyValue("icons", out var iconsNode) || iconsNode is not JsonObject iconsObj)
        {
            if (node.TryGetPropertyValue("action", out var actionNode) && actionNode is JsonObject actionObj)
            {
                if (actionObj.TryGetPropertyValue("default_icon", out var actIcon))
                {
                    if (actIcon is JsonObject actIconObj)
                    {
                        var first = actIconObj.Select(kv => kv.Value?.ToString()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
                        if (first != null && File.Exists(Path.Combine(rootDir, first)))
                            return Path.Combine(rootDir, first);
                    }
                    else if (actIcon != null)
                    {
                        var str = actIcon.ToString();
                        if (File.Exists(Path.Combine(rootDir, str)))
                            return Path.Combine(rootDir, str);
                    }
                }
            }
            return null;
        }

        // Try largest sizes first (128, 64, 48, 32, 16)
        foreach (var size in new[] { "128", "64", "48", "32", "16" })
        {
            if (iconsObj.TryGetPropertyValue(size, out var pathNode) && pathNode != null)
            {
                var relPath = pathNode.ToString();
                var full = Path.Combine(rootDir, relPath);
                if (File.Exists(full)) return full;
            }
        }

        var any = iconsObj.Select(kv => kv.Value?.ToString()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        if (any != null)
        {
            var full = Path.Combine(rootDir, any);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private static string? ResolveLocaleString(string rootDir, string msgKey)
    {
        try
        {
            var keyName = msgKey.Replace("__MSG_", "").Replace("__", "");
            var localesDir = Path.Combine(rootDir, "_locales");
            if (!Directory.Exists(localesDir)) return null;

            // Try default_locale or vi/en
            var targetLocaleDir = Directory.GetDirectories(localesDir).FirstOrDefault();
            if (targetLocaleDir == null) return null;

            var msgFile = Path.Combine(targetLocaleDir, "messages.json");
            if (!File.Exists(msgFile)) return null;

            var json = File.ReadAllText(msgFile);
            var root = JsonSerializer.Deserialize<JsonObject>(json);
            if (root != null && root.TryGetPropertyValue(keyName, out var itemNode) && itemNode is JsonObject itemObj)
            {
                if (itemObj.TryGetPropertyValue("message", out var msgVal) && msgVal != null)
                {
                    return msgVal.ToString();
                }
            }
        }
        catch { }
        return null;
    }

}

