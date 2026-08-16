using System.Windows;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Catches window.open / target=_blank events to open a new tab in the UI
/// instead of letting CEF open the default Chromium window (spec 11.2 — ILifeSpanHandler).
/// </summary>
public sealed class LifeSpanHandler : ILifeSpanHandler
{
    private readonly TabViewModel _owner;

    public LifeSpanHandler(TabViewModel owner) => _owner = owner;

    public bool DoClose(IWebBrowser browserControl, IBrowser browser) => false;

    public void OnAfterCreated(IWebBrowser browserControl, IBrowser browser) { }

    public void OnBeforeClose(IWebBrowser browserControl, IBrowser browser) { }

    public bool OnBeforePopup(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        string targetUrl, string targetFrameName,
        WindowOpenDisposition targetDisposition, bool userGesture,
        IPopupFeatures popupFeatures, IWindowInfo windowInfo,
        IBrowserSettings browserSettings, ref bool noJavascriptAccess,
        out IWebBrowser? newBrowser)
    {
        newBrowser = null;
        if (string.IsNullOrEmpty(targetUrl))
            return false;

        // Extension action/default_popup windows belong to Chromium's extension runtime.
        // Do not redirect them into a normal Zidimi tab and do not apply the site's popup
        // blocker to them; Chrome runtime needs to create/own this popup itself.
        if (targetUrl.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            return false;

        // When pop-ups are blocked entirely by the profile's site settings, drop the request.
        if (Models.AppSettings.Profile.SitePermissions.BlockPopups)
            return true;

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            App.ViewModel.NewTab(targetUrl);
        });
        return true;
    }
}
