using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure;

/// <summary>Lưu trữ lịch sử duyệt web, persisted theo profile (User Data\&lt;profile&gt;\History.json).</summary>
public sealed class HistoryService
{
    private string _profileName = AppSettings.Current.CurrentProfile;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    private string FilePath => UserDataPaths.HistoryFile(_profileName);

    public HistoryService()
    {
        Load();
    }

    /// <summary>Chuyển sang profile khác — tải lại lịch sử của profile đó.</summary>
    public void SwitchProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == _profileName) return;
        _profileName = profileName;
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
        Load();
    }

    public void Add(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var entry = new HistoryEntry
        {
            Url = url,
            Title = string.IsNullOrWhiteSpace(title) ? url : title,
            VisitedAt = DateTime.Now,
            Id = Entries.Count == 0 ? 1 : Entries.Max(x => x.Id) + 1,
        };
        Application.Current?.Dispatcher.Invoke(() => Entries.Insert(0, entry));
        Save();
    }

    public void Remove(HistoryEntry entry)
    {
        if (!Entries.Contains(entry)) return;
        Application.Current?.Dispatcher.Invoke(() => Entries.Remove(entry));
        Save();
    }

    public void Clear()
    {
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new();
            foreach (var e in list.OrderByDescending(x => x.VisitedAt))
                Entries.Add(e);
        }
        catch { /* ignore corrupted file */ }
    }

    private void Save()
    {
        try
        {
            UserDataPaths.EnsureProfileDir(_profileName);
            var json = JsonSerializer.Serialize(Entries.ToList());
            File.WriteAllText(FilePath, json);
        }
        catch { /* ignore write errors */ }
    }
}
