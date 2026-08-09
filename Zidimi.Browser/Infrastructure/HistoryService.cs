using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Zidimi.Browser.Models;
using Microsoft.Data.Sqlite;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Stores browsing history as SQLite using Chrome's schema (User Data\&lt;profile&gt;\History).
/// Timestamps use WebKit time (microseconds since 1601-01-01) like Chrome.
/// </summary>
public sealed class HistoryService
{
    private string _profileName = AppSettings.Global.CurrentProfile;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    private string DbPath => UserDataPaths.HistoryFile(_profileName);

    public HistoryService()
    {
        Load();
    }

    /// <summary>Switches to another profile — reloads that profile's history.</summary>
    public void SwitchProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName == _profileName) return;
        _profileName = profileName;
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
        Load();
    }

    public void Add(string url, string title)
    {
        // CEF handles history natively.
    }

    public void Remove(HistoryEntry entry)
    {
        // CEF handles history natively. Read-only from UI.
        if (entry != null)
            Application.Current?.Dispatcher.Invoke(() => Entries.Remove(entry));
    }

    public void Clear()
    {
        // CEF handles history natively. Read-only from UI.
        Application.Current?.Dispatcher.Invoke(Entries.Clear);
    }

    private void Load()
    {
        try
        {
            using var conn = SqliteHelper.Open(DbPath);

            var list = new System.Collections.Generic.List<HistoryEntry>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT u.id, u.url, u.title, u.last_visit_time
                FROM urls u
                ORDER BY u.last_visit_time DESC;
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new HistoryEntry
                {
                    Id = r.GetInt64(0),
                    Url = r.GetString(1),
                    Title = r.IsDBNull(2) || string.IsNullOrWhiteSpace(r.GetString(2)) ? r.GetString(1) : r.GetString(2),
                    VisitedAt = SqliteHelper.FromChromeTime(r.GetInt64(3)),
                });
            }

            foreach (var e in list)
                Entries.Add(e);
        }
        catch { }
    }

    private void EnsureSchema(SqliteConnection conn)
    {
        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS meta(key LONGVARCHAR NOT NULL UNIQUE PRIMARY KEY, value LONGVARCHAR);");

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS urls (
                id INTEGER PRIMARY KEY AUTOINCREMENT, url LONGVARCHAR, title LONGVARCHAR,
                visit_count INTEGER DEFAULT 0 NOT NULL, typed_count INTEGER DEFAULT 0 NOT NULL,
                last_visit_time INTEGER NOT NULL, hidden INTEGER DEFAULT 0 NOT NULL);
            """);

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS keyword_search_terms (
                keyword_id INTEGER NOT NULL, url_id INTEGER NOT NULL,
                term LONGVARCHAR NOT NULL, normalized_term LONGVARCHAR NOT NULL);
            """);

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS visits (
                id INTEGER PRIMARY KEY AUTOINCREMENT, url INTEGER NOT NULL, visit_time INTEGER NOT NULL,
                from_visit INTEGER, external_referrer_url LONGVARCHAR,
                transition INTEGER DEFAULT 0 NOT NULL, segment_id INTEGER,
                visit_duration INTEGER DEFAULT 0 NOT NULL, incremented_omnibox_typed_score INTEGER DEFAULT 0 NOT NULL,
                opener_visit INTEGER, originator_cache_guid TEXT, originator_visit_id INTEGER,
                originator_from_visit INTEGER, originator_opener_visit INTEGER,
                is_known_to_sync INTEGER DEFAULT 0 NOT NULL, consider_for_ntp_most_visited INTEGER DEFAULT 0 NOT NULL,
                visited_link_id INTEGER, app_id TEXT);
            """);

        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS visit_source(id INTEGER PRIMARY KEY, source INTEGER NOT NULL);");

        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS segments(id INTEGER PRIMARY KEY, name VARCHAR, url_id INTEGER NOT NULL);");
        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS segment_usage(id INTEGER PRIMARY KEY, segment_id INTEGER NOT NULL, time_slot INTEGER NOT NULL, visit_count INTEGER DEFAULT 0 NOT NULL);");

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS visited_links (
                id INTEGER PRIMARY KEY, link_url_id INTEGER NOT NULL,
                top_level_url LONGVARCHAR NOT NULL, frame_url LONGVARCHAR NOT NULL, visit_count INTEGER);
            """);

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS downloads (
                id INTEGER PRIMARY KEY, guid VARCHAR NOT NULL, current_path LONGVARCHAR, target_path LONGVARCHAR,
                start_time INTEGER NOT NULL, received_bytes INTEGER NOT NULL, total_bytes INTEGER NOT NULL,
                state INTEGER NOT NULL, danger_type INTEGER NOT NULL, interrupt_reason INTEGER NOT NULL,
                hash BLOB, end_time INTEGER NOT NULL, opened INTEGER NOT NULL, last_access_time INTEGER NOT NULL,
                transient INTEGER, referrer VARCHAR, site_url VARCHAR, embedder_download_data BLOB,
                tab_url VARCHAR, tab_referrer_url VARCHAR, http_method VARCHAR, by_ext_id VARCHAR,
                by_ext_name VARCHAR, by_web_app_id VARCHAR, etag VARCHAR, last_modified VARCHAR,
                mime_type VARCHAR, original_mime_type VARCHAR);
            """);
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS downloads_url_chains (
                id INTEGER NOT NULL, chain_index INTEGER NOT NULL, url LONGVARCHAR NOT NULL);
            """);
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS downloads_slices (
                download_id INTEGER NOT NULL, offset INTEGER NOT NULL, received_bytes INTEGER NOT NULL, finished INTEGER DEFAULT 0 NOT NULL);
            """);

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS content_annotations (
                visit_id INTEGER PRIMARY KEY, visibility_score NUMERIC, floc_protected_score NUMERIC,
                categories TEXT, page_topics_model_version INTEGER, annotation_flags INTEGER NOT NULL,
                entities TEXT, related_searches TEXT, search_normalized_url TEXT, search_terms TEXT,
                alternative_title TEXT, page_language TEXT, password_state INTEGER, has_url_keyed_image BOOLEAN);
            """);
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS context_annotations (
                visit_id INTEGER PRIMARY KEY, context_annotation_flags INTEGER NOT NULL,
                duration_since_last_visit INTEGER, page_end_reason INTEGER, total_foreground_duration INTEGER,
                browser_type INTEGER DEFAULT 0 NOT NULL, window_id INTEGER, tab_id INTEGER, task_id INTEGER,
                root_task_id INTEGER, parent_task_id INTEGER, response_code INTEGER);
            """);

        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS clusters (
                cluster_id INTEGER PRIMARY KEY, should_show_on_prominent_ui_surfaces INTEGER,
                label TEXT, raw_label TEXT, triggerability_calculated INTEGER,
                originator_cache_guid TEXT, originator_cluster_id INTEGER);
            """);
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS clusters_and_visits (
                cluster_id INTEGER NOT NULL, visit_id INTEGER NOT NULL, score REAL NOT NULL,
                engagement_score REAL NOT NULL, url_for_deduping LONGVARCHAR NOT NULL,
                normalized_url LONGVARCHAR NOT NULL, url_for_display LONGVARCHAR NOT NULL,
                interaction_state LONGVARCHAR NOT NULL);
            """);
        SqliteHelper.Exec(conn, """
            CREATE TABLE IF NOT EXISTS cluster_keywords (
                cluster_id INTEGER NOT NULL, keyword TEXT NOT NULL, type INTEGER NOT NULL,
                score REAL NOT NULL, collections TEXT NOT NULL);
            """);
        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS cluster_visit_duplicates(visit_id INTEGER NOT NULL, duplicate_visit_id INTEGER NOT NULL);");

        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS history_sync_metadata(storage_key INTEGER PRIMARY KEY NOT NULL, value BLOB);");
        SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS history_sync_model_metadata(id INTEGER PRIMARY KEY NOT NULL, value BLOB);");

        try { SqliteHelper.SetMeta(conn, "version", "24"); } catch { }
    }

    /// <summary>Migrates the old History.json (the previous JSON layout) to SQLite, then deletes the migrate file.</summary>
    private void MigrateLegacyJson()
    {
        try
        {
            var migrateFile = Path.Combine(UserDataPaths.ProfileDir(_profileName), "History.migrate");
            if (!File.Exists(migrateFile)) return;

            System.Collections.Generic.List<HistoryEntry>? legacy = null;
            try
            {
                legacy = JsonSerializer.Deserialize<System.Collections.Generic.List<HistoryEntry>>(File.ReadAllText(migrateFile));
            }
            catch { }

            if (legacy != null && legacy.Count > 0)
            {
                UserDataPaths.EnsureProfileDir(_profileName);
                using var conn = SqliteHelper.Open(DbPath);
                EnsureSchema(conn);
                foreach (var e in legacy)
                {
                    var chromeTime = SqliteHelper.ToChromeTime(e.VisitedAt);
                    SqliteHelper.Exec(conn,
                        "INSERT INTO urls(url, title, visit_count, typed_count, last_visit_time, hidden) VALUES ($u,$t,1,0,$ct,0);",
                        ("$u", e.Url), ("$t", e.Title), ("$ct", chromeTime));
                    using var last = conn.CreateCommand();
                    last.CommandText = "SELECT last_insert_rowid();";
                    var urlId = Convert.ToInt64(last.ExecuteScalar());
                    SqliteHelper.Exec(conn,
                        "INSERT INTO visits(url, visit_time, from_visit, transition, segment_id, visit_duration) VALUES ($id,$ct,0,1,NULL,0);",
                        ("$id", urlId), ("$ct", chromeTime));
                }
            }

            File.Delete(migrateFile);
        }
        catch { }
    }
}

