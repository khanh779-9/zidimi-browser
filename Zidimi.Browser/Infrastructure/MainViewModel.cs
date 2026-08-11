using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CefSharp;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

public sealed class MainViewModel : ViewModelBase
{
    private PageId _activePage = PageId.Preferences;
    private TabViewModel? _activeTab;
    private Theme _theme = Theme.Dark;
    private string _searchFilter = "";
    private bool _isGuestMode;

private readonly HistoryService _history;
    private readonly BookmarkService _bookmarks;
    private readonly DownloadService _downloads;
    private readonly Dictionary<TabViewModel, IWebBrowser> _browsers = new();
    private readonly object _browsersLock = new();

    public MainViewModel(HistoryService history, BookmarkService bookmarks, DownloadService downloads)
    {
        _history = history;
        _bookmarks = bookmarks;
        _downloads = downloads;

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
ClearDownloadsCommand = new RelayCommand(_ => _downloads.Clear());
        RemoveDownloadCommand = new RelayCommand(p =>
        {
            if (p is DownloadEntry d) _downloads.Remove(d);
        });

// Create the default tab
        var startupBehavior = Zidimi.Browser.Models.AppSettings.Profile.StartupBehavior;
        if (startupBehavior == 0) // Open a blank page
        {
            NewTab("about:newtab");
        }
        else if (startupBehavior == 1) // Resume from where you left off
        {
            var urls = Zidimi.Browser.Models.AppSettings.Profile.LastSessionTabs;
            if (urls.Count > 0)
            {
                foreach (var url in urls)
                    NewTab(string.IsNullOrEmpty(url) ? "about:newtab" : url);
            }
            else
            {
                NewTab("about:newtab");
            }
        }
        else // Open a specific set of pages
        {
            var pages = Zidimi.Browser.Models.AppSettings.Profile.StartupPages;
            if (pages.Count > 0)
            {
                foreach (var p in pages)
                    NewTab(string.IsNullOrEmpty(p) ? "about:newtab" : p);
            }
            else
            {
                NewTab(Zidimi.Browser.Models.AppSettings.Profile.HomePageUrl);
            }
        }
    }

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ObservableCollection<HistoryEntry> History => _history.Entries;
    public ObservableCollection<Bookmark> Bookmarks => _bookmarks.Items;
    public ObservableCollection<DownloadEntry> Downloads => _downloads.Entries;

    /// <summary>Registers a new download — adds it to the list and saves it to disk.</summary>
    public void AddDownload(DownloadEntry entry) => _downloads.Add(entry);

    /// <summary>Updates a download's progress — persists the new state to disk.</summary>
    public void UpdateDownload(DownloadEntry entry) => _downloads.Update(entry);

    /// <summary>Registers the ChromiumWebBrowser for a tab (called from BrowserView).</summary>
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

    /// <summary>Gets the browser registered for a tab (null if none yet).</summary>
    public IWebBrowser? GetBrowser(TabViewModel tab)
    {
        lock (_browsersLock)
        {
            return _browsers.TryGetValue(tab, out var b) ? b : null;
        }
    }

/// <summary>Adds an entry to the browsing history.</summary>

/// <summary>Switches the current profile — reloads history, bookmarks and downloads for the new profile.</summary>
    public void SwitchProfile(string profileName)
    {
        _history.SwitchProfile(profileName);
        _bookmarks.SwitchProfile(profileName);
        _downloads.SwitchProfile(profileName);
        AutofillManager.Load();
    }

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

    /// <summary>Guest mode: no data is saved after closing (spec 8.2).</summary>
    public bool IsGuestMode
    {
        get => _isGuestMode;
        private set => Set(ref _isGuestMode, value);
    }

/// <summary>Gets a RequestContext for a new tab based on the current profile.
/// In guest mode an in-memory context is used instead.</summary>
    public CefSharp.IRequestContext? GetRequestContext()
        => IsGuestMode ? App.RequestContexts.GetGuestContext() : App.RequestContexts.GetProfileContext(Zidimi.Browser.Models.AppSettings.Global.CurrentProfile);

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
    public ICommand RemoveDownloadCommand { get; }

    /// <summary>Quick helper to add a bookmark from code-behind.</summary>
    public void AddBookmark(string url, string title) => _bookmarks.Add(url, title);

    public void NewTab(string url = "about:newtab")
    {
        var tab = new TabViewModel { Address = url, Title = "New Tab" };
        Tabs.Add(tab);
        ActiveTab = tab;
        ActivePage = PageId.Browser;
    }

/// <summary>Opens an internal app tab (Settings/History/Downloads/Bookmarks) in the tab strip.
/// If an app tab of the same kind already exists, activates it instead of opening a duplicate (spec 7.4).</summary>
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
            TabKind.Settings => LanguageManager.Instance["Tab_SettingsTitle"],
            TabKind.History => LanguageManager.Instance["Tab_HistoryTitle"],
            TabKind.Bookmarks => LanguageManager.Instance["Tab_BookmarksTitle"],
            TabKind.Downloads => LanguageManager.Instance["Tab_DownloadsTitle"],
            TabKind.Extensions => LanguageManager.Instance["Tab_ExtensionsTitle"],
            _ => "New Tab",
        } };
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    /// <summary>Moves a tab to the target tab's position (drag-reorder, spec 10.4).</summary>
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

    /// <summary>Pins/unpins a tab. When pinned, the tab shrinks and sits at the front of the list (spec 10.4).</summary>
    public void TogglePinTab(TabViewModel tab)
    {
        tab.IsPinned = !tab.IsPinned;
        // Sort: pinned tabs always come before regular tabs
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
        var next = Theme == Theme.Dark ? Theme.Light : Theme.Dark;
        Theme = next;
        ApplyTheme(Theme);
    }

    /// <summary>Turns guest mode on/off. When enabled, closes all tabs (unsaved) and opens a new in-memory tab.</summary>
    public void ToggleGuestMode()
    {
        IsGuestMode = !IsGuestMode;
        while (Tabs.Count > 0) CloseTab(Tabs[0]);
        NewTab();
    }

    public static void ApplyTheme(Theme theme)
    {
        ThemeManager.Apply(theme switch
        {
            Theme.Light => ThemeManager.AppTheme.Light,
            Theme.Dark => ThemeManager.AppTheme.Dark,
            _ => ThemeManager.AppTheme.Classic
        });
    }

/// <summary>Saves the list of open tabs (used for "Continue" mode on restart).
/// Incognito (guest) sessions are never persisted, matching Chrome's behavior
/// (spec 8.2).</summary>
    public void SaveSession()
    {
        if (IsGuestMode) return;
        var urls = Tabs
            .Where(t => t.Kind == TabKind.Web && !string.IsNullOrEmpty(t.Address))
            .Select(t => t.Address!)
            .ToList();
        Zidimi.Browser.Models.AppSettings.Profile.LastSessionTabs = urls;
        Zidimi.Browser.Models.AppSettings.SaveAll();
    }
}

