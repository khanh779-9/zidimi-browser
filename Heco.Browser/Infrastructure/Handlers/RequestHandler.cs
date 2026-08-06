using CefSharp;
using CefSharp.Handler;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using Heco.Browser.Controls;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure.Handlers;

public class RequestHandler : CefSharp.Handler.RequestHandler
{
    protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        IRequest request, bool userGesture, bool isRedirect)
    {
        if (AppSettings.Current.SendDoNotTrack && !request.IsReadOnly)
        {
            // request.Headers là NameValueCollection read-only trong OnBeforeBrowse —
            // dùng SetHeaderByName (native, không qua collection).
            var existing = request.GetHeaderByName("DNT");
            if (string.IsNullOrEmpty(existing))
                request.SetHeaderByName("DNT", "1", overwrite: true);
        }
        return false;
    }

    protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
    {
        // Safe Browsing: Nếu người dùng tắt cảnh báo trang nguy hiểm, không hiển thị dialog và chặn luôn.
        if (!AppSettings.Current.WarnDangerousSites)
        {
            callback.Continue(false);
            return true;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var result = HecoMessageBox.Show(
                $"Trang web {requestUrl} có chứng chỉ bảo mật không hợp lệ ({errorCode}).\n\nBạn có muốn tiếp tục truy cập không? (Không khuyến nghị)",
                "Cảnh báo bảo mật",
                HecoMessageBoxButton.YesNo,
                HecoMessageBoxImage.Warning);

            if (result == HecoMessageBoxResult.Yes)
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
