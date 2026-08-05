using System.Collections.ObjectModel;
using Heco.Browser.Models;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Heco.Browser.Infrastructure;

/// <summary>Bookmark service có persistence JSON.</summary>
public sealed class BookmarkService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HecoBrowser");
    private static readonly string DataFile = Path.Combine(DataDir, "bookmarks.json");

    public ObservableCollection<Bookmark> Items { get; } = new();

    public BookmarkService()
    {
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
            var list = JsonSerializer.Deserialize<List<Bookmark>>(json) ?? new();
            foreach (var b in list) Items.Add(b);
        }
        catch { /* ignore corrupted file */ }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(Items.ToList());
            File.WriteAllText(DataFile, json);
        }
        catch { /* ignore write errors */ }
    }
}
