using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Native Chromium Bookmarks reader. Zidimi does not create bookmarks.json of its own.
///
/// CEF currently does not expose Chromium's full BookmarkModel mutation API. Zidimi therefore
/// treats the native Bookmarks file as read-only metadata for the omnibox/toolbar and sends bookmark
/// management to Chromium's own <c>chrome://bookmarks</c> UI instead of editing the JSON itself.
/// </summary>
public sealed class BookmarkService : IDisposable
{
    private string _profileName = AppSettings.Global.CurrentProfile;
    private int _loadGeneration;
    private readonly HashSet<string> _urls = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<Bookmark> Items { get; } = new();

    public Task InitializeAsync() => LoadAsync(_profileName);

    public void SwitchProfile(string profileName)
    {
        var profileId = UserDataPaths.NormalizeProfileId(profileName);
        if (string.Equals(profileId, _profileName, StringComparison.OrdinalIgnoreCase)) return;
        _profileName = profileId;
        Interlocked.Increment(ref _loadGeneration);
        Application.Current?.Dispatcher.Invoke(ClearMemory);
        _ = LoadAsync(profileId);
    }

    private async Task LoadAsync(string profileId)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            var list = await Task.Run(() => ReadNativeBookmarks(UserDataPaths.BookmarksFile(profileId))).ConfigureAwait(false);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            await dispatcher.InvokeAsync(() =>
            {
                if (generation != _loadGeneration ||
                    !string.Equals(profileId, _profileName, StringComparison.OrdinalIgnoreCase))
                    return;
                ClearMemory();
                foreach (var item in list)
                    if (_urls.Add(item.Url)) Items.Add(item);
            });
        }
        catch (Exception ex)
        {
            AppLogger.Log("Bookmarks", ex, $"Reading native Chromium Bookmarks for '{profileId}'.");
        }
    }

    private static List<Bookmark> ReadNativeBookmarks(string path)
    {
        var items = new List<Bookmark>();
        if (!File.Exists(path)) return items;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            if (root?["roots"] is not JsonObject roots) return items;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, node) in roots)
                Extract(node, seen, items);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Bookmarks", ex, "Parsing Chromium Bookmarks.");
        }
        return items;
    }

    private static void Extract(JsonNode? node, ISet<string> seen, ICollection<Bookmark> output)
    {
        if (node is not JsonObject obj) return;
        var type = obj["type"]?.GetValue<string>();
        if (string.Equals(type, "url", StringComparison.OrdinalIgnoreCase))
        {
            var url = obj["url"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(url) || !seen.Add(url)) return;
            var title = obj["name"]?.GetValue<string>();
            output.Add(new Bookmark
            {
                Url = url,
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                CreatedAt = ParseChromeBookmarkTime(obj["date_added"]?.GetValue<string>()),
            });
            return;
        }
        if (obj["children"] is JsonArray children)
            foreach (var child in children) Extract(child, seen, output);
    }

    private static DateTime ParseChromeBookmarkTime(string? value)
        => long.TryParse(value, out var micros) ? SqliteHelper.FromChromeTime(micros) : DateTime.Now;

    private void ClearMemory()
    {
        Items.Clear();
        _urls.Clear();
    }

    public void Dispose()
    {
        // Invalidate any read-only Chromium Bookmarks load still running in the background so it
        // cannot repopulate the WPF collection after the owning browser window has been disposed.
        Interlocked.Increment(ref _loadGeneration);
    }
}
