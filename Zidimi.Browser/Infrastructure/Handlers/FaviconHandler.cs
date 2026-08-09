using System.Collections.Generic;
using CefSharp;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Catches page favicon change events (spec 10.4 — IDisplayHandler.OnFaviconUrlChange).
/// Only extracts the favicon URL and raises an event so the UI can load the image asynchronously.
/// </summary>
public sealed class FaviconHandler : CefSharp.Handler.DisplayHandler
{
    public event System.Action<string>? FaviconUrlChanged;

    protected override void OnFaviconUrlChange(IWebBrowser browserControl, IBrowser browser,
        IList<string> urls)
    {
        if (urls == null || urls.Count == 0) return;
        // CEF provides multiple sizes; the last one is usually the larger version. Take the first to stay lightweight.
        var url = urls[0];
        FaviconUrlChanged?.Invoke(url);
    }
}
