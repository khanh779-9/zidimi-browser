using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;
using Heco.Browser.Views;

namespace Heco.Browser;

public partial class MainWindow : HecoWindow
{
    private readonly MainViewModel _vm;
    private readonly Dictionary<PageId, UserControl> _pages = new();

    public MainWindow()
    {
InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;

        _vm.PropertyChanged += OnVmPropertyChanged;

        SwitchPage(_vm.ActivePage);

        // Apply the default FontSize from AppSettings to the whole UI at startup.
        UpdateAppFontSize();

        Closing += OnMainWindowClosing;
    }

    private void UpdateAppFontSize()
    {
        FontSize = Models.AppSettings.Profile.FontSize;
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
// "RunInBackground": hide the window instead of exiting so CEF keeps running in the background,
        // and show the system tray icon so the user can reopen it or quit completely.
        if (Models.AppSettings.Global.RunInBackground)
        {
            e.Cancel = true;
            Hide();
            App.TrayIcon?.Show();
        }
    }

private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActivePage) || string.IsNullOrEmpty(e.PropertyName))
            SwitchPage(_vm.ActivePage);
    }

    private void SwitchPage(PageId id)
    {
        PageHost.Content = GetPage(id);
    }

    /// <summary>Get the page lazily on demand — avoids creating BrowserView (and dragging along the loading of
    /// CefSharp/native CEF) right when the window opens, so the window appears quickly.</summary>
    private UserControl GetPage(PageId id)
    {
        _pages.TryGetValue(id, out var page);
        return page ??= CreatePage(id);
    }

    private static UserControl CreatePage(PageId id) => id switch
    {
        PageId.Browser => new BrowserView(),
        PageId.History => new HistoryView(),
        PageId.Bookmarks => new BookmarksView(),
        PageId.Preferences => new PreferencesView(),
        PageId.Downloads => new DownloadsView(),
        _ => new UserControl(),
    };

    public void OpenTabSearch()
    {
        TheTabStrip?.OpenTabSearch();
    }
}

