using CefSharp;
using CefSharp.Handler;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

public class RequestHandler : CefSharp.Handler.RequestHandler
{
protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        IRequest request, bool userGesture, bool isRedirect)
    {
        // DNT is handled natively by CefSharp via enable_do_not_track preference.
        return false;
    }

    protected override void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser,
        CefTerminationStatus status, int errorCode, string errorMessage)
    {
        try
        {
            AppLogger.Log("CefCrash",
                $"Renderer process terminated. Status={status}, ErrorCode={errorCode}, Error={errorMessage}, BrowserId={browser?.Identifier}, Url={browser?.MainFrame?.Url}");
        }
        catch { }
    }

    protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
    {
        bool warn = true;
        var ctx = chromiumWebBrowser?.GetBrowserHost()?.RequestContext;
        if (ctx != null)
        {
            if (ctx.GetPreferenceSafe("safebrowsing.enabled") is bool sb) warn = sb;
        }

        // Safe Browsing: if the user disabled dangerous-site warnings, don't show a dialog and just block.
        if (!warn)
        {
            callback.Continue(false);
            return true;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var msg = string.Format(LanguageManager.Instance["Security_CertWarning"],
                requestUrl, errorCode);
            var result = ZidimiMessageBox.Show(
                msg,
                LanguageManager.Instance["Security_CertTitle"],
                ZidimiMessageBoxButton.YesNo,
                ZidimiMessageBoxImage.Warning);

            if (result == ZidimiMessageBoxResult.Yes)
            {
                callback.Continue(true);
            }
            else
            {
                callback.Continue(false);
            }
        });

        // Return true to indicate we are handling it asynchronously with the callback
        return true; 
    }
}

