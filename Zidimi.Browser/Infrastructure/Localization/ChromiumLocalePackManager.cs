using Zidimi.Browser.Infrastructure;
using System.Text;

namespace Zidimi.Browser.Infrastructure.Localization;

/// <summary>
/// Adds Zidimi's WPF strings to the same Chromium locales/*.pak DataPacks that CEF loads.
/// No language/*.lng file and no locales/zidimi sidecar tree is created.
/// </summary>
internal static class ChromiumLocalePackManager
{
    private const string ManifestPrefix = "ZIDIMI_LOCALE_MANIFEST_V1\n";
    private static readonly object Gate = new();
    private static Dictionary<string, ushort>? _resourceIds;
    private static ushort _manifestId;
    private static bool _initialized;

    public static string LocalesDirectory => Path.Combine(AppContext.BaseDirectory, "locales");

    public static void EnsureMerged()
    {
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var available = ZidimiLocaleCatalog.Locales.Values
                    .Select(def => (Definition: def, Path: Path.Combine(LocalesDirectory, def.PackName)))
                    .Where(x => File.Exists(x.Path))
                    .ToArray();

                if (available.Length == 0)
                {
                    AppLogger.Log("LocalePack", $"No Chromium locale packs found in '{LocalesDirectory}'. Using compiled bootstrap fallback.");
                    return;
                }

                var packs = available.ToDictionary(x => x.Definition.Code, x => ChromiumDataPack.Read(x.Path), StringComparer.OrdinalIgnoreCase);
                if (!TryReadManifest(packs.Values, out var manifestId, out var ids) || HasCollision(packs, manifestId, ids))
                {
                    RemoveRecognizedOldZidimiResources(packs, manifestId, ids);
                    AllocateIds(packs.Values, out manifestId, out ids);
                }

                _manifestId = manifestId;
                _resourceIds = ids;
                var manifest = BuildManifest(manifestId, ids);

                foreach (var item in available)
                {
                    var pack = packs[item.Definition.Code];
                    pack.SetUtf8(manifestId, manifest);
                    foreach (var key in ZidimiLocaleCatalog.Keys)
                    {
                        var localized = item.Definition.Strings.TryGetValue(key, out var value)
                            ? value
                            : ZidimiLocaleCatalog.English.Strings.TryGetValue(key, out var fallback)
                                ? fallback
                                : key;
                        pack.SetUtf8(ids[key], localized);
                    }
                    ChromiumDataPack.WriteAtomic(item.Path, pack.ToBytes());
                }

                AppLogger.Log("LocalePack", $"Merged {ids.Count} Zidimi strings into {available.Length} Chromium locales/*.pak packs.");
            }
            catch (Exception ex)
            {
                _resourceIds = null;
                AppLogger.Log("LocalePack", ex, "Merging Zidimi UI strings into Chromium locale DataPacks. Compiled fallback remains available.");
            }
        }
    }

    public static IReadOnlyList<LanguageInfo> GetLanguages()
    {
        EnsureMerged();
        return ZidimiLocaleCatalog.Locales.Values
            .Select(def =>
            {
                var strings = LoadLanguage(def.Code);
                var displayKey = def.Code switch
                {
                    "vi-VN" => "Language_Vietnamese",
                    "fr-FR" => "Language_French",
                    "de-DE" => "Language_German",
                    "it-IT" => "Language_Italian",
                    "ru-RU" => "Language_Russian",
                    "zh-CN" => "Language_ChineseSimplified",
                    _ => "Language_English",
                };
                return new LanguageInfo
                {
                    Code = def.Code,
                    Name = strings.TryGetValue(displayKey, out var name) ? name : def.DisplayName,
                    FilePath = Path.Combine(LocalesDirectory, def.PackName),
                };
            })
            .ToArray();
    }

    public static Dictionary<string, string> LoadLanguage(string code)
    {
        EnsureMerged();
        if (!ZidimiLocaleCatalog.Locales.TryGetValue(code, out var definition))
            definition = ZidimiLocaleCatalog.English;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(LocalesDirectory, definition.PackName);
        if (_resourceIds != null && File.Exists(path))
        {
            try
            {
                var pack = ChromiumDataPack.Read(path);
                foreach (var key in ZidimiLocaleCatalog.Keys)
                {
                    if (_resourceIds.TryGetValue(key, out var id) && pack.Resources.TryGetValue(id, out var raw))
                    {
                        // Zidimi resources are explicitly written as UTF-8 regardless of the stock pack encoding.
                        result[key] = Encoding.UTF8.GetString(raw).TrimEnd('\0');
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("LocalePack", ex, $"Reading Chromium locale pack '{definition.PackName}'.");
            }
        }

        // Fallback is compiled only for resilience when a deployment has missing/read-only/corrupt CEF packs.
        foreach (var key in ZidimiLocaleCatalog.Keys)
        {
            if (result.ContainsKey(key)) continue;
            if (definition.Strings.TryGetValue(key, out var localized)) result[key] = localized;
            else if (ZidimiLocaleCatalog.English.Strings.TryGetValue(key, out var english)) result[key] = english;
            else result[key] = key;
        }
        return result;
    }

    private static bool TryReadManifest(IEnumerable<ChromiumDataPack> packs, out ushort manifestId, out Dictionary<string, ushort> ids)
    {
        manifestId = 0;
        ids = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in packs)
        {
            foreach (var (id, raw) in pack.Resources)
            {
                string text;
                try { text = Encoding.UTF8.GetString(raw); } catch { continue; }
                if (!text.StartsWith(ManifestPrefix, StringComparison.Ordinal)) continue;
                if (!TryParseManifest(text, out ids)) continue;
                manifestId = id;
                return ids.Count == ZidimiLocaleCatalog.Keys.Count;
            }
        }
        return false;
    }

    private static bool TryParseManifest(string text, out Dictionary<string, ushort> ids)
    {
        ids = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n').Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("manifest=", StringComparison.Ordinal)) continue;
            var split = line.IndexOf('=');
            if (split <= 0 || !ushort.TryParse(line[(split + 1)..], out var id)) return false;
            ids[line[..split]] = id;
        }
        return ZidimiLocaleCatalog.Keys.All(ids.ContainsKey);
    }

    private static string BuildManifest(ushort manifestId, IReadOnlyDictionary<string, ushort> ids)
    {
        var sb = new StringBuilder(ManifestPrefix);
        sb.Append("manifest=").Append(manifestId).Append('\n');
        foreach (var key in ZidimiLocaleCatalog.Keys)
            sb.Append(key).Append('=').Append(ids[key]).Append('\n');
        return sb.ToString();
    }

    private static bool HasCollision(
        IReadOnlyDictionary<string, ChromiumDataPack> packs,
        ushort manifestId,
        IReadOnlyDictionary<string, ushort> ids)
    {
        if (manifestId == 0 || ids.Count != ZidimiLocaleCatalog.Keys.Count) return true;
        foreach (var (code, pack) in packs)
        {
            var definition = ZidimiLocaleCatalog.Locales[code];
            if (pack.Resources.TryGetValue(manifestId, out var manifestRaw) &&
                !Encoding.UTF8.GetString(manifestRaw).StartsWith(ManifestPrefix, StringComparison.Ordinal))
                return true;

            foreach (var key in ZidimiLocaleCatalog.Keys)
            {
                if (!pack.Resources.TryGetValue(ids[key], out var raw)) continue;
                var expected = definition.Strings.TryGetValue(key, out var localized)
                    ? localized
                    : ZidimiLocaleCatalog.English.Strings.TryGetValue(key, out var fallback) ? fallback : key;
                if (!raw.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected))) return true;
            }
        }
        return false;
    }

    private static void RemoveRecognizedOldZidimiResources(
        IReadOnlyDictionary<string, ChromiumDataPack> packs,
        ushort manifestId,
        IReadOnlyDictionary<string, ushort> ids)
    {
        if (manifestId == 0 || ids.Count == 0) return;
        foreach (var (code, pack) in packs)
        {
            if (pack.Resources.TryGetValue(manifestId, out var manifestRaw) &&
                Encoding.UTF8.GetString(manifestRaw).StartsWith(ManifestPrefix, StringComparison.Ordinal))
                pack.Resources.Remove(manifestId);

            var definition = ZidimiLocaleCatalog.Locales[code];
            foreach (var (key, id) in ids)
            {
                if (!pack.Resources.TryGetValue(id, out var raw)) continue;
                var expected = definition.Strings.TryGetValue(key, out var localized)
                    ? localized
                    : ZidimiLocaleCatalog.English.Strings.TryGetValue(key, out var fallback) ? fallback : key;
                if (raw.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected))) pack.Resources.Remove(id);
            }
        }
    }

    private static void AllocateIds(IEnumerable<ChromiumDataPack> packs, out ushort manifestId, out Dictionary<string, ushort> ids)
    {
        var used = new HashSet<ushort>(packs.SelectMany(p => p.Resources.Keys));
        var required = ZidimiLocaleCatalog.Keys.Count + 1;
        var free = new List<ushort>(required);
        for (var candidate = (int)ushort.MaxValue; candidate >= 1 && free.Count < required; candidate--)
        {
            var id = checked((ushort)candidate);
            if (!used.Contains(id)) free.Add(id);
        }
        if (free.Count < required)
            throw new InvalidDataException("Chromium locale packs do not have enough free uint16 resource IDs for Zidimi UI strings.");

        manifestId = free[0];
        ids = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ZidimiLocaleCatalog.Keys.Count; i++) ids[ZidimiLocaleCatalog.Keys[i]] = free[i + 1];
    }
}
