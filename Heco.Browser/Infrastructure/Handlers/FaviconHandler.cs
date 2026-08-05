using System.Collections.Generic;
using CefSharp;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Bắt sự kiện favicon đổi trang (spec 10.4 — IDisplayHandler.OnFaviconUrlChange).
/// Chỉ lấy URL favicon rồi raise event để UI tải ảnh bất đồng bộ.
/// </summary>
public sealed class FaviconHandler : CefSharp.Handler.DisplayHandler
{
    public event System.Action<string>? FaviconUrlChanged;

    protected override void OnFaviconUrlChange(IWebBrowser browserControl, IBrowser browser,
        IList<string> urls)
    {
        if (urls == null || urls.Count == 0) return;
        // CEF đưa nhiều size; ưu tiên cái cuối (thường là bản lớn hơn). Lấy cái đầu để nhẹ.
        var url = urls[0];
        FaviconUrlChanged?.Invoke(url);
    }
}
