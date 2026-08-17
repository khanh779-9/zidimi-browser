using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Live CEF download list plus read-through of Chromium History.downloads.
/// No download mirror database is created. CEF DownloadHandler owns current-process updates;
/// Chromium owns any persistent download history that its runtime chooses to populate.
/// </summary>
public sealed class DownloadService : IDisposable
{
    public const int MaxInMemoryEntries = 2_000;

    private string _profileName = AppSettings.Global.CurrentProfile;
    private int _loadGeneration;
    private readonly Dictionary<string, DownloadEntry> _entriesByGuid = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<DownloadEntry> Entries { get; } = new();

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

    public void Add(DownloadEntry entry) => SyncBoundEntry(entry);
    public void Update(DownloadEntry entry) => SyncBoundEntry(entry);

    private void SyncBoundEntry(DownloadEntry source)
    {
        void Apply()
        {
            if (!_entriesByGuid.TryGetValue(source.Guid, out var existing))
            {
                Entries.Insert(0, source);
                _entriesByGuid[source.Guid] = source;
                TrimMemory();
                return;
            }
            if (ReferenceEquals(existing, source)) return;
            existing.Url = source.Url;
            existing.SuggestedFileName = source.SuggestedFileName;
            existing.FullPath = source.FullPath;
            existing.IsCancelled = source.IsCancelled;
            existing.IsComplete = source.IsComplete;
            existing.TotalBytes = source.TotalBytes;
            existing.ReceivedBytes = source.ReceivedBytes;
            existing.StartedAt = source.StartedAt;
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
                    _entriesByGuid[entry.Guid] = entry;
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Log("Downloads", ex, $"Reading Chromium History.downloads for '{profileId}'.");
        }
    }

    private static List<DownloadEntry> LoadNativeSnapshot(string profileId)
    {
        var path = UserDataPaths.HistoryFile(profileId);
        if (!File.Exists(path)) return new List<DownloadEntry>();
        using var conn = SqliteHelper.OpenReadOnly(path);
        if (!SqliteHelper.TableExists(conn, "downloads")) return new List<DownloadEntry>();

        var hasChains = SqliteHelper.TableExists(conn, "downloads_url_chains");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = hasChains ? """
            SELECT d.guid,
                   COALESCE((SELECT c.url FROM downloads_url_chains c WHERE c.id=d.id ORDER BY c.chain_index DESC LIMIT 1), d.tab_url, d.site_url, ''),
                   d.target_path, d.current_path, d.state, d.total_bytes, d.received_bytes, d.start_time
            FROM downloads d
            ORDER BY d.start_time DESC
            LIMIT $limit;
            """ : """
            SELECT guid, COALESCE(tab_url, site_url, ''), target_path, current_path, state,
                   total_bytes, received_bytes, start_time
            FROM downloads
            ORDER BY start_time DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", MaxInMemoryEntries);
        using var reader = cmd.ExecuteReader();
        var list = new List<DownloadEntry>();
        while (reader.Read())
        {
            var target = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var current = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var fullPath = !string.IsNullOrWhiteSpace(target) ? target : current;
            var state = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            list.Add(new DownloadEntry
            {
                Guid = reader.IsDBNull(0) ? Guid.NewGuid().ToString("N") : reader.GetString(0),
                Url = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SuggestedFileName = string.IsNullOrWhiteSpace(fullPath) ? string.Empty : Path.GetFileName(fullPath),
                FullPath = fullPath,
                IsComplete = state == 1,
                IsCancelled = state == 2,
                TotalBytes = reader.IsDBNull(5) ? -1 : reader.GetInt64(5),
                ReceivedBytes = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                StartedAt = reader.IsDBNull(7) ? DateTime.Now : SqliteHelper.FromChromeTime(reader.GetInt64(7)),
            });
        }
        return list;
    }

    private void RemoveFromMemory(DownloadEntry entry)
    {
        Entries.Remove(entry);
        if (_entriesByGuid.TryGetValue(entry.Guid, out var mapped) && ReferenceEquals(mapped, entry))
            _entriesByGuid.Remove(entry.Guid);
    }

    private void ClearMemory()
    {
        Entries.Clear();
        _entriesByGuid.Clear();
    }

    private void TrimMemory()
    {
        while (Entries.Count > MaxInMemoryEntries)
            RemoveFromMemory(Entries[^1]);
    }

    public void Dispose() { }
}
