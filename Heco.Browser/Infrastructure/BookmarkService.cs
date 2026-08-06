using System.Collections.ObjectModel;
using Heco.Browser.Models;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Heco.Browser.Infrastructure;

/// <summary>Bookmark service có persistence JSON theo profile (User Data\&lt;profile&gt;\Bookmarks — giống Chrome).</summary>
public sealed class BookmarkService
{
    private string _profileName = AppSettings.Global.CurrentProfile;

    public ObservableCollection<Bookmark> Items { get; } = new();

    private string DataFile => UserDataPaths.BookmarksFile(_profileName);

    public BookmarkService()
    {
        Load();
    }

    /// <summary>Chuyển sang profile khác — tải lại bookmarks của profile đó.</summary>
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
            MigrateLegacyJson();
            if (!File.Exists(DataFile)) return;
            var json = File.ReadAllText(DataFile);
            var list = JsonSerializer.Deserialize<List<Bookmark>>(json) ?? new();
            foreach (var b in list) Items.Add(b);
        }
        catch { /* ignore corrupted file */ }
    }

    public void Save()
    {
        try
        {
            UserDataPaths.EnsureProfileDir(_profileName);
            var json = JsonSerializer.Serialize(Items.ToList());
            File.WriteAllText(DataFile, json);
        }
        catch { /* ignore write errors */ }
    }

    /// <summary>Di chuyển bookmarks JSON cũ (nếu migration chưa rename kịp) sang file mới.</summary>
    private void MigrateLegacyJson()
    {
        try
        {
            var dir = UserDataPaths.ProfileDir(_profileName);
            var oldFile = System.IO.Path.Combine(dir, "Bookmarks.migrate");
            if (File.Exists(oldFile) && !File.Exists(DataFile))
                File.Move(oldFile, DataFile);
        }
        catch { }
    }
}

