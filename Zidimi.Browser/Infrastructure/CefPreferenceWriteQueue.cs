namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Serializes runtime preference writes before they reach CEF.
///
/// UI controls can fire several SaveGlobal/SaveProfile calls quickly. Sending those writes as
/// unrelated fire-and-forget tasks allows an older operation to arrive after a newer one, and an
/// application close can otherwise race the last queued CEF UI-thread task. This queue preserves
/// ordering and gives App.OnExit one bounded drain point before RequestContexts/Cef.Shutdown.
/// It never writes Chromium files itself.
/// </summary>
internal static class CefPreferenceWriteQueue
{
    private static readonly object Gate = new();
    private static Task _tail = Task.CompletedTask;
    private static long _sequence;

    public static void Enqueue(string reason, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var sequence = Interlocked.Increment(ref _sequence);

        lock (Gate)
        {
            _tail = RunAfterAsync(_tail, sequence, reason, operation);
        }
    }

    public static bool Drain(TimeSpan timeout)
    {
        Task pending;
        lock (Gate) pending = _tail;

        if (pending.IsCompleted)
        {
            try
            {
                pending.GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Log("CEFPreferences", ex, "Observing completed preference queue during shutdown.");
                return false;
            }
        }

        try
        {
            if (!pending.Wait(timeout))
            {
                AppLogger.Log("CEFPreferences", $"Timed out after {timeout.TotalMilliseconds:0} ms waiting for CEF preference writes.");
                return false;
            }

            pending.GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Log("CEFPreferences", ex, "Draining CEF preference queue during shutdown.");
            return false;
        }
    }

    private static async Task RunAfterAsync(Task previous, long sequence, string reason, Func<Task> operation)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // Each operation is guarded below. A previous failure must not poison later writes.
        }

        try
        {
            await operation().ConfigureAwait(false);
            AppLogger.Log("CEFPreferences", $"Applied queued preference write #{sequence}: {reason}.");
        }
        catch (Exception ex)
        {
            AppLogger.Log("CEFPreferences", ex, $"Queued preference write #{sequence} failed: {reason}.");
        }
    }
}
