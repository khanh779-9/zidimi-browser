using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CefSharp;
using Heco.Browser.Infrastructure.Handlers;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure;

public sealed class MainViewModel : ViewModelBase
{
    private PageId _activePage = PageId.Preferences;
    private TabViewModel? _activeTab;
    private Theme _theme = Theme.Dark;
    private string _searchFilter = "";
    private bool _isGuestMode;

    private readonly HistoryService _history;
    private readonly BookmarkService _bookmarks;
    private readonly Dictionary<TabViewModel, IWebBrowser> _browsers = new();
    private readonly object _browsersLock = new();

    public MainViewModel(HistoryService history, BookmarkService bookmarks)
    {
        _history = history;
        _bookmarks = bookmarks;

        NewTabCommand = new RelayCommand(_ => NewTab());
        CloseTabCommand = new RelayCommand(p =>
        {
            if (p is TabViewModel t) CloseTab(t);
        });
        SelectTabCommand = new RelayCommand(p =>
        {
            if (p is TabViewModel t) ActiveTab = t;
        });
        NavigateCommand = new RelayCommand(p =>
        {
            if (p is PageId id) ActivePage = id;
        });
        GoHomeCommand = new RelayCommand(_ => { ActivePage = PageId.Browser; });
        RemoveHistoryCommand = new RelayCommand(p =>
        {
            if (p is HistoryEntry e) _history.Remove(e);
        });
        ClearHistoryCommand = new RelayCommand(_ => _history.Clear());
        AddBookmarkCommand = new RelayCommand(p =>
        {
            if (p is ValueTuple<string, string> tuple)
                _bookmarks.Add(tuple.Item1, tuple.Item2);
            else if (p is string[] arr && arr.Length >= 2)
                _bookmarks.Add(arr[0], arr[1]);
        });
        RemoveBookmarkCommand = new RelayCommand(p =>
        {
            if (p is Bookmark b) _bookmarks.Remove(b);
        });
        ClearDownloadsCommand = new RelayCommand(_ => Downloads.Clear());

        // Tạo tab mặc định
        var startupBehavior = Heco.Browser.Models.AppSettings.Current.StartupBehavior;
        if (startupBehavior == 0) // Mở trang mới
        {
            NewTab("about:newtab");
        }
        else if (startupBehavior == 1) // Tiếp tục từ nơi đã dừng (stub, just open Home for now)
        {
            NewTab(Heco.Browser.Models.AppSettings.Current.HomePageUrl);
        }
        else // Mở tập trang cụ thể (stub)
        {
            NewTab(Heco.Browser.Models.AppSettings.Current.HomePageUrl);
        }
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ObservableCollection<HistoryEntry> History => _history.Entries;
    public ObservableCollection<Bookmark> Bookmarks => _bookmarks.Items;
    public ObservableCollection<DownloadEntry> Downloads { get; } = new();

    /// <summary>Đăng ký ChromiumWebBrowser cho tab (gọi từ BrowserView).</summary>
    public void RegisterBrowser(TabViewModel tab, IWebBrowser browser)
    {
        lock (_browsersLock)
        {
            _browsers[tab] = browser;
        }
    }

    public void UnregisterBrowser(TabViewModel tab)
    {
        lock (_browsersLock)
        {
            _browsers.Remove(tab);
        }
    }

    /// <summary>Lấy browser đã đăng ký cho tab (null nếu chưa có).</summary>
    public IWebBrowser? GetBrowser(TabViewModel tab)
    {
        lock (_browsersLock)
        {
            return _browsers.TryGetValue(tab, out var b) ? b : null;
        }
    }

    /// <summary>Thêm một entry vào lịch sử duyệt web.</summary>
    public void AddHistory(string url, string title) => _history.Add(url, title);

    public PageId ActivePage
    {
        get => _activePage;
        set => Set(ref _activePage, value);
    }

    public TabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (_activeTab != null)
                _activeTab.IsActive = false;
            if (Set(ref _activeTab, value) && value != null)
            {
                value.IsActive = true;
                ActivePage = PageId.Browser;
            }
        }
    }

    public Theme Theme
    {
        get => _theme;
        set => Set(ref _theme, value);
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set => Set(ref _searchFilter, value);
    }

    /// <summary>Chế độ khách (guest): không lưu dữ liệu nào sau khi đóng (spec 8.2).</summary>
    public bool IsGuestMode
    {
        get => _isGuestMode;
        private set => Set(ref _isGuestMode, value);
    }

    /// <summary>Lấy RequestContext cho tab mới theo profile hiện tại.
    /// Nếu là chế độ khách (GuestMode) thì dùng in-memory context.</summary>
    public CefSharp.IRequestContext? GetRequestContext()
        => IsGuestMode ? App.RequestContexts.GetGuestContext() : App.RequestContexts.GetProfileContext(Heco.Browser.Models.AppSettings.Current.CurrentProfile);

    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand GoHomeCommand { get; }
    public ICommand RemoveHistoryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand AddBookmarkCommand { get; }
    public ICommand RemoveBookmarkCommand { get; }
    public ICommand ClearDownloadsCommand { get; }

    /// <summary>Quick helper để thêm bookmark từ code-behind.</summary>
    public void AddBookmark(string url, string title) => _bookmarks.Add(url, title);

    public void NewTab(string url = "about:newtab")
    {
        var tab = new TabViewModel { Address = url, Title = "New Tab" };
        Tabs.Add(tab);
        ActiveTab = tab;
        ActivePage = PageId.Browser;
    }

    /// <summary>Mở app-tab nội bộ (Settings/History/Downloads/Bookmarks) trong tab strip.
    /// Nếu đã có app-tab cùng loại thì activate tab đó (không mở trùng). (spec 7.4)</summary>
    public void OpenAppTab(TabKind kind)
    {
        var existing = Tabs.FirstOrDefault(t => t.Kind == kind);
        if (existing != null)
        {
            ActiveTab = existing;
            return;
        }
        var tab = new TabViewModel { Kind = kind, Title = kind switch
        {
            TabKind.Settings => "Cài đặt",
            TabKind.History => "Lịch sử",
            TabKind.Bookmarks => "Bookmark",
            TabKind.Downloads => "Tải xuống",
            _ => "New Tab",
        } };
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>Di chuyển tab đến vị trí của tab đích (drag-reorder, spec 10.4).</summary>
    public void MoveTab(TabViewModel dragged, TabViewModel target)
    {
        var fromIdx = Tabs.IndexOf(dragged);
        var toIdx = Tabs.IndexOf(target);
        if (fromIdx < 0 || toIdx < 0 || fromIdx == toIdx) return;
        Tabs.Move(fromIdx, toIdx);
    }

    public void CloseTab(TabViewModel tab)
    {
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        Tabs.Remove(tab);
        if (ActiveTab == tab)
        {
            if (Tabs.Count == 0) { NewTab(); return; }
            ActiveTab = idx < Tabs.Count ? Tabs[idx] : Tabs[^1];
        }
    }

    /// <summary>Ghim/bỏ ghim tab. Ghim: tab thu nhỏ, đứng đầu danh sách (spec 10.4).</summary>
    public void TogglePinTab(TabViewModel tab)
    {
        tab.IsPinned = !tab.IsPinned;
        // Sắp xếp: tab ghim luôn nằm trước tab thường
        var pinned = Tabs.Where(t => t.IsPinned).ToList();
        var unpinned = Tabs.Where(t => !t.IsPinned).ToList();
        var reordered = pinned.Concat(unpinned).ToList();
        for (int i = 0; i < reordered.Count; i++)
            Tabs.Move(Tabs.IndexOf(reordered[i]), i);
    }

    public void ReloadTab(TabViewModel tab)
    {
        var browser = GetBrowser(tab);
        if (browser != null)
        {
            if (browser.IsLoading) browser.Stop();
            browser.Reload();
        }
    }

    public void DuplicateTab(TabViewModel tab)
    {
        var url = string.IsNullOrEmpty(tab.Address) ? "about:newtab" : tab.Address;
        var copy = new TabViewModel { Address = url, Title = tab.Title };
        var idx = Tabs.IndexOf(tab);
        if (idx >= 0) Tabs.Insert(idx + 1, copy);
        else Tabs.Add(copy);
        ActiveTab = copy;
    }

    public void ToggleTheme()
    {
        Theme = Theme == Theme.Dark ? Theme.Light : Theme.Dark;
        ApplyTheme(Theme);
    }

    /// <summary>Bật/tắt chế độ khách. Khi bật: đóng toàn bộ tab (không lưu) và mở tab mới in-memory.</summary>
    public void ToggleGuestMode()
    {
        IsGuestMode = !IsGuestMode;
        while (Tabs.Count > 0) CloseTab(Tabs[0]);
        NewTab();
    }

    public static void ApplyTheme(Theme theme)
    {
        // Heco Browser hiện chỉ có theme dark; để mở rộng lấy theme từ app resource.
        // (Placeholder cho light theme sau này)
    }
}
