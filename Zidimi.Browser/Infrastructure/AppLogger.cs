using System.Diagnostics;
using System.Text;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Lightweight diagnostics that deliberately do not create a Zidimi-specific file in User Data.
/// CEF's optional native diagnostics use User Data/chrome_debug.log; managed diagnostics are sent
/// to Debug/Trace listeners only. This keeps Chromium User Data free of parallel app-state files.
/// </summary>
public static class AppLogger
{
    private static DateTime _startedAt;
    public static string LogPath => UserDataPaths.ChromeDebugLogFile;

    public static void Init()
    {
        _startedAt = DateTime.Now;
        Log("Lifecycle", $"Application started. Version={GetVersion()}, OS={Environment.OSVersion}");
    }

    public static void Log(string category, string message)
        => Write(category, message);

    public static void Log(string category, Exception? ex, string? context = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(context)) sb.AppendLine(context);
        var current = ex;
        for (var depth = 0; current != null && depth < 10; depth++, current = current.InnerException)
        {
            sb.AppendLine($"[ex{depth}] {current.GetType().FullName}: {current.Message}");
            if (!string.IsNullOrWhiteSpace(current.StackTrace)) sb.AppendLine(current.StackTrace);
        }
        Write(category, sb.ToString());
    }

    public static void MarkCleanExit()
        => Log("Lifecycle", $"Application exited cleanly. Uptime={(DateTime.Now - _startedAt):c}");

    private static void Write(string category, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}";
        Debug.WriteLine(line);
        Trace.WriteLine(line);
    }

    private static string GetVersion()
    {
        try { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?"; }
        catch { return "?"; }
    }
}
