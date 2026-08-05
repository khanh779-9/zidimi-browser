using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CefSharp;
using CefSharp.Wpf;
using Heco.Browser.Infrastructure;
using Heco.Browser.Infrastructure.Handlers;
using Heco.Browser.Models;
using Path = System.Windows.Shapes.Path;

namespace Heco.Browser.Views;

public partial class BrowserView : UserControl
{
    private readonly MainViewModel _vm;
    private TabViewModel? _currentTab;
    private ChromiumWebBrowser? _currentBrowser;
    private readonly System.Collections.Generic.Dictionary<TabViewModel, ChromiumWebBrowser> _browsers = new();
    private readonly System.Collections.Generic.Dictionary<TabViewModel, FrameworkElement> _appViews = new();
    private bool _suppressAddressUpdate;
    private readonly System.Collections.Generic.List<Models.AutocompleteSuggestion> _allSuggestions = new();

    public BrowserView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Initialize language menu
        PopulateLanguageMenu();

        foreach (var t in _vm.Tabs) SubscribeTab(t);
        _vm.Tabs.CollectionChanged += OnTabsChanged;

        SwitchToTab(_vm.ActiveTab);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown -= OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        
        // Escape: hide Find bar if open
        if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible)
        {
            HideFindBar();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.T when mods == ModifierKeys.Control:
                _vm.NewTabCommand.Execute(null);
                FocusAddressBox();
                e.Handled = true;
                break;
            case Key.W when mods == ModifierKeys.Control:
                if (_vm.ActiveTab != null) _vm.CloseTabCommand.Execute(_vm.ActiveTab);
                e.Handled = true;
                break;
            case Key.Tab when mods == ModifierKeys.Control:
                CycleTab(direction: +1);
                e.Handled = true;
                break;
            case Key.Tab when mods == (ModifierKeys.Control | ModifierKeys.Shift):
                CycleTab(direction: -1);
                e.Handled = true;
                break;
            case Key.L when mods == ModifierKeys.Control:
                FocusAddressBox();
                e.Handled = true;
                break;
            case Key.R when mods == ModifierKeys.Control:
            case Key.F5:
                Reload_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.D when mods == ModifierKeys.Control:
                Star_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.D when mods == (ModifierKeys.Control | ModifierKeys.Shift):
                OpenDevTools();
                e.Handled = true;
                break;
            case Key.H when mods == ModifierKeys.Control:
                _vm.NavigateCommand.Execute(PageId.History);
                e.Handled = true;
                break;
            case Key.J when mods == ModifierKeys.Control:
                _vm.NavigateCommand.Execute(PageId.Downloads);
                e.Handled = true;
                break;
            case Key.F when mods == ModifierKeys.Control:
                Menu_FindInPage(null!, null!);
                e.Handled = true;
                break;
            // Zoom shortcuts
            case Key.Add when mods == ModifierKeys.Control:
            case Key.OemPlus when mods == ModifierKeys.Control:
                ZoomIn_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.Subtract when mods == ModifierKeys.Control:
            case Key.OemMinus when mods == ModifierKeys.Control:
                ZoomOut_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.D0 when mods == ModifierKeys.Control:
                ZoomReset_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.F11:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.F12:
                OpenDevTools();
                e.Handled = true;
                break;
            case Key.Left when mods == ModifierKeys.Alt:
                Back_Click(null!, null!);
                e.Handled = true;
                break;
            case Key.Right when mods == ModifierKeys.Alt:
                Forward_Click(null!, null!);
                e.Handled = true;
                break;
            // Ctrl+1..8: nhảy tới tab thứ N
            case Key.D1 when mods == ModifierKeys.Control:
            case Key.D2 when mods == ModifierKeys.Control:
            case Key.D3 when mods == ModifierKeys.Control:
            case Key.D4 when mods == ModifierKeys.Control:
            case Key.D5 when mods == ModifierKeys.Control:
            case Key.D6 when mods == ModifierKeys.Control:
            case Key.D7 when mods == ModifierKeys.Control:
            case Key.D8 when mods == ModifierKeys.Control:
                JumpToTab(e.Key - Key.D1);
                e.Handled = true;
                break;
            case Key.D9 when mods == ModifierKeys.Control:
                JumpToTab(_vm.Tabs.Count - 1);
                e.Handled = true;
                break;
            case Key.Escape:
                if (MenuPopup.IsOpen) { MenuPopup.IsOpen = false; e.Handled = true; }
                break;
        }
    }

    private void CycleTab(int direction)
    {
        var tabs = _vm.Tabs;
        if (tabs.Count < 2 || _vm.ActiveTab == null) return;
        var idx = tabs.IndexOf(_vm.ActiveTab);
        if (idx < 0) return;
        var next = (idx + direction + tabs.Count) % tabs.Count;
        _vm.ActiveTab = tabs[next];
    }

    private void JumpToTab(int index)
    {
        if (index >= 0 && index < _vm.Tabs.Count)
            _vm.ActiveTab = _vm.Tabs[index];
    }

    private void ToggleFullscreen()
    {
        var win = Window.GetWindow(this);
        if (win == null) return;
        if (win.WindowState == WindowState.Maximized)
            win.WindowState = WindowState.Normal;
        else
            win.WindowState = WindowState.Maximized;
    }

    private void FocusAddressBox()
    {
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    private void OnTabsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TabViewModel t in e.NewItems) SubscribeTab(t);

        if (e.OldItems != null)
            foreach (TabViewModel t in e.OldItems) UnsubscribeTab(t);
    }

    private void SubscribeTab(TabViewModel tab)
    {
        // App-tab nội bộ (Settings/History/...): không tạo ChromiumWebBrowser,
        // ẩn toolbar, hiển thị view tương ứng. (spec 7.4 — Settings mở trong tab)
        if (tab.Kind != TabKind.Web)
        {
            _appViews[tab] = CreateAppView(tab.Kind);
            return;
        }
        var browser = new ChromiumWebBrowser
        {
            Address = NormalizeUrl(tab.Address),
            RequestContext = _vm.GetRequestContext(),
        };

        // CEF handlers (spec 11.2)
        browser.LifeSpanHandler = new LifeSpanHandler(tab);
        var downloadHandler = new DownloadHandler();
        downloadHandler.DownloadStarted += entry =>
        {
            Dispatcher.BeginInvoke(() => _vm.Downloads.Insert(0, entry));
        };
        downloadHandler.DownloadUpdated += entry =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                var existing = _vm.Downloads.FirstOrDefault(d => d.Url == entry.Url && d.SuggestedFileName == entry.SuggestedFileName);
                if (existing != null)
                {
                    existing.IsCancelled = entry.IsCancelled;
                    existing.IsComplete = entry.IsComplete;
                    existing.TotalBytes = entry.TotalBytes;
                    existing.ReceivedBytes = entry.ReceivedBytes;
                    existing.FullPath = entry.FullPath;
                }
            });
        };
        browser.DownloadHandler = downloadHandler;
        browser.MenuHandler = new ContextMenuHandler();
        browser.KeyboardHandler = new KeyboardHandler();
        browser.JsDialogHandler = new JsDialogHandler();
        browser.RequestHandler = new RequestHandler();

        // Favicon (spec 10.4): load ảnh bất đồng bộ khi URL favicon đổi
        var faviconHandler = new FaviconHandler();
        faviconHandler.FaviconUrlChanged += faviconUrl =>
        {
            Dispatcher.BeginInvoke(() => LoadFaviconAsync(tab, faviconUrl));
        };
        browser.DisplayHandler = faviconHandler;

        // Audio indicator (spec 10.4): bật khi tab phát âm thanh
        var audioHandler = new AudioHandler();
        audioHandler.PlaybackStateChanged += playing =>
        {
            Dispatcher.BeginInvoke(() => tab.IsAudioPlaying = playing);
        };
        browser.AudioHandler = audioHandler;

        browser.TitleChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                var t = (string?)args.NewValue ?? LanguageManager.Instance["Browser_HecoBrowser"];
                tab.Title = string.IsNullOrEmpty(t) ? LanguageManager.Instance["Browser_HecoBrowser"] : t;
                if (ReferenceEquals(_currentTab, tab))
                    UpdateStarState(tab);
            });
        };

        browser.AddressChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                var newUrl = (string?)args.NewValue ?? "";
                tab.Address = newUrl;
                if (ReferenceEquals(_currentTab, tab) && !_suppressAddressUpdate)
                {
                    AddressBox.Text = newUrl;
                    UpdateSecurityIcon(newUrl);
                }
                AddToHistory(newUrl, tab.Title);
            });
        };

        browser.LoadingStateChanged += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                tab.IsLoading = e.IsLoading;
                tab.CanGoBack = browser.CanGoBack;
                tab.CanGoForward = browser.CanGoForward;
                if (ReferenceEquals(_currentBrowser, browser))
                {
                    UpdateReloadIcon(e.IsLoading);
                    UpdateLoadingProgress(e.IsLoading);
                }
            });
        };

        _browsers[tab] = browser;
        _vm.RegisterBrowser(tab, browser);
    }

    private static void AddToHistory(string address, string title)
    {
        if (string.IsNullOrEmpty(address)) return;
        var a = address.Trim();
        if (a == "about:blank" || a == "about:newtab") return;
        App.ViewModel?.AddHistory(a, title);
    }

    private void UnsubscribeTab(TabViewModel tab)
    {
        if (_browsers.TryGetValue(tab, out var b))
        {
            b.Dispose();
            _browsers.Remove(tab);
        }
        _appViews.Remove(tab);
        _vm.UnregisterBrowser(tab);
        if (ReferenceEquals(_currentTab, tab)) _currentTab = null;
    }

    /// <summary>Tạo view nội bộ cho app-tab (Settings/History/Downloads/Bookmarks).</summary>
    private static FrameworkElement CreateAppView(TabKind kind) => kind switch
    {
        TabKind.Settings => new PreferencesView(),
        TabKind.History => new HistoryView(),
        TabKind.Bookmarks => new BookmarksView(),
        TabKind.Downloads => new DownloadsView(),
        _ => new TextBlock { Text = "?" },
    };

    private async void LoadFaviconAsync(TabViewModel tab, string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(6);
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length == 0) return;
            using var ms = new System.IO.MemoryStream(bytes);
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            _ = Dispatcher.BeginInvoke(new Action(() => tab.Favicon = bmp));
        }
        catch
        {
            // favicon lỗi/timed out — giữ fallback icon
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveTab))
            SwitchToTab(_vm.ActiveTab);
    }

    private void SwitchToTab(TabViewModel? tab)
    {
        if (tab == null)
        {
            BrowserHost.Content = null;
            EmptyHint.Visibility = Visibility.Visible;
            ToolbarRow.Visibility = Visibility.Visible;
            return;
        }
        EmptyHint.Visibility = Visibility.Collapsed;
        _currentTab = tab;

        // App-tab nội bộ: ẩn toolbar trình duyệt, hiển thị view nội bộ.
        if (tab.Kind != TabKind.Web)
        {
            _currentBrowser = null;
            ToolbarRow.Visibility = Visibility.Collapsed;
            if (_appViews.TryGetValue(tab, out var view))
            {
                BrowserHost.Content = view;
            }
            else
            {
                var v = CreateAppView(tab.Kind);
                _appViews[tab] = v;
                BrowserHost.Content = v;
            }
            return;
        }
        ToolbarRow.Visibility = Visibility.Visible;

        if (_browsers.TryGetValue(tab, out var browser))
        {
            _currentBrowser = browser;
            BrowserHost.Content = browser;
            _suppressAddressUpdate = true;
            AddressBox.Text = browser.Address ?? "";
            _suppressAddressUpdate = false;
            UpdateSecurityIcon(browser.Address ?? "");
            UpdateReloadIcon(tab.IsLoading);
            UpdateLoadingProgress(tab.IsLoading);
            UpdateStarState(tab);
        }
    }

    private static string NormalizeUrl(string raw)
    {
        raw = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(raw) || raw == "about:newtab") return Heco.Browser.Models.AppSettings.Current.HomePageUrl;
        if (Uri.IsWellFormedUriString(raw, UriKind.Absolute)) return raw;
        if (raw.Contains('.') && !raw.Contains(' ')) return "https://" + raw;
        
        var engine = Heco.Browser.Models.AppSettings.Current.SearchEngine;
        var query = Uri.EscapeDataString(raw);
        return engine switch
        {
            "DuckDuckGo" => "https://duckduckgo.com/?q=" + query,
            "Bing" => "https://www.bing.com/search?q=" + query,
            "Brave Search" => "https://search.brave.com/search?q=" + query,
            _ => "https://www.google.com/search?q=" + query
        };
    }

    private void UpdateSecurityIcon(string? url)
    {
        if (string.IsNullOrEmpty(url) || url == "about:blank" || url == "about:newtab")
        {
            SecurityIcon.Stroke = (Brush)FindResource("Ink300Brush");
            SecurityIcon.Fill = Brushes.Transparent;
            SecurityIcon.Data = Geometry.Parse("M8 10 V7 a4 4 0 0 1 8 0 v3 M5 10 H19 V20 H5 Z");
            SecurityIcon.ToolTip = LanguageManager.Instance["Browser_SiteInfo"];
            return;
        }
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            SecurityIcon.Stroke = (Brush)FindResource("SafeBrush");
            SecurityIcon.Fill = new SolidColorBrush(Color.FromArgb(0x26, 0x22, 0xC5, 0x5E));
            SecurityIcon.Data = Geometry.Parse("M8 10 V7 a4 4 0 0 1 8 0 v3 M5 10 H19 V20 H5 Z");
            SecurityIcon.ToolTip = LanguageManager.Instance["Browser_SecureConnHttps"];
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            // HTTP không an toàn — icon "info" cảnh báo
            SecurityIcon.Stroke = (Brush)FindResource("WarnBrush");
            SecurityIcon.Fill = new SolidColorBrush(Color.FromArgb(0x26, 0xF5, 0x9E, 0x0B));
            SecurityIcon.Data = Geometry.Parse("M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01");
            SecurityIcon.ToolTip = LanguageManager.Instance["Browser_NotSecureHttp"];
        }
        else
        {
            SecurityIcon.Stroke = (Brush)FindResource("InfoBrush");
            SecurityIcon.Fill = Brushes.Transparent;
            SecurityIcon.Data = Geometry.Parse("M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01");
            SecurityIcon.ToolTip = LanguageManager.Instance["Browser_InternalPage"];
        }
    }

    private void UpdateReloadIcon(bool isLoading)
    {
        if (ReloadBtn.Content is not Path p) return;
        p.Data = isLoading
            ? Geometry.Parse("M7 7 L17 17 M17 7 L7 17")
            : Geometry.Parse("M12 3 a9 9 0 1 0 9 9 M12 3 L9 6 M12 3 L15 6");
        ReloadBtn.ToolTip = isLoading ? LanguageManager.Instance["Browser_StopLoad"] : LanguageManager.Instance["Browser_Reload"];
    }

    private void UpdateLoadingProgress(bool isLoading)
    {
        if (LoadingProgress == null) return;
        LoadingProgress.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStarState(TabViewModel tab)
    {
        var url = tab.Address?.Trim() ?? "";
        var bm = _vm.Bookmarks;
        bool active = !string.IsNullOrEmpty(url) && bm.Any(b => b.Url == url);
        if (StarIcon == null) return;
        StarIcon.Fill = active
            ? (Brush)FindResource("HecoPurpleBrush")
            : Brushes.Transparent;
        StarIcon.Stroke = active
            ? (Brush)FindResource("HecoPurpleBrush")
            : (Brush)FindResource("Ink300Brush");
        StarBtn.ToolTip = active ? LanguageManager.Instance["Browser_RemoveBookmark"] : LanguageManager.Instance["Browser_SavePage"];
    }

    // ===== Toolbar handlers =====
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser is { CanGoBack: true }) _currentBrowser.Back();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser is { CanGoForward: true }) _currentBrowser.Forward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser == null) return;
        if (_currentTab?.IsLoading == true) _currentBrowser.Stop();
        else _currentBrowser.Reload();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(Heco.Browser.Models.AppSettings.Current.HomePageUrl);
    }

    private void Address_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NavigateTo(AddressBox.Text);
        Keyboard.ClearFocus();
    }

    private void NavigateTo(string input)
    {
        if (_currentTab == null) return;
        var url = NormalizeUrl(input);
        _suppressAddressUpdate = true;
        _currentTab.Address = url;
        if (_browsers.TryGetValue(_currentTab, out var b))
            b.Load(url);
        UpdateSecurityIcon(url);
        AddressBox.Text = url;
        _suppressAddressUpdate = false;
    }

    private void Address_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressBox.Dispatcher.BeginInvoke(new Action(AddressBox.SelectAll),
            System.Windows.Threading.DispatcherPriority.Input);
        AddressBarBorder.BorderBrush = (Brush)FindResource("HecoPurpleBrush");
        AddressBarBorder.Background = (Brush)FindResource("OmniboxFocusBgBrush");
        UpdateAutocomplete();
    }

    private void Address_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Delay hide để cho phép click vào dropdown
        AddressBox.Dispatcher.BeginInvoke(() =>
        {
            if (!AutocompletePopup.IsMouseOver)
                AutocompletePopup.IsOpen = false;
        }, System.Windows.Threading.DispatcherPriority.Background);

        AddressBarBorder.BorderBrush = (Brush)FindResource("StrokeBrush");
        AddressBarBorder.Background = (Brush)FindResource("HecoBgSurfaceBrush");
    }

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateAutocomplete();
    }

    private void UpdateAutocomplete()
    {
        if (AutocompletePopup == null || AutocompleteList == null) return;

        if (!AddressBox.IsKeyboardFocusWithin || !Heco.Browser.Models.AppSettings.Current.SearchSuggestEnabled)
        {
            AutocompletePopup.IsOpen = false;
            return;
        }

        var query = AddressBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query))
        {
            AutocompletePopup.IsOpen = false;
            return;
        }

        _allSuggestions.Clear();

        // History matches
        foreach (var h in _vm.History)
        {
            if (h.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || h.Url?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            {
                _allSuggestions.Add(new Models.AutocompleteSuggestion
                {
                    Title = h.Title ?? h.Url ?? "",
                    Subtitle = h.Url ?? "",
                    IconPath = "M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01",
                    TypeLabel = LanguageManager.Instance["Browser_History"],
                    TargetUrl = h.Url ?? ""
                });
            }
        }

        // Bookmark matches
        foreach (var b in _vm.Bookmarks)
        {
            if (b.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true
                || b.Url?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            {
                _allSuggestions.Add(new Models.AutocompleteSuggestion
                {
                    Title = b.Title ?? b.Url ?? "",
                    Subtitle = b.Url ?? "",
                    IconPath = "M12 2 L15.09 8.26 L22 9.27 L17 14.14 L18.18 21.02 L12 17.77 L5.82 21.02 L7 14.14 L2 9.27 L8.91 8.26 Z",
                    TypeLabel = LanguageManager.Instance["Bookmarks_Title"],
                    TargetUrl = b.Url ?? ""
                });
            }
        }

        // Search suggestion
        if (!string.IsNullOrWhiteSpace(query))
        {
            var engine = Heco.Browser.Models.AppSettings.Current.SearchEngine;
            var engineUrl = engine switch
            {
                "DuckDuckGo" => "https://duckduckgo.com/?q=",
                "Bing" => "https://www.bing.com/search?q=",
                "Brave Search" => "https://search.brave.com/search?q=",
                _ => "https://www.google.com/search?q="
            };
            _allSuggestions.Add(new Models.AutocompleteSuggestion
            {
                Title = LanguageManager.Instance["Browser_SearchQuery"].Replace("{query}", query),
                Subtitle = LanguageManager.Instance["Browser_SearchOnEngine"].Replace("{engine}", engine),
                IconPath = "M15.5 14 h-.79 l-.28-.27 a6.5 6.5 0 1 0 -.7.7 l.27.28 v.79 l5 4.99 L20.49 19 z",
                TypeLabel = LanguageManager.Instance["Browser_Search"],
                TargetUrl = engineUrl + Uri.EscapeDataString(query)
            });
        }

        // Limit to 10
        var limited = _allSuggestions.Take(10).ToList();
        AutocompleteList.ItemsSource = limited;

        AutocompletePopup.IsOpen = limited.Count > 0;
    }

    private void Autocomplete_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AutocompleteList.SelectedItem is Models.AutocompleteSuggestion suggestion)
        {
            NavigateTo(suggestion.TargetUrl);
            AutocompletePopup.IsOpen = false;
            AutocompleteList.SelectedItem = null;
        }
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTab == null) return;
        var url = (_currentTab.Address ?? "").Trim();
        var title = (_currentTab.Title ?? "").Trim();
        if (string.IsNullOrEmpty(url) || url == "about:newtab" || url == "about:blank") return;

        var existing = _vm.Bookmarks.FirstOrDefault(b => b.Url == url);
        if (existing != null)
        {
            _vm.RemoveBookmarkCommand.Execute(existing);
        }
        else
        {
            _vm.AddBookmark(url, title);
        }
        UpdateStarState(_currentTab);
    }

    // ===== Menu popup =====
    private void Menu_Click(object sender, RoutedEventArgs e) => MenuPopup.IsOpen = !MenuPopup.IsOpen;
    private void Menu_NewTab(object sender, RoutedEventArgs e)
    {
        _vm.NewTabCommand.Execute(null);
        MenuPopup.IsOpen = false;
    }
    private void Menu_CloseTab(object sender, RoutedEventArgs e)
    {
        if (_vm.ActiveTab != null) _vm.CloseTabCommand.Execute(_vm.ActiveTab);
        MenuPopup.IsOpen = false;
    }
    private void Menu_History(object sender, RoutedEventArgs e)
    {
        _vm.NavigateCommand.Execute(PageId.History);
        MenuPopup.IsOpen = false;
    }
    private void Menu_Bookmarks(object sender, RoutedEventArgs e)
    {
        _vm.NavigateCommand.Execute(PageId.Bookmarks);
        MenuPopup.IsOpen = false;
    }
    private void Menu_FindInPage(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        ShowFindBar();
    }

    private void ShowFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void HideFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        FindBox.Text = "";
        FindCount.Text = "";
        if (_currentBrowser != null)
        {
            _currentBrowser.StopFinding(true);
        }
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideFindBar();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrev_Click(null!, null!);
            else
                FindNext_Click(null!, null!);
            e.Handled = true;
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser == null || string.IsNullOrWhiteSpace(FindBox.Text)) return;
        var browser = _currentBrowser.GetBrowser();
        if (browser != null)
            CefSharp.WebBrowserExtensions.Find(browser, FindBox.Text, true, false, false);
    }

    private void FindPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser == null || string.IsNullOrWhiteSpace(FindBox.Text)) return;
        var browser = _currentBrowser.GetBrowser();
        if (browser != null)
            CefSharp.WebBrowserExtensions.Find(browser, FindBox.Text, false, false, false);
    }

    // ===== Zoom controls =====
    private void UpdateZoomLevel(double level)
    {
        if (_currentBrowser == null) return;
        _currentBrowser.SetZoomLevel(level);
        if (ZoomLevelText != null)
        {
            int percent = (int)Math.Round(level * 100);
            ZoomLevelText.Text = $"{percent}%";
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser == null) return;
        var level = Math.Min(5.0, _currentBrowser.ZoomLevel + 0.25);
        UpdateZoomLevel(level);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBrowser == null) return;
        var level = Math.Max(0.25, _currentBrowser.ZoomLevel - 0.25);
        UpdateZoomLevel(level);
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        UpdateZoomLevel(0.0);
    }

    private void Menu_DevTools(object sender, RoutedEventArgs e)
    {
        MenuPopup.IsOpen = false;
        OpenDevTools();
    }
    private void Menu_Preferences(object sender, RoutedEventArgs e)
    {
        _vm.OpenAppTab(TabKind.Settings);
        MenuPopup.IsOpen = false;
    }

    private void PopulateLanguageMenu()
    {
        if (LanguageMenu == null) return;
        
        LanguageMenu.Items.Clear();
        foreach (var lang in LanguageManager.Instance.AvailableLanguages)
        {
            var item = new MenuItem
            {
                Header = lang.Name,
                Tag = lang,
                IsChecked = LanguageManager.Instance.CurrentLanguage?.Code == lang.Code,
                Foreground = (Brush)FindResource("Ink100Brush")
            };
            item.Click += LanguageMenuItem_Click;
            LanguageMenu.Items.Add(item);
        }
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is LanguageInfo lang)
        {
            LanguageManager.Instance.CurrentLanguage = lang;
            
            // Update checked state
            foreach (MenuItem mi in LanguageMenu.Items)
            {
                mi.IsChecked = ReferenceEquals(mi, item);
            }
            MenuPopup.IsOpen = false;
        }
    }

    // ===== Profile / Guest mode =====
    private void Avatar_Click(object sender, RoutedEventArgs e)
    {
        AvatarInitial.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_GuestInitial"] : LanguageManager.Instance["Browser_HecoInitial"];
        AvatarInitial2.Text = AvatarInitial.Text;
        ProfileNameText.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_Guest"] : LanguageManager.Instance["Browser_HecoBrowser"];
        ProfileModeText.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_NoDataSaved"] : LanguageManager.Instance["Browser_DefaultProfile"];
        GuestModeCheck.IsChecked = _vm.IsGuestMode;
        AvatarPopup.IsOpen = !AvatarPopup.IsOpen;
    }

    private void GuestMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_vm.IsGuestMode != (GuestModeCheck.IsChecked == true))
            _vm.ToggleGuestMode();
    }

    private void Avatar_ManageProfiles(object sender, RoutedEventArgs e)
    {
        AvatarPopup.IsOpen = false;
        _vm.OpenAppTab(TabKind.Settings);
    }

    private void OpenDevTools()
    {
        _currentBrowser?.ShowDevTools();
    }

    // ===== Site Info Popup =====
    private void SecurityBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateSiteInfo();
        SiteInfoPopup.IsOpen = !SiteInfoPopup.IsOpen;
    }

    private void UpdateSiteInfo()
    {
        if (_currentTab == null) return;
        var url = _currentTab.Address ?? "";
        var isHttps = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var isHttp = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        if (isHttps)
        {
            ConnIcon.Stroke = (Brush)FindResource("SafeBrush");
            ConnTitle.Text = LanguageManager.Instance["Browser_SecureConn"];
            ConnDesc.Text = LanguageManager.Instance["Browser_EncryptedConn"];
            ConnIcon.Data = Geometry.Parse("M8 10 V7 a4 4 0 0 1 8 0 v3 M5 10 H19 V20 H5 Z");
        }
        else if (isHttp)
        {
            ConnIcon.Stroke = (Brush)FindResource("WarnBrush");
            ConnTitle.Text = LanguageManager.Instance["Browser_NotSecure"];
            ConnDesc.Text = LanguageManager.Instance["Browser_UnencryptedConn"];
            ConnIcon.Data = Geometry.Parse("M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01");
        }
        else
        {
            ConnIcon.Stroke = (Brush)FindResource("InfoBrush");
            ConnTitle.Text = LanguageManager.Instance["Browser_InternalPage"];
            ConnDesc.Text = LanguageManager.Instance["Browser_LocalBrowserPage"];
            ConnIcon.Data = Geometry.Parse("M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01");
        }

        // Permissions placeholder (cần CEF permission handler để lấy thực tế)
        PermissionsPanel.Children.Clear();
        var perms = new[]
        {
            (LanguageManager.Instance["Perm_Camera"], "camera"),
            (LanguageManager.Instance["Perm_Microphone"], "mic"),
            (LanguageManager.Instance["Perm_Location"], "location"),
            (LanguageManager.Instance["Perm_Notifications"], "notifications"),
            (LanguageManager.Instance["Perm_Popups"], "popups"),
            (LanguageManager.Instance["Perm_JavaScript"], "javascript")
        };
        foreach (var (name, key) in perms)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            item.Children.Add(new Path
            {
                Width = 14, Height = 14, Stretch = Stretch.Uniform,
                Stroke = (Brush)FindResource("Ink400Brush"), StrokeThickness = 1.5,
                Data = Geometry.Parse("M12 2 a10 10 0 1 0 0.01 0 Z M12 8 V12 M12 16 H12.01"),
                Margin = new Thickness(0, 0, 8, 0)
            });
            item.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 12,
                Foreground = (Brush)FindResource("Ink200Brush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            item.Children.Add(new TextBlock
            {
                Text = LanguageManager.Instance["Browser_AskDefault"],
                FontSize = 11,
                Foreground = (Brush)FindResource("Ink500Brush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
            PermissionsPanel.Children.Add(item);
        }
    }

    private void SiteInfo_Cookies_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        MessageBox.Show(LanguageManager.Instance["Browser_CookieWIP"],
            LanguageManager.Instance["Browser_HecoBrowser"], MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SiteInfo_Cert_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        if (_currentBrowser != null)
        {
            _currentBrowser.ShowDevTools();
        }
    }

    private void SiteInfo_Settings_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        MessageBox.Show(LanguageManager.Instance["Browser_SiteSettingsWIP"],
            LanguageManager.Instance["Browser_HecoBrowser"], MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
