using System;
using System.Collections.Generic;
using CefSharp;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Cung cấp RequestContext cho tab theo chế độ duyệt.
/// Cache dùng chung cho mọi profile (User Data\Cache) nên tất cả profile đều dùng
/// global context (khai báo ở CefSettings.CachePath). Chỉ guest mode dùng context
/// in-memory riêng (CachePath rỗng) để không ghi gì xuống đĩa.
/// </summary>
public sealed class RequestContextFactory : IDisposable
{
    private IRequestContext? _guestContext;

    /// <summary>Context bản (profile mặc định) — dùng global context chia sẻ cache chung.</summary>
    public IRequestContext? GetDefaultContext() => null;

    /// <summary>Context in-memory (guest mode / chế độ khách).</summary>
    public IRequestContext GetGuestContext()
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

    /// <summary>Context cho Profile chỉ định — mọi profile dùng chung global context (cache chung ở root).</summary>
    public IRequestContext? GetProfileContext(string profileName) => null;

    public void Dispose()
    {
        _guestContext?.Dispose();
        _guestContext = null;
    }
}
