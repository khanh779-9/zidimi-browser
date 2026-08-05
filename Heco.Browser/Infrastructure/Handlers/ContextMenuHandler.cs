using System.Windows;
using CefSharp;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Context menu chuột phải tùy biến (spec 11.2 — IContextMenuHandler).
/// Menu cơ bản: Back, Forward, Reload, Save as, Print, View source, Inspect, Copy.
/// </summary>
public sealed class ContextMenuHandler : IContextMenuHandler
{
    public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        IContextMenuParams parameters, IMenuModel model)
    {
        // Xoá menu mặc định CEF
        model.Clear();

        // Build menu items — chỉ dùng commands chắc chắn có trong CefSharp 150
        Add(model, (CefMenuCommand)26501, "Quay lại");           // Back
        Add(model, (CefMenuCommand)26502, "Tiến tới");           // Forward
        model.AddSeparator();

        Add(model, (CefMenuCommand)26503, "Tải lại trang");      // Reload
        model.AddSeparator();

        if (!string.IsNullOrEmpty(parameters.LinkUrl))
        {
            Add(model, (CefMenuCommand)26511, "Mở liên kết trong tab mới");              // OpenLinkInNewTab
            Add(model, (CefMenuCommand)26512, "Mở liên kết trong cửa sổ mới");           // OpenLinkInNewWindow
            Add(model, (CefMenuCommand)26513, "Sao chép địa chỉ liên kết");              // CopyLinkAddress
            model.AddSeparator();
        }

        if (parameters.HasImageContents)
        {
            Add(model, (CefMenuCommand)26514, "Lưu ảnh...");                             // SaveImageAs
            Add(model, (CefMenuCommand)26515, "Sao chép ảnh");                           // CopyImage
            Add(model, (CefMenuCommand)26516, "Sao chép địa chỉ ảnh");                   // CopyImageAddress
            model.AddSeparator();
        }

        if (parameters.SelectionText?.Length > 0)
        {
            Add(model, (CefMenuCommand)26504, "Sao chép");                               // Copy
            model.AddSeparator();
        }

        Add(model, (CefMenuCommand)26505, "Lưu trang dưới dạng...");                      // SavePageAs
        Add(model, (CefMenuCommand)26506, "In trang...");                                 // Print
        model.AddSeparator();

        Add(model, (CefMenuCommand)26507, "Xem nguồn trang");                             // ViewSource
        Add(model, (CefMenuCommand)26508, "Kiểm tra phần tử (DevTools)");                 // InspectElement
    }

    public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
    {
        // DevTools: lệnh InspectElement (26508) → ShowDevTools
        if ((int)commandId == 26508)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                browser.ShowDevTools();
            });
            return true;
        }
        return false;
    }

    public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame) { }

    public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
    {
        return false;
    }

    private static void Add(IMenuModel model, CefMenuCommand cmd, string label)
    {
        model.AddItem(cmd, label);
    }

    private static void AddIf(IMenuModel model, CefMenuCommand cmd, string label, bool condition)
    {
        if (condition) Add(model, cmd, label);
    }
}