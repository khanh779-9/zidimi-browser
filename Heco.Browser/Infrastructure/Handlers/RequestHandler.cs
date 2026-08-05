using CefSharp;
using CefSharp.Handler;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace Heco.Browser.Infrastructure.Handlers;

public class RequestHandler : CefSharp.Handler.RequestHandler
{
    protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
    {
        // For development/testing, we might allow it. For production, we should block it or show a warning.
        // Returning false means we do NOT handle the error, and the default behavior (canceling the request) happens.
        
        // Example: Show a dialog if they want to proceed anyway
        // Warning: This blocks the CEF UI thread if not careful. CefSharp recommends using callback.Continue() asynchronously.
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var result = MessageBox.Show(
                $"Trang web {requestUrl} có chứng chỉ bảo mật không hợp lệ ({errorCode}).\n\nBạn có muốn tiếp tục truy cập không? (Không khuyến nghị)",
                "Cảnh báo bảo mật",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
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
