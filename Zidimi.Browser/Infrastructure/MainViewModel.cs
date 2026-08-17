using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly HistoryService _history;
    private readonly BookmarkService _bookmarks;
    private readonly DownloadService _downloads;
    private readonly Dictionary<TabViewModel, IWebBrowser> _browsers = new();
    private readonly Dictionary<int, IWebBrowser> _browsersByTabId = new();
    private readonly Dictionary<int, TabViewModel> _tabsById = new();
    private readonly object _browsersLock = new();

    private TabViewModel? _activeTab;
    private int _activeTabId;
    private string _searchFilter = "";
    private bool _isGuestMode;

    public MainViewModel(HistoryService history, BookmarkService bookmarks, DownloadService downloads)
    {
        _history = history;
        _bookmarks = bookmarks;
        _downloads = downloads;

        NewTabCommand = new RelayCommand(_ => NewTab());
        CloseTabCommand = new RelayCommand(p =>
        {
            if (p is int tabId) CloseTabById(tabId);
            else if (p is TabViewModel tab) CloseTab(tab);
        });
        SelectTabCommand = new RelayCommand(p =>
        {
            if (p is int tabId) SelectTabById(tabId);
            else if (p is TabViewModel tab) ActiveTab = tab;
        });

        OpenStartupTabs();
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ObservableCollection<HistoryEntry> History => _history.Entries;
    public ObservableCollection<Bookmark> Bookmarks => _bookmarks.Items;
    public ObservableCollection<DownloadEntry> Downloads => _downloads.Entries;

    public TabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value)) return;

            if (_activeTab != null)
                _activeTab.IsActive = false;

            if (!Set(ref _activeTab, value)) return;

            if (value != null)
                value.IsActive = true;

            // The shell still keeps the TabViewModel for WPF binding, but web-tab identity is
            // native-first: once initialized, ActiveTabId is the same CEF/extension tabId used
            // everywhere else. Zidimi-native pages intentionally use 0.
            ActiveTabId = value?.TabId ?? 0;
        }
    }

    public int ActiveTabId
    {
        get => _activeTabId;
        private set => Set(ref _activeTabId, value);
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set => Set(ref _searchFilter, value);
    }

    /// <summary>
    /// Uses a temporary, in-memory CEF request context. Zidimi history is not recorded
    /// while guest mode is active.
    /// </summary>
    public bool IsGuestMode
    {
        get => _isGuestMode;
        private set => Set(ref _isGuestMode, value);
    }

    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SelectTabCommand { get; }

    public Task InitializeProfileDataAsync()
        => Task.WhenAll(_history.InitializeAsync(), _bookmarks.InitializeAsync(), _downloads.InitializeAsync());

    public void AddDownload(DownloadEntry entry) => _downloads.Add(entry);

    public void UpdateDownload(DownloadEntry entry) => _downloads.Update(entry);

    public void RecordHistory(string url, string title)
    {
        if (!IsGuestMode)
            _history.Add(url, title);
    }

    public void RegisterBrowser(TabViewModel tab, IWebBrowser browser)
    {
        lock (_browsersLock)
        {
            _browsers[tab] = browser;
            if (tab.TabId > 0)
            {
                _browsersByTabId[tab.TabId] = browser;
                _tabsById[tab.TabId] = tab;
            }
        }
    }

    /// <summary>
    /// Binds the WPF tab model to the real native CEF browser id. From this point the same TabId
    /// is used by Zidimi's TabStrip and Chromium extension APIs.
    /// </summary>
    public void BindBrowserTabId(TabViewModel tab, int tabId, IWebBrowser browser)
    {
        if (tabId <= 0) return;
        var isActive = ReferenceEquals(ActiveTab, tab);

        lock (_browsersLock)
        {
            if (tab.TabId > 0 && tab.TabId != tabId)
            {
                _browsersByTabId.Remove(tab.TabId);
                _tabsById.Remove(tab.TabId);
            }

            tab.TabId = tabId;
            _browsers[tab] = browser;
            _browsersByTabId[tabId] = browser;
            _tabsById[tabId] = tab;
        }

        if (isActive) ActiveTabId = tabId;
    }

    public void UnregisterBrowser(TabViewModel tab)
    {
        var isActive = ReferenceEquals(ActiveTab, tab);
        lock (_browsersLock)
        {
            _browsers.Remove(tab);
            if (tab.TabId > 0)
            {
                _browsersByTabId.Remove(tab.TabId);
                _tabsById.Remove(tab.TabId);
            }
            tab.TabId = 0;
        }

        if (isActive) ActiveTabId = 0;
    }

    public IWebBrowser? GetBrowser(TabViewModel tab)
    {
        lock (_browsersLock)
        {
            return _browsers.TryGetValue(tab, out var browser) ? browser : null;
        }
    }

    public IWebBrowser? GetBrowser(int tabId)
    {
        if (tabId <= 0) return null;
        lock (_browsersLock)
        {
            return _browsersByTabId.TryGetValue(tabId, out var browser) ? browser : null;
        }
    }

    public TabViewModel? GetTabById(int tabId)
    {
        if (tabId <= 0) return null;
        lock (_browsersLock)
        {
            return _tabsById.TryGetValue(tabId, out var tab) ? tab : null;
        }
    }

    public void SelectTabById(int tabId)
    {
        var tab = GetTabById(tabId);
        if (tab != null) ActiveTab = tab;
    }

    public void CloseTabById(int tabId)
    {
        var tab = GetTabById(tabId);
        if (tab != null) CloseTab(tab);
    }

    public void MoveTabById(int draggedTabId, int targetTabId)
    {
        var dragged = GetTabById(draggedTabId);
        var target = GetTabById(targetTabId);
        if (dragged != null && target != null) MoveTab(dragged, target);
    }

    /// <summary>
    /// Switches the current profile and recreates tabs, because existing browser instances
    /// retain the request context they were created with.
    /// </summary>
    public void SwitchProfile(string profileName)
    {
        var profileId = UserDataPaths.NormalizeProfileId(profileName);
        IsGuestMode = false;

        _history.SwitchProfile(profileId);
        _bookmarks.SwitchProfile(profileId);
        _downloads.SwitchProfile(profileId);

        // Extension metadata/runtime is loaded when an extension surface or Chromium
        // browser actually needs it. Avoid scanning manifests on the UI thread here.
        ResetTabsForBrowsingContext();
        App.RequestContexts?.ResetGuestContext();
    }

    /// <summary>
    /// Returns the request context for a new browser tab. Guest tabs use an in-memory context.
    /// </summary>
    public IRequestContext? GetRequestContext()
        => IsGuestMode
            ? App.RequestContexts?.GetGuestContext()
            : App.RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile);

    public void NewTab(string url = "chrome://newtab/")
    {
        var tab = new TabViewModel
        {
            Address = string.IsNullOrWhiteSpace(url) ? "chrome://newtab/" : url,
            Title = "New Tab",
        };

        Tabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>
    /// Opens a Zidimi native page in a browser tab. Internal pages keep the normal
    /// browser toolbar and expose a real zidimi:// URL in the omnibox.
    /// </summary>
    public void OpenAppTab(TabKind kind)
    {
        var chromiumUrl = kind switch
        {
            TabKind.History => "chrome://history/",
            TabKind.Bookmarks => "chrome://bookmarks/",
            TabKind.Downloads => "chrome://downloads/",
            TabKind.Extensions => "chrome://extensions/",
            _ => null,
        };
        if (chromiumUrl != null)
        {
            var existingNative = Tabs.FirstOrDefault(tab => tab.Kind == TabKind.Web &&
                string.Equals(tab.Address?.TrimEnd('/'), chromiumUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (existingNative != null) ActiveTab = existingNative;
            else NewTab(chromiumUrl);
            return;
        }

        if (kind == TabKind.Web)
        {
            NewTab();
            return;
        }

        var address = InternalUrlRouter.UrlForKind(kind);
        var existing = Tabs.FirstOrDefault(tab =>
            tab.Kind == kind && string.Equals(tab.Address, address, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ActiveTab = existing;
            return;
        }

        InternalUrlRouter.TryParse(address, out var route);
        var tab = new TabViewModel
        {
            Kind = kind,
            Address = address,
            Title = InternalUrlRouter.TitleFor(route),
        };
        tab.ResetNavigation(address);

        Tabs.Add(tab);
        ActiveTab = tab;
    }

    public void MoveTab(TabViewModel dragged, TabViewModel target)
    {
        var fromIndex = Tabs.IndexOf(dragged);
        var toIndex = Tabs.IndexOf(target);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) return;

        Tabs.Move(fromIndex, toIndex);
    }

    public void CloseTab(TabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        Tabs.RemoveAt(index);

        if (!ReferenceEquals(ActiveTab, tab)) return;

        if (Tabs.Count == 0)
        {
            NewTab();
            return;
        }

        ActiveTab = index < Tabs.Count ? Tabs[index] : Tabs[^1];
    }

    public void TogglePinTabById(int tabId)
    {
        var tab = GetTabById(tabId);
        if (tab != null) TogglePinTab(tab);
    }

    public void ReloadTabById(int tabId)
    {
        var tab = GetTabById(tabId);
        if (tab != null) ReloadTab(tab);
    }

    public void DuplicateTabById(int tabId)
    {
        var tab = GetTabById(tabId);
        if (tab != null) DuplicateTab(tab);
    }

    public void TogglePinTab(TabViewModel tab)
    {
        var currentIndex = Tabs.IndexOf(tab);
        if (currentIndex < 0) return;

        tab.IsPinned = !tab.IsPinned;

        // Pinned tabs form one stable prefix. Toggling one tab only needs one Move; rebuilding a
        // sorted copy and repeatedly calling IndexOf/Move made the operation O(n²) with many tabs.
        var targetIndex = Tabs.Count(item => item.IsPinned && !ReferenceEquals(item, tab));
        if (currentIndex != targetIndex)
            Tabs.Move(currentIndex, targetIndex);
    }

    public void ReloadTab(TabViewModel tab)
    {
        var browser = GetBrowser(tab);
        if (browser == null) return;

        if (browser.IsLoading)
            browser.Stop();
        browser.Reload();
    }

    public void DuplicateTab(TabViewModel tab)
    {
        var copy = new TabViewModel
        {
            Kind = tab.Kind,
            Address = string.IsNullOrWhiteSpace(tab.Address) ? "chrome://newtab/" : tab.Address,
            Title = tab.Title,
        };
        if (tab.Kind != TabKind.Web)
            copy.ResetNavigation(copy.Address);

        var index = Tabs.IndexOf(tab);
        if (index >= 0)
            Tabs.Insert(index + 1, copy);
        else
            Tabs.Add(copy);

        ActiveTab = copy;
    }

    public void ToggleGuestMode()
    {
        var wasGuest = IsGuestMode;
        if (!wasGuest)
            App.RequestContexts?.ResetGuestContext();

        IsGuestMode = !wasGuest;
        ResetTabsForBrowsingContext();

        if (wasGuest)
            App.RequestContexts?.ResetGuestContext();
    }

    private void OpenStartupTabs()
    {
        switch (AppSettings.Profile.StartupBehavior)
        {
            case 1:
                // Native Chromium Sessions are left untouched. Until CEF exposes SessionService
                // restore into Zidimi's WPF TabStrip, do not manufacture a parallel session file.
                NewTab();
                break;
            case 2:
                OpenTabsOrFallback(AppSettings.Profile.StartupPages, AppSettings.Profile.HomePageUrl);
                break;
            default:
                NewTab();
                break;
        }
    }

    private void OpenTabsOrFallback(IEnumerable<string> urls, string fallback)
    {
        var validUrls = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList();

        if (validUrls.Count == 0)
        {
            NewTab(fallback);
            return;
        }

        foreach (var url in validUrls)
            NewTab(url);
    }

    private void ResetTabsForBrowsingContext()
    {
        ActiveTab = null;
        Tabs.Clear();
        NewTab();
    }
    /// <summary>
    /// Releases Zidimi-owned background services. Chromium browser controls themselves are owned
    /// by BrowserView and are disposed when their tabs are removed.
    /// </summary>
    public void Dispose()
    {
        _downloads.Dispose();
        _history.Dispose();
        _bookmarks.Dispose();

        lock (_browsersLock)
        {
            _browsers.Clear();
            _browsersByTabId.Clear();
            _tabsById.Clear();
        }
    }

}
