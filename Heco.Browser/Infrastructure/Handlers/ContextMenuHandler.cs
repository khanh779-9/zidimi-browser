using System.Windows;
using CefSharp;
using Heco.Browser.Infrastructure;

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

        var l = LanguageManager.Instance;

        // Link context
        if (!string.IsNullOrEmpty(parameters.LinkUrl))
        {
            Add(model, CustomOpenLinkNewTab, l["Ctx_OpenLinkNewTab"]);
            Add(model, CustomCopyLinkAddress, l["Ctx_CopyLinkAddress"]);
            Add(model, CustomSaveLinkAs, l["Ctx_SaveLinkAs"]);
            model.AddSeparator();
        }

        // Image context
        if (parameters.HasImageContents)
        {
            Add(model, CustomSaveImageAs, l["Ctx_SaveImageAs"]);
            Add(model, CustomCopyImageAddress, l["Ctx_CopyImageAddress"]);
            model.AddSeparator();
        }

        // Text selection
        if (parameters.SelectionText?.Length > 0)
        {
            Add(model, CefMenuCommand.Copy, l["Ctx_Copy"]);
            model.AddSeparator();
        }

        Add(model, CefMenuCommand.Back, l["Ctx_Back"]);
        Add(model, CefMenuCommand.Forward, l["Ctx_Forward"]);
        Add(model, CefMenuCommand.Reload, l["Ctx_Reload"]);
        model.AddSeparator();

        Add(model, CefMenuCommand.Print, l["Ctx_Print"]);
        Add(model, CefMenuCommand.ViewSource, l["Ctx_ViewSource"]);
        model.AddSeparator();

        Add(model, CustomInspectElement, l["Ctx_InspectElement"]);
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
