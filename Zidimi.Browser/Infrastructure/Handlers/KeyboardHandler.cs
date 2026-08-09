using CefSharp;
using System.Windows.Input;
using System.Windows;

namespace Zidimi.Browser.Infrastructure.Handlers;

public class KeyboardHandler : IKeyboardHandler
{
    // CefSharp calls these on a CEF background thread.
    // We need to use Dispatcher to interact with WPF UI.

    public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
    {
        // Only handle key down events
        if (type != KeyType.RawKeyDown)
        {
            return false;
        }

        bool isCtrlDown = modifiers.HasFlag(CefEventFlags.ControlDown);
        bool isShiftDown = modifiers.HasFlag(CefEventFlags.ShiftDown);

        // Ctrl + T (New Tab)
        if (windowsKeyCode == (int)Key.T && isCtrlDown)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                App.ViewModel.NewTabCommand.Execute(null);
            });
            return true; // Handled
        }

        // Ctrl + W (Close Tab)
        if (windowsKeyCode == (int)Key.W && isCtrlDown)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var activeTab = App.ViewModel.ActiveTab;
                if (activeTab != null)
                {
                    App.ViewModel.CloseTabCommand.Execute(activeTab);
                }
            });
            return true; // Handled
        }

        // F5 or Ctrl + R (Reload)
        if (windowsKeyCode == (int)Key.F5 || (windowsKeyCode == (int)Key.R && isCtrlDown))
        {
            browser.Reload(ignoreCache: isShiftDown);
            return true; // Handled
        }

        // F12 (DevTools)
        if (windowsKeyCode == (int)Key.F12)
        {
            browser.ShowDevTools();
            return true; // Handled
        }

        // Ctrl + L (Focus Address Bar) - Tricky because AddressBar is in BrowserView
        if (windowsKeyCode == (int)Key.L && isCtrlDown)
        {
            // Just request focus on the MainWindow, and the view will handle it if we route an event,
            // or we just let WPF bindings do it. For simplicity, we can route a custom command.
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // This is a simplified way to focus address bar. A robust way requires routing to BrowserView.
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    // Focus the window, which might restore focus to the address bar if configured.
                    mw.Focus();
                }
            });
            return false; // Let it pass so it might trigger default behavior
        }

        return false;
    }

    public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
    {
        return false;
    }
}
