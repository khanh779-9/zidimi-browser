using System.Windows;
using CefSharp;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Context menu chuột phải tùy biến (spec 11.2 — IContextMenuHandler).
/// Dùng đúng CefMenuCommand chuẩn (Back=100, Forward=101, Reload=102, Copy=113,
/// Print=131, ViewSource=132) để CEF tự xử lý; các action tuỳ chỉnh (mở link tab mới,
/// sao chép link, lưu ảnh, DevTools) dùng ID trong vùng UserFirst và xử lý thủ công.
/// </summary>
public sealed class ContextMenuHandler : IContextMenuHandler
{
    private const int CustomOpenLinkNewTab = 26500;
    private const int CustomCopyLinkAddress = 26501;
    private const int CustomSaveLinkAs = 26502;
    private const int CustomSaveImageAs = 26503;
    private const int CustomCopyImageAddress = 26504;
    private const int CustomInspectElement = 26505;

    public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        IContextMenuParams parameters, IMenuModel model)
    {
        // Xoá menu mặc định CEF
        model.Clear();

        // Link context
        if (!string.IsNullOrEmpty(parameters.LinkUrl))
        {
            Add(model, CustomOpenLinkNewTab, "Mở liên kết trong tab mới");
            Add(model, CustomCopyLinkAddress, "Sao chép địa chỉ liên kết");
            Add(model, CustomSaveLinkAs, "Lưu liên kết thành...");
            model.AddSeparator();
        }

        // Image context
        if (parameters.HasImageContents)
        {
            Add(model, CustomSaveImageAs, "Lưu ảnh thành...");
            Add(model, CustomCopyImageAddress, "Sao chép địa chỉ ảnh");
            model.AddSeparator();
        }

        // Text selection
        if (parameters.SelectionText?.Length > 0)
        {
            Add(model, CefMenuCommand.Copy, "Sao chép");
            model.AddSeparator();
        }

        Add(model, CefMenuCommand.Back, "Quay lại");
        Add(model, CefMenuCommand.Forward, "Tiến tới");
        Add(model, CefMenuCommand.Reload, "Tải lại trang");
        model.AddSeparator();

        Add(model, CefMenuCommand.Print, "In trang...");
        Add(model, CefMenuCommand.ViewSource, "Xem nguồn trang");
        model.AddSeparator();

        Add(model, CustomInspectElement, "Kiểm tra phần tử (DevTools)");
    }

    public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame,
        IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
    {
        var cmd = (int)commandId;

        switch (cmd)
        {
            case CustomOpenLinkNewTab:
                if (!string.IsNullOrEmpty(parameters.LinkUrl))
                    Application.Current?.Dispatcher.BeginInvoke(() => App.ViewModel.NewTab(parameters.LinkUrl));
                return true;

            case CustomCopyLinkAddress:
                if (!string.IsNullOrEmpty(parameters.LinkUrl))
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        Clipboard.SetText(parameters.UnfilteredLinkUrl ?? parameters.LinkUrl));
                return true;

            case CustomSaveLinkAs:
                if (!string.IsNullOrEmpty(parameters.LinkUrl))
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        browserControl.StartDownload(parameters.LinkUrl));
                return true;

            case CustomSaveImageAs:
                if (!string.IsNullOrEmpty(parameters.SourceUrl))
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        browserControl.StartDownload(parameters.SourceUrl));
                return true;

            case CustomCopyImageAddress:
                if (!string.IsNullOrEmpty(parameters.SourceUrl))
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                        Clipboard.SetText(parameters.SourceUrl));
                return true;

            case CustomInspectElement:
                Application.Current?.Dispatcher.BeginInvoke(() =>
                    browserControl.ShowDevTools());
                return true;

            default:
                // Các command chuẩn (Back/Forward/Reload/Copy/Print/ViewSource) do CEF tự xử lý.
                return false;
        }
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

    private static void Add(IMenuModel model, int userCommand, string label)
    {
        model.AddItem((CefMenuCommand)userCommand, label);
    }
}
