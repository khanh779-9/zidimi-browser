using System.Windows;
using CefSharp;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Bắt sự kiện window.open / target=_blank để mở tab mới trong UI
/// thay vì để CEF mở cửa sổ Chromium mặc định (spec 11.2 — ILifeSpanHandler).
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

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            App.ViewModel.NewTab(targetUrl);
        });
        return true;
    }
}
