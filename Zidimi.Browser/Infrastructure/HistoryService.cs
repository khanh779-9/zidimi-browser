using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Read-through view of Chromium's native History database.
///
/// Chromium is the only persistent writer. Navigation callbacks update the WPF collection in memory
/// for immediate UI feedback, but Zidimi never creates a mirror history.db and never opens the
/// native History database read/write while CEF owns it.
/// </summary>
public sealed class HistoryService : IDisposable
{
    public const int MaxInMemoryEntries = 5_000;

    private string _profileName = AppSettings.Global.CurrentProfile;
    private int _loadGeneration;
    private long _transientId;
    private readonly Dictionary<string, HistoryEntry> _entriesByUrl = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

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

    /// <summary>
    /// Updates only the live shell list. Chromium records the same navigation in its native
    /// History service/database; writing that SQLite file from the embedding app would race it.
    /// </summary>
    public void Add(string url, string title)
    {
        url = (url ?? string.Empty).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        title = string.IsNullOrWhiteSpace(title) ? url : title.Trim();
        var now = DateTime.Now;

        void Apply()
        {
            if (_entriesByUrl.TryGetValue(url, out var existing))
            {
                existing.Title = title;
                existing.VisitedAt = now;
                Entries.Remove(existing);
                Entries.Insert(0, existing);
                return;
            }

            var entry = new HistoryEntry
            {
                // Negative ids are session-only placeholders until a native reload supplies the
                // real Chromium urls.id value.
                Id = Interlocked.Decrement(ref _transientId),
                Url = url,
                Title = title,
                VisitedAt = now,
            };
            Entries.Insert(0, entry);
            _entriesByUrl[entry.Url] = entry;
            TrimMemory();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) Apply();
        else dispatcher.BeginInvoke((Action)Apply);
    }

    private async Task LoadAsync(string profileId)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            var list = await Task.Run(() => LoadNativeSnapshot(profileId)).ConfigureAwait(false);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            await dispatcher.InvokeAsync(() =>
            {
                if (generation != _loadGeneration ||
                    !string.Equals(profileId, _profileName, StringComparison.OrdinalIgnoreCase))
                    return;

                ClearMemory();
                foreach (var entry in list)
                {
                    Entries.Add(entry);
                            _entriesByUrl[entry.Url] = entry;
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Log("History", ex, $"Reading native Chromium History for '{profileId}'.");
        }
    }

    private static List<HistoryEntry> LoadNativeSnapshot(string profileId)
    {
        var path = UserDataPaths.HistoryFile(profileId);
        if (!File.Exists(path)) return new List<HistoryEntry>();

        using var conn = SqliteHelper.OpenReadOnly(path);
        if (!SqliteHelper.TableExists(conn, "urls")) return new List<HistoryEntry>();

        var list = new List<HistoryEntry>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, url, title, last_visit_time
            FROM urls
            WHERE hidden = 0 AND last_visit_time > 0
            ORDER BY last_visit_time DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", MaxInMemoryEntries);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var url = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(url)) continue;
            list.Add(new HistoryEntry
            {
                Id = reader.GetInt64(0),
                Url = url,
                Title = reader.IsDBNull(2) || string.IsNullOrWhiteSpace(reader.GetString(2))
                    ? url : reader.GetString(2),
                VisitedAt = SqliteHelper.FromChromeTime(reader.GetInt64(3)),
            });
        }
        return list;
    }

    private void RemoveFromMemory(HistoryEntry entry)
    {
        Entries.Remove(entry);
        if (_entriesByUrl.TryGetValue(entry.Url, out var mapped) && ReferenceEquals(mapped, entry))
            _entriesByUrl.Remove(entry.Url);
    }

    private void ClearMemory()
    {
        Entries.Clear();
        _entriesByUrl.Clear();
    }

    private void TrimMemory()
    {
        while (Entries.Count > MaxInMemoryEntries)
            RemoveFromMemory(Entries[^1]);
    }

    public void Dispose() { }
}
