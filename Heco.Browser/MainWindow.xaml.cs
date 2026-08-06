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

        BuildPages();

        _vm.PropertyChanged += OnVmPropertyChanged;

        SwitchPage(_vm.ActivePage);

        // Áp dụng FontSize mặc định từ AppSettings cho toàn UI khi khởi động.
        UpdateAppFontSize();

        Closing += OnMainWindowClosing;
    }

    private void UpdateAppFontSize()
    {
        FontSize = Models.AppSettings.Current.FontSize;
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        // NOTE: "RunInBackground" sẽ ẩn window thay vì thoát để CEF chạy nền.
        // Hiện chưa có system tray icon để gọi lại window, nên tạm thời skip để tránh app "mất tích".
        // Khi nào có tray icon, bỏ comment đoạn dưới.
        // if (Models.AppSettings.Current.RunInBackground) { e.Cancel = true; Hide(); }
    }

    private void BuildPages()
    {
        _pages[PageId.Browser] = new BrowserView();
        _pages[PageId.History] = new HistoryView();
        _pages[PageId.Bookmarks] = new BookmarksView();
        _pages[PageId.Preferences] = new PreferencesView();
        _pages[PageId.Downloads] = new DownloadsView();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActivePage) || string.IsNullOrEmpty(e.PropertyName))
            SwitchPage(_vm.ActivePage);
    }

    private void SwitchPage(PageId id)
    {
        if (!_pages.TryGetValue(id, out var page)) return;
        PageHost.Content = page;
    }
}
