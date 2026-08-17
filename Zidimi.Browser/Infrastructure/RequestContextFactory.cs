using CefSharp;
using CefSharp.Handler;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Creates one disk-backed CEF RequestContext per non-default Chromium profile. The Default
/// profile uses Cef.GetGlobalRequestContext(), whose CefSettings.CachePath is configured to the
/// Default profile directory. This avoids creating two CEF contexts that both own the same
/// Default profile files.
/// </summary>
public sealed class RequestContextFactory : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IRequestContext> _profileContexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskCompletionSource<bool>> _profileReady = new(StringComparer.OrdinalIgnoreCase);
    private IRequestContext? _guestContext;

    public IRequestContext? GetDefaultContext()
    {
        if (Cef.IsInitialized != true) return null;
        try
        {
            var context = Cef.GetGlobalRequestContext();
            return context is { IsDisposed: false } ? context : null;
        }
        catch
        {
            return null;
        }
    }

    public IRequestContext GetGuestContext()
    {
        lock (_gate)
        {
            if (_guestContext == null || _guestContext.IsDisposed)
            {
                _guestContext = new RequestContext(new RequestContextSettings
                {
                    CachePath = string.Empty,
                    PersistSessionCookies = false,
                });
            }
            return _guestContext;
        }
    }

    public void ResetGuestContext()
    {
        lock (_gate)
        {
            if (_guestContext != null)
            {
                try { _guestContext.Dispose(); }
                catch (Exception ex) { AppLogger.Log("RequestContext", ex, "Disposing guest context."); }
                _guestContext = null;
            }
        }
    }

    public void ReleaseProfileContext(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return;
        var profileId = UserDataPaths.NormalizeProfileId(profileName);

        // The global RequestContext is CEF-owned and must survive until Cef.Shutdown().
        if (string.Equals(profileId, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase)) return;

        lock (_gate)
        {
            _profileReady.Remove(profileId);
            if (_profileContexts.Remove(profileId, out var context))
            {
                try { context.Dispose(); }
                catch (Exception ex) { AppLogger.Log("RequestContext", ex, $"Disposing profile context '{profileId}'."); }
            }
        }
    }

    public IRequestContext? GetProfileContext(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || Cef.IsInitialized != true) return null;

        var profileId = UserDataPaths.NormalizeProfileId(profileName);
        if (string.Equals(profileId, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            return GetDefaultContext();

        lock (_gate)
        {
            if (_profileContexts.TryGetValue(profileId, out var existing) && !existing.IsDisposed)
                return existing;

            var cachePath = UserDataPaths.ProfileDir(profileId);

            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new RequestContextHandler();
            handler.OnInitialize(_ => ready.TrySetResult(true));

            try
            {
                var context = new RequestContext(new RequestContextSettings
                {
                    CachePath = cachePath,
                }, handler);

                _profileContexts[profileId] = context;
                _profileReady[profileId] = ready;
                return context;
            }
            catch (Exception ex)
            {
                AppLogger.Log("RequestContext", ex, $"Creating CEF RequestContext for '{profileId}'.");
                return null;
            }
        }
    }

    public async Task<IRequestContext?> GetProfileContextReadyAsync(string profileName)
    {
        var profileId = UserDataPaths.NormalizeProfileId(profileName);
        var context = GetProfileContext(profileId);
        if (context == null) return null;

        if (string.Equals(profileId, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            return context;

        Task readyTask;
        lock (_gate)
        {
            readyTask = _profileReady.TryGetValue(profileId, out var ready)
                ? ready.Task
                : Task.CompletedTask;
        }

        await Task.WhenAny(readyTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
        return context.IsDisposed ? null : context;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var ctx in _profileContexts.Values)
            {
                try { ctx.Dispose(); }
                catch (Exception ex) { AppLogger.Log("RequestContext", ex, "Disposing profile context."); }
            }
            _profileContexts.Clear();
            _profileReady.Clear();

            try { _guestContext?.Dispose(); } catch { }
            _guestContext = null;
        }
    }
}
