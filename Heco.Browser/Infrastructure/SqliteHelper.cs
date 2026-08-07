using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Helper to open/create SQLite databases using Chrome's schema (User Data\&lt;profile&gt;).
/// Chrome uses WebKit-style timestamps: microseconds since 1601-01-01 00:00:00 UTC
/// (equal to Windows FILETIME / 10). Every .db file has a meta(key,value) table to store the version.
/// </summary>
public static class SqliteHelper
{
    /// <summary>Chrome/Windows FILETIME epoch.</summary>
    private static readonly DateTime ChromeEpoch = new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static long ToChromeTime(DateTime utc) => (long)((utc.ToUniversalTime().Ticks - ChromeEpoch.Ticks) / 10);

    public static DateTime FromChromeTime(long micros) => ChromeEpoch.AddTicks(micros * 10).ToLocalTime();

    /// <summary>Opens a SQLite connection, creating the profile folder first if needed.</summary>
    public static SqliteConnection Open(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        conn.Open();
        Exec(conn, "PRAGMA journal_mode=DELETE;");
        Exec(conn, "PRAGMA synchronous=NORMAL;");
        Exec(conn, "PRAGMA foreign_keys=ON;");
        return conn;
    }

    /// <summary>Runs a DDL/DML command.</summary>
    public static void Exec(SqliteConnection conn, string sql, params (string name, object? value)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Writes meta(key,value) — similar to Chrome's meta table.</summary>
    public static void SetMeta(SqliteConnection conn, string key, string value)
    {
        Exec(conn,
            "INSERT INTO meta(key, value) VALUES($k, $v) " +
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value;",
            ("$k", key), ("$v", value));
    }

    /// <summary>Checks whether a table already exists.</summary>
    public static bool TableExists(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$t;";
        cmd.Parameters.AddWithValue("$t", table);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
