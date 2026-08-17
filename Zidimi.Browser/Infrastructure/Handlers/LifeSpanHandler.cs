using System;
using System.Windows;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Owns all popup/new-window requests created by Chromium content.
///
/// CEF's default behaviour is to create a standalone native Chromium window when
/// <see cref="OnBeforePopup"/> returns false. Zidimi deliberately never falls back to that
/// behaviour: user-requested popups are converted to normal Zidimi tabs and unrequested
/// popups are blocked when the profile popup blocker is enabled.
/// </summary>
public sealed class LifeSpanHandler : ILifeSpanHandler
{
    private readonly Action<string>? _openInApp;
    private readonly string _sourceName;
    private readonly Action<IBrowser>? _browserCreated;
    private readonly Action<IBrowser>? _browserClosed;

    public LifeSpanHandler(
        Action<string>? openInApp = null,
        string sourceName = "Browser",
        Action<IBrowser>? browserCreated = null,
        Action<IBrowser>? browserClosed = null)
    {
        _openInApp = openInApp;
        _sourceName = sourceName;
        _browserCreated = browserCreated;
        _browserClosed = browserClosed;
    }

    public bool DoClose(IWebBrowser browserControl, IBrowser browser)
    {
        // Use CEF's normal browser-close lifecycle. Returning true here means the application
        // handled the close itself, which Zidimi does not do for these hosted browser controls.
        return false;
    }

    public void OnAfterCreated(IWebBrowser browserControl, IBrowser browser)
    {
        _browserCreated?.Invoke(browser);
    }

    public void OnBeforeClose(IWebBrowser browserControl, IBrowser browser)
    {
        _browserClosed?.Invoke(browser);
    }

    public bool OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        string targetUrl, string targetFrameName,
        WindowOpenDisposition targetDisposition, bool userGesture,
        IPopupFeatures popupFeatures, IWindowInfo windowInfo,
        IBrowserSettings browserSettings, ref bool noJavascriptAccess,
        out IWebBrowser? newBrowser)
    {
        // IMPORTANT: when returning true (cancel), CefSharp requires newBrowser to be null.
        newBrowser = null;

        // The profile option is described as blocking *unrequested* popups. Keep explicit
        // user gestures working, but never let them escape into CEF's default native window.
        if (AppSettings.Profile.SitePermissions.BlockPopups && !userGesture)
        {
            AppLogger.Log("PopupRouter",
                $"Blocked unrequested popup. Source={_sourceName}; Url={targetUrl}; Disposition={targetDisposition}");
            return true;
        }

        // targetUrl is allowed to be null/empty in CEF. Returning false in that case is exactly
        // what used to leak an unmanaged native Chromium window. Use a Zidimi-owned blank tab
        // instead so every popup path remains inside the application shell.
        var routedUrl = string.IsNullOrWhiteSpace(targetUrl) ? "about:blank" : targetUrl.Trim();
        RouteToApplication(routedUrl);

        AppLogger.Log("PopupRouter",
            $"Routed popup to Zidimi tab. Source={_sourceName}; Url={routedUrl}; " +
            $"Disposition={targetDisposition}; UserGesture={userGesture}");

        // Always cancel CEF's top-level popup creation. This is the key invariant that prevents
        // standalone native CEF/Chromium windows from appearing next to the Zidimi window.
        return true;
    }

    private void RouteToApplication(string url)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return;

        dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_openInApp != null)
                    _openInApp(url);
                else if (App.ViewModel != null)
                    App.ViewModel.NewTab(url);
            }
            catch (Exception ex)
            {
                AppLogger.Log("PopupRouter", ex, $"Failed to route popup: {url}");
            }
        }));
    }
}
