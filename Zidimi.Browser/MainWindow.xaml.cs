using System.Windows;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Views;

namespace Zidimi.Browser;

public partial class MainWindow : ZidimiWindow
{
    private BrowserView? _browserView;
    private bool _browserHostAttached;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ViewModel;
        FontSize = 14;
    }

    public bool HasBrowserHost => _browserHostAttached && _browserView != null;

    public void SetStartupStatus(string status, string? detail = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SetStartupStatus(status, detail));
            return;
        }

        StartupLayer.Visibility = Visibility.Visible;
        BrowserLayer.Visibility = Visibility.Collapsed;
        TheTabStrip.Visibility = Visibility.Collapsed;
        StartupStatusText.Text = status;
        StartupDetailText.Text = detail ?? string.Empty;
        StartupProgressBar.IsIndeterminate = true;
    }

    public void SetStartupReady(string status)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => SetStartupReady(status));
            return;
        }

        StartupStatusText.Text = status;
        StartupProgressBar.IsIndeterminate = false;
        StartupProgressBar.Minimum = 0;
        StartupProgressBar.Maximum = 1;
        StartupProgressBar.Value = 1;
    }

    /// <summary>
    /// Switches from the startup layer to the browser layer and only then constructs BrowserView.
    /// HwndHost is therefore never created underneath a WPF loading overlay (WPF airspace would
    /// allow the native child HWND to paint above that overlay).
    /// </summary>
    public void AttachBrowserHost()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(AttachBrowserHost);
            return;
        }

        StartupLayer.Visibility = Visibility.Collapsed;
        BrowserLayer.Visibility = Visibility.Visible;
        TheTabStrip.Visibility = Visibility.Visible;

        if (_browserHostAttached) return;

        _browserView = new BrowserView();
        PageHost.Content = _browserView;
        _browserHostAttached = true;
    }

    public void OpenTabSearch()
    {
        TheTabStrip?.OpenTabSearch();
    }

    internal void DisposeBrowserHost()
    {
        if (_browserView == null) return;

        _browserView.Dispose();
        PageHost.Content = null;
        _browserView = null;
        _browserHostAttached = false;
    }
}
