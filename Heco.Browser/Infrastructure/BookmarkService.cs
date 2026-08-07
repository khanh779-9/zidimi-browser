using System.Collections.ObjectModel;
using Heco.Browser.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;

namespace Heco.Browser.Infrastructure;

/// <summary>Bookmark service with per-profile JSON persistence (User Data\&lt;profile&gt;\Bookmarks — like Chrome).</summary>
public sealed class BookmarkService
{
    private string _profileName = AppSettings.Global.CurrentProfile;

    public ObservableCollection<Bookmark> Items { get; } = new();

    private string DataFile => UserDataPaths.BookmarksFile(_profileName);

    public BookmarkService()
    {
        Load();
    }

    /// <summary>Switch to another profile — reload that profile's bookmarks.</summary>
    public void SwitchProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == _profileName) return;
        _profileName = profileName;
        Application.Current?.Dispatcher.Invoke(Items.Clear);
        Load();
    }

    public void Add(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (Items.Any(b => b.Url == url)) return;
        var bm = new Bookmark { Url = url, Title = string.IsNullOrWhiteSpace(title) ? url : title, CreatedAt = DateTime.Now };
        Application.Current?.Dispatcher.Invoke(() => Items.Add(bm));
        Save();
    }

    public void Remove(Bookmark bm)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (Items.Contains(bm)) Items.Remove(bm);
        });
        Save();
    }

    public bool Contains(string url) => Items.Any(b => b.Url == url);

    private void Load()
    {
        try
        {
            if (!File.Exists(DataFile)) return;
            var json = File.ReadAllText(DataFile);
            var root = JsonNode.Parse(json);
            var roots = root?["roots"];
            if (roots != null)
            {
                ExtractBookmarks(roots["bookmark_bar"]);
                ExtractBookmarks(roots["other"]);
                ExtractBookmarks(roots["synced"]);
            }
        }
        catch { /* ignore corrupted file */ }
    }

    private void ExtractBookmarks(JsonNode? folder)
    {
        var children = folder?["children"] as JsonArray;
        if (children == null) return;
        foreach (var child in children)
        {
            if (child?["type"]?.GetValue<string>() == "url")
            {
                var bm = new Bookmark
                {
                    Url = child["url"]?.GetValue<string>() ?? "",
                    Title = child["name"]?.GetValue<string>() ?? ""
                };
                Items.Add(bm);
            }
            else if (child?["type"]?.GetValue<string>() == "folder")
            {
                ExtractBookmarks(child);
            }
        }
    }

    public void Save()
    {
        try
        {
            UserDataPaths.EnsureProfileDir(_profileName);
            JsonNode? rootNode = null;
            if (File.Exists(DataFile))
            {
                try { rootNode = JsonNode.Parse(File.ReadAllText(DataFile)); } catch { }
            }
            
            if (rootNode == null)
            {
                rootNode = new JsonObject
                {
                    ["version"] = 1,
                    ["roots"] = new JsonObject
                    {
                        ["bookmark_bar"] = new JsonObject
                        {
                            ["id"] = "1",
                            ["name"] = "Bookmarks bar",
                            ["type"] = "folder",
                            ["children"] = new JsonArray()
                        },
                        ["other"] = new JsonObject
                        {
                            ["id"] = "2",
                            ["name"] = "Other bookmarks",
                            ["type"] = "folder",
                            ["children"] = new JsonArray()
                        },
                        ["synced"] = new JsonObject
                        {
                            ["id"] = "3",
                            ["name"] = "Mobile bookmarks",
                            ["type"] = "folder",
                            ["children"] = new JsonArray()
                        }
                    }
                };
            }
            
            // Replace the bookmark_bar children
            var children = new JsonArray();
            int id = 10;
            foreach (var b in Items)
            {
                children.Add(new JsonObject
                {
                    ["id"] = (id++).ToString(),
                    ["name"] = b.Title,
                    ["type"] = "url",
                    ["url"] = b.Url,
                    ["guid"] = Guid.NewGuid().ToString()
                });
            }
            
            var roots = rootNode["roots"] as JsonObject;
            if (roots != null)
            {
                var bbar = roots["bookmark_bar"] as JsonObject;
                if (bbar != null)
                {
                    bbar["children"] = children;
                }
            }
            
            File.WriteAllText(DataFile, rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* ignore write errors */ }
    }
}

