using System;
using System.Collections.ObjectModel;
using System.Windows;
using Zidimi.Browser.Models;
using Microsoft.Data.Sqlite;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Stores the app's download list as its own SQLite database
/// (User Data\&lt;profile&gt;\zidimi_downloads.db). It never touches Chromium's downloads table;
/// entries are added/updated from the DownloadHandler and written to disk
/// so the data survives restarts.
/// </summary>
public sealed class DownloadService
{
    private string _profileName = AppSettings.Global.CurrentProfile;

    public ObservableCollection<DownloadEntry> Entries { get; } = new();

    private string DbPath => UserDataPaths.DownloadsFile(_profileName);

    public DownloadService()
    {
        Load();
    }

    /// <summary>Switches to another profile — reloads that profile's download list.</summary>
    public void SwitchProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == _profileName) return;
        _profileName = profileName;
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
        Load();
    }

    /// <summary>Adds a new download (DownloadStarted) and writes it to disk.</summary>
    public void Add(DownloadEntry entry)
    {
        if (entry == null) return;
        Application.Current?.Dispatcher.Invoke(() => Entries.Insert(0, entry));
        try
        {
            UserDataPaths.EnsureProfileDir(_profileName);
            using var conn = SqliteHelper.Open(DbPath);
            EnsureSchema(conn);
            SqliteHelper.Exec(conn,
                """
                INSERT INTO downloads(guid, url, title, full_path, is_cancelled, is_complete, total_bytes, received_bytes, start_time)
                VALUES ($g, $u, $n, $p, $c, $d, $t, $r, $s);
                """,
                ("$g", entry.Guid), ("$u", entry.Url), ("$n", entry.SuggestedFileName),
                ("$p", entry.FullPath), ("$c", entry.IsCancelled ? 1 : 0), ("$d", entry.IsComplete ? 1 : 0),
                ("$t", entry.TotalBytes), ("$r", entry.ReceivedBytes),
                ("$s", SqliteHelper.ToChromeTime(entry.StartedAt)));
        }
        catch { }
    }

    /// <summary>Updates a download's state by its stable GUID and saves it back.</summary>
    public void Update(DownloadEntry entry)
    {
        if (entry == null) return;
        try
        {
            UserDataPaths.EnsureProfileDir(_profileName);
            using var conn = SqliteHelper.Open(DbPath);
            EnsureSchema(conn);

            // If DownloadStarted was missed, insert the update as a new row.
            var affected = UpdateExisting(conn, entry);

            if (affected == 0)
            {
                SqliteHelper.Exec(conn,
                    """
                    INSERT INTO downloads(guid, url, title, full_path, is_cancelled, is_complete, total_bytes, received_bytes, start_time)
                    VALUES ($g, $u, $n, $p, $c, $d, $t, $r, $s);
                    """,
                    ("$g", entry.Guid), ("$u", entry.Url), ("$n", entry.SuggestedFileName),
                    ("$p", entry.FullPath), ("$c", entry.IsCancelled ? 1 : 0), ("$d", entry.IsComplete ? 1 : 0),
                    ("$t", entry.TotalBytes), ("$r", entry.ReceivedBytes),
                    ("$s", SqliteHelper.ToChromeTime(entry.StartedAt)));
            }
        }
        catch { }
    }

    /// <summary>Updates the row matching the stable download GUID.</summary>
    private static int UpdateExisting(SqliteConnection conn, DownloadEntry entry)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE downloads SET url=$u, title=$n, full_path=$p, is_cancelled=$c, is_complete=$d,
                   total_bytes=$t, received_bytes=$r
            WHERE guid=$g;
            """;
        cmd.Parameters.AddWithValue("$g", entry.Guid);
        cmd.Parameters.AddWithValue("$p", (object?)entry.FullPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", entry.IsCancelled ? 1 : 0);
        cmd.Parameters.AddWithValue("$d", entry.IsComplete ? 1 : 0);
        cmd.Parameters.AddWithValue("$t", entry.TotalBytes);
        cmd.Parameters.AddWithValue("$r", entry.ReceivedBytes);
        cmd.Parameters.AddWithValue("$u", (object?)entry.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)entry.SuggestedFileName ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Removes an entry from the list and from disk.</summary>
    public void Remove(DownloadEntry entry)
    {
        if (entry == null) return;
        Application.Current?.Dispatcher.Invoke(() => Entries.Remove(entry));
        try
        {
            using var conn = SqliteHelper.Open(DbPath);
            SqliteHelper.Exec(conn, "DELETE FROM downloads WHERE guid=$g;",
                ("$g", entry.Guid));
        }
        catch { }
    }

    /// <summary>Clears the entire download list.</summary>
    public void Clear()
    {
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
        try
        {
            using var conn = SqliteHelper.Open(DbPath);
            SqliteHelper.Exec(conn, "DELETE FROM downloads;");
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!System.IO.File.Exists(DbPath)) return;
            using var conn = SqliteHelper.Open(DbPath);
            EnsureSchema(conn);

            var list = new System.Collections.Generic.List<DownloadEntry>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT guid, url, title, full_path, is_cancelled, is_complete, total_bytes, received_bytes, start_time FROM downloads ORDER BY start_time DESC;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DownloadEntry
                {
                    Guid = r.GetString(0),
                    Url = r.GetString(1),
                    SuggestedFileName = r.GetString(2),
                    FullPath = r.IsDBNull(3) ? "" : r.GetString(3),
                    IsCancelled = r.GetInt64(4) != 0,
                    IsComplete = r.GetInt64(5) != 0,
                    TotalBytes = r.IsDBNull(6) ? -1 : r.GetInt64(6),
                    ReceivedBytes = r.IsDBNull(7) ? 0 : r.GetInt64(7),
                    StartedAt = r.IsDBNull(8) ? DateTime.Now : SqliteHelper.FromChromeTime(r.GetInt64(8)),
                });
            }

            foreach (var e in list)
                Entries.Add(e);
        }
        catch { }
    }

    private void EnsureSchema(SqliteConnection conn)
    {
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS downloads (
                guid TEXT PRIMARY KEY,
                url TEXT, title TEXT, full_path TEXT,
                is_cancelled INTEGER DEFAULT 0, is_complete INTEGER DEFAULT 0,
                total_bytes INTEGER DEFAULT -1, received_bytes INTEGER DEFAULT 0,
                start_time INTEGER);
            """);
        try { SqliteHelper.SetMeta(conn, "version", "1"); } catch { }
    }
}