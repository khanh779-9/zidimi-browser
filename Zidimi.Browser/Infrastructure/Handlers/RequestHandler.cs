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
    private DateTime _lastRendererRecoveryUtc = DateTime.MinValue;
    private int _rendererRecoveryBurst;

    protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        IRequest request, bool userGesture, bool isRedirect)
    {
        return false;
    }

    protected override void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser,
        CefTerminationStatus status, int errorCode, string errorMessage)
    {
        string url = string.Empty;
        try { url = browser?.MainFrame?.Url ?? string.Empty; } catch { }

        AppLogger.Log("CefCrash",
            $"Renderer terminated. Status={status}, ErrorCode={errorCode}, Error={errorMessage}, BrowserId={browser?.Identifier}, Url={url}");

        // A dead renderer leaves the WPF shell alive but the browser surface frozen.
        // Recover once or twice automatically; avoid an endless crash/reload loop.
        var now = DateTime.UtcNow;
        if ((now - _lastRendererRecoveryUtc) > TimeSpan.FromSeconds(20))
            _rendererRecoveryBurst = 0;

        _lastRendererRecoveryUtc = now;
        _rendererRecoveryBurst++;

        if (_rendererRecoveryBurst > 2)
        {
            AppLogger.Log("CefCrash", "Renderer recovery stopped after repeated crashes within 20 seconds.");
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (chromiumWebBrowser == null || chromiumWebBrowser.IsDisposed) return;

                AppLogger.Log("CefCrash", $"Recovering renderer by reloading. Attempt={_rendererRecoveryBurst}, Url={url}");
                if (!string.IsNullOrWhiteSpace(url))
                    chromiumWebBrowser.Load(url);
                else
                    chromiumWebBrowser.Reload();
            }
            catch (Exception ex)
            {
                AppLogger.Log("CefCrash", ex, "Renderer recovery failed.");
            }
        });
    }

    protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser,
        CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
    {
        bool warn = true;
        var ctx = chromiumWebBrowser?.GetBrowserHost()?.RequestContext;
        if (ctx != null)
        {
            if (ctx.GetPreferenceSafe("safebrowsing.enabled") is bool sb) warn = sb;
        }

        if (!warn)
        {
            callback.Continue(false);
            return true;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            using (callback)
            {
                if (callback.IsDisposed) return;

                var msg = string.Format(LanguageManager.Instance["Security_CertWarning"],
                    requestUrl, errorCode);
                var result = ZidimiMessageBox.Show(
                    msg,
                    LanguageManager.Instance["Security_CertTitle"],
                    ZidimiMessageBoxButton.YesNo,
                    ZidimiMessageBoxImage.Warning);

                callback.Continue(result == ZidimiMessageBoxResult.Yes);
            }
        });

        return true;
    }
}
