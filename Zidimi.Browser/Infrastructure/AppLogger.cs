using System;
using System.IO;
using System.Text;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Thread-safe lifecycle/crash logger. Writes a single log file next to the
/// executable (falling back to %LOCALAPPDATA%\Zidimi Browser if the app folder
/// is not writable). A "clean exit" marker file lets the next session detect
/// that the previous one ended abnormally — this is how native CEF crashes are
/// caught, because they never reach the managed exception handlers.
/// </summary>
public static class AppLogger
{
    private static readonly object Lock = new();
    private static readonly string LogDir;
    private static readonly string LogFile;
    private static readonly string CleanMarker;
    private static DateTime _startedAt;

    /// <summary>Full path of the current log file.</summary>
    public static string LogPath => LogFile;

    static AppLogger()
    {
        LogDir = ResolveLogDir();
        LogFile = Path.Combine(LogDir, "zidimi-browser.log");
        CleanMarker = Path.Combine(LogDir, "clean-exit.marker");
    }

    /// <summary>
    /// Must be called once at startup. Records the start time, logs the previous
    /// session's exit state, and removes the clean-exit marker for the new session.
    /// </summary>
    public static void Init()
    {
        try
        {
            _startedAt = DateTime.Now;
            Directory.CreateDirectory(LogDir);

            if (!File.Exists(LogFile))
            {
                Write("Lifecycle", "First run — no previous session log found.");
            }
            else if (!File.Exists(CleanMarker))
            {
                Write("Lifecycle", "Previous session did NOT shut down cleanly (clean-exit marker missing) — likely a crash or force-close.");
            }
            else
            {
                try { File.Delete(CleanMarker); } catch { }
            }

            Write("Lifecycle",
                $"Application started. Version={GetVersion()}, OS={Environment.OSVersion}, Path={Environment.ProcessPath ?? "unknown"}");
        }
        catch { }
    }

    public static void Log(string category, string message)
    {
        try { Write(category, message); } catch { }
    }

    public static void Log(string category, Exception? ex, string? context = null)
    {
        try
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(context)) sb.AppendLine(context);
            if (ex != null)
            {
                var current = ex;
                int depth = 0;
                while (current != null && depth < 10)
                {
                    sb.AppendLine($"[ex{depth}] {current.GetType().FullName}: {current.Message}");
                    if (!string.IsNullOrEmpty(current.StackTrace)) sb.AppendLine(current.StackTrace);
                    current = current.InnerException;
                    depth++;
                }
            }
            Write(category, sb.ToString());
        }
        catch { }
    }

    /// <summary>Records a graceful exit and writes the marker that lets the next startup verify a clean shutdown.</summary>
    public static void MarkCleanExit()
    {
        try
        {
            Write("Lifecycle", $"Application exited cleanly. Uptime={(DateTime.Now - _startedAt):c}");
            File.WriteAllText(CleanMarker, DateTime.Now.ToString("O"));
        }
        catch { }
    }

    private static string GetVersion()
    {
        try
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
        }
        catch { return "?"; }
    }

    private static string ResolveLogDir()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir) && CanWrite(baseDir))
                return baseDir;
        }
        catch { }

        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zidimi Browser");
        try { Directory.CreateDirectory(local); } catch { }
        return local;
    }

    private static bool CanWrite(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".zidimi-write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    private static void Write(string category, string message)
    {
        lock (Lock)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
            File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}