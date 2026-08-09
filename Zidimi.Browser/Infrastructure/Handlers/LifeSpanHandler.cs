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
