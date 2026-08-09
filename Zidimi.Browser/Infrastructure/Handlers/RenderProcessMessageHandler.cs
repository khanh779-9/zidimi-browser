using System;
using CefSharp;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Watches the renderer process (spec 11.2 — IRenderProcessMessageHandler):
///   - OnFocusedNodeChanged: detects when focus moves into an input field on the page
///     (so the UI can respond, e.g. hide the find bar).
///   - OnUncaughtException / OnContextCreated: only log, never crash.
/// </summary>
public sealed class ZidimiRenderProcessMessageHandler : IRenderProcessMessageHandler
{
    /// <summary>Raises an event when focus moves into or out of an input field on the page.</summary>
    public event Action<bool>? EditableFocused;

    public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
    {
        // The V8 context has been created — safe to run JS if needed.
    }

    public void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
    {
        // Context released — JS can no longer run.
    }

    public void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
    {
        var isEditable = node != null &&
            (node.HasAttribute("contenteditable") || node.TagName == "INPUT" || node.TagName == "TEXTAREA");
        EditableFocused?.Invoke(isEditable);
    }

    public void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
    {
        System.Diagnostics.Debug.WriteLine($"[Zidimi JS] {frame.Url}: {exception?.Message}");
    }
}