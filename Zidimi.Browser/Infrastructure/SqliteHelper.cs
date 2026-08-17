using Microsoft.Data.Sqlite;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Read-only helpers for Chromium-owned SQLite stores. Zidimi intentionally exposes no generic
/// writable/open-create helper here: persistent browser data should be mutated through CEF APIs,
/// not by bypassing Chromium's live services and locks.
/// </summary>
public static class SqliteHelper
{
    private static readonly DateTime ChromeEpoch = new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static DateTime FromChromeTime(long micros)
    {
        try { return ChromeEpoch.AddTicks(checked(micros * 10)).ToLocalTime(); }
        catch { return DateTime.MinValue; }
    }

    public static SqliteConnection OpenReadOnly(string dbPath)
    {
        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Chromium SQLite database was not found.", dbPath);
        var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
        conn.Open();
        return conn;
    }

    public static bool TableExists(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$t;";
        cmd.Parameters.AddWithValue("$t", table);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
