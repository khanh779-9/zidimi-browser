using System;
using System.Collections.Generic;
using CefSharp;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Provides a RequestContext for a tab based on the browsing mode.
/// Each profile has its own RequestContext whose CachePath points at its folder
/// (User Data\&lt;ProfileFolder&gt; — a subfolder of the root, valid per the
/// CefSettings.RootCachePath requirement), so cookies, session and localStorage are
/// isolated per profile, following the Chromium model. Guest mode uses an in-memory
/// context (empty CachePath) so nothing is written to disk.
/// </summary>
public sealed class RequestContextFactory : IDisposable
{
    private static readonly object Lock = new();
    private readonly Dictionary<string, IRequestContext> _profileContexts = new();
    private IRequestContext? _guestContext;

    /// <summary>The base context (default profile) — the default profile's profile context.</summary>
    public IRequestContext? GetDefaultContext()
        => GetProfileContext(UserDataPaths.DefaultProfileName);

    /// <summary>In-memory context (guest mode).</summary>
    public IRequestContext GetGuestContext()
    {
        lock (Lock)
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
        lock (Lock)
        {
            if (_guestContext != null)
            {
                try { _guestContext.Dispose(); } catch { }
                _guestContext = null;
            }
        }
    }

/// <summary>
/// The context for the given Profile. CachePath points at the profile's own folder
/// (User Data\&lt;ProfileName&gt;) so cookies/session are isolated per profile.
/// </summary>
    public IRequestContext? GetProfileContext(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        lock (Lock)
        {
            if (_profileContexts.TryGetValue(profileName, out var existing) && !existing.IsDisposed)
                return existing;

            var cachePath = UserDataPaths.ProfileDir(profileName);
            try { System.IO.Directory.CreateDirectory(cachePath); } catch { }

            var context = new RequestContext(new RequestContextSettings
            {
                CachePath = cachePath,
                PersistSessionCookies = true,
            });
            _profileContexts[profileName] = context;

            // Chromium only exposes unpacked extension loading when developer mode is enabled
            // for this profile. Set it before the first tab starts loading extensions.
            context.SetPreferenceSafe("extensions.ui.developer_mode", true);
            ExtensionService.Instance.LoadProfileExtensions(context);
            return context;
        }
    }

    public void Dispose()
    {
        lock (Lock)
        {
            foreach (var ctx in _profileContexts.Values)
                try { ctx.Dispose(); } catch { }
            _profileContexts.Clear();

            _guestContext?.Dispose();
            _guestContext = null;
        }
    }
}
