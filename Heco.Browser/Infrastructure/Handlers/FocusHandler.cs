using CefSharp;
using CefSharp.Handler;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Handles browser focus (spec 11.2 — IFocusHandler).
/// When the tab is selected / the browser gets focus, keep focus on the web control so keyboard
/// input and page interactions work properly. Don't block focus, so return false.
/// </summary>
public sealed class HecoFocusHandler : CefSharp.Handler.FocusHandler
{
    protected override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        // The browser received focus — nothing more to do (ensure focus actually transfers into the webview).
    }

    protected override bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
    {
        // Let CEF handle focus from any source (navigation, system).
        return false;
    }

    protected override void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next)
    {
        // When leaving the webview (Tab out), focus can be moved back to the omnibox if needed.
        // Leave it as is by default; the main UI handles tab navigation behavior.
    }
}