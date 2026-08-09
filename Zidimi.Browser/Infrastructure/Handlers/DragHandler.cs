using System;
using System.Windows;
using CefSharp;
using CefSharp.Handler;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Handles dragging (spec 11.2 — IDragHandler).
/// When a link (URL) is dragged from outside/inside onto the browser → open the link in a new tab.
/// Files dragged in are left to the webview's default handling (upload).
/// </summary>
public sealed class ZidimiDragHandler : CefSharp.Handler.DragHandler
{
    protected override bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser,
        IDragData dragData, CefSharp.Enums.DragOperationsMask mask)
    {
        if (dragData.IsLink && !string.IsNullOrWhiteSpace(dragData.LinkUrl))
        {
            var url = dragData.LinkUrl;
            Application.Current?.Dispatcher.BeginInvoke(() => App.ViewModel.NewTab(url));
            return true; // cancel default drag – already opened a new tab ourselves
        }

        // File / fragment → leave to the webview's default handling.
        return false;
    }
}