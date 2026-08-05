using System.Collections.Generic;
using System.IO;
using CefSharp;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Tạo RequestContext theo Profile (spec 8 — Multi-Profile).
/// - Profile mặc định: cache bản trên đĩa.
/// - Guest mode: CachePath rỗng = in-memory, không ghi gì xuống đĩa.
/// - Dynamic Profile: Tạo RequestContext theo tên Profile.
/// Chia sẻ cùng 1 context cho các tab trong cùng profile để tiết kiệm RAM.
/// </summary>
public sealed class RequestContextFactory : System.IDisposable
{
    private readonly string _defaultCachePath;
    private IRequestContext? _defaultContext;
    private IRequestContext? _guestContext;
    private readonly Dictionary<string, IRequestContext> _profileContexts = new();

    public RequestContextFactory(string defaultCachePath)
    {
        _defaultCachePath = defaultCachePath;
        Directory.CreateDirectory(defaultCachePath);
    }

    /// <summary>Context bản (profile mặc định).</summary>
    public IRequestContext GetDefaultContext()
    {
        if (_defaultContext == null || _defaultContext.IsDisposed)
        {
            _defaultContext = Create(_defaultCachePath, persist: true);
        }
        return _defaultContext;
    }

    /// <summary>Context in-memory (guest mode / chế độ khách).</summary>
    public IRequestContext GetGuestContext()
    {
        if (_guestContext == null || _guestContext.IsDisposed)
        {
            _guestContext = Create(cachePath: "", persist: false);
        }
        return _guestContext;
    }

    /// <summary>Context cho Profile chỉ định.</summary>
    public IRequestContext? GetProfileContext(string profileName)
    {
        // Trả về null để sử dụng global context (đã khai báo ở CefSettings.CachePath), 
        // tránh lỗi tạo RequestContext trùng cache path gây crash ứng dụng.
        if (string.IsNullOrEmpty(profileName) || profileName == "Cá nhân")
            return null;

        if (_profileContexts.TryGetValue(profileName, out var context) && !context.IsDisposed)
        {
            return context;
        }

        var safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(Directory.GetParent(_defaultCachePath)?.FullName ?? _defaultCachePath, "Profiles", safeName);
        Directory.CreateDirectory(path);

        var newContext = Create(path, persist: true);
        _profileContexts[profileName] = newContext;
        return newContext;
    }

    private static IRequestContext Create(string cachePath, bool persist)
    {
        var settings = new RequestContextSettings
        {
            CachePath = cachePath,
            PersistSessionCookies = persist,
        };
        return new RequestContext(settings);
    }

    public void Dispose()
    {
        _defaultContext?.Dispose();
        _guestContext?.Dispose();
        foreach (var ctx in _profileContexts.Values)
        {
            ctx?.Dispose();
        }
        _profileContexts.Clear();
        _defaultContext = null;
        _guestContext = null;
    }
}
