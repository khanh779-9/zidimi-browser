using CefSharp;
using Heco.Browser.Controls;
using System.Windows;

namespace Heco.Browser.Infrastructure.Handlers;

public class JsDialogHandler : IJsDialogHandler
{
    public bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
    {
        // This is called on a CEF background thread. Switch to UI thread.
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var dialog = new HecoJsDialog
            {
                MessageText = messageText,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            switch (dialogType)
            {
                case CefJsDialogType.Alert:
                    dialog.DialogTitle = "Cảnh báo";
                    dialog.ShowCancel = false;
                    dialog.IsPrompt = false;
                    break;
                case CefJsDialogType.Confirm:
                    dialog.DialogTitle = "Xác nhận";
                    dialog.ShowCancel = true;
                    dialog.IsPrompt = false;
                    break;
                case CefJsDialogType.Prompt:
                    dialog.DialogTitle = "Trang web yêu cầu nhập thông tin";
                    dialog.ShowCancel = true;
                    dialog.IsPrompt = true;
                    dialog.InputText = defaultPromptText ?? "";
                    break;
            }

            // ShowDialog blocks the UI thread until the dialog is closed
            bool result = dialog.ShowDialog() == true;

            // Call the callback with the result
            if (result)
            {
                callback.Continue(success: true, userInput: dialog.InputText);
            }
            else
            {
                callback.Continue(success: false, userInput: string.Empty);
            }
        });

        // Return true to indicate we are handling the dialog asynchronously
        return true;
    }

    public bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
    {
        // Handle window.onbeforeunload (e.g. "You have unsaved changes")
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var dialog = new HecoJsDialog
            {
                DialogTitle = "Rời khỏi trang web?",
                MessageText = string.IsNullOrEmpty(messageText) ? "Bạn có thay đổi chưa lưu. Bạn có chắc chắn muốn rời khỏi trang này?" : messageText,
                ShowCancel = true,
                IsPrompt = false,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            bool result = dialog.ShowDialog() == true;
            callback.Continue(result, string.Empty);
        });

        return true;
    }

    public void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        // Clean up any state if needed
    }

    public void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        // Notification that a dialog was closed
    }
}
