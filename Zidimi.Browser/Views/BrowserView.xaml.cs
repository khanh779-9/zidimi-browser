using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using CefSharp;
using CefSharp.Wpf.HwndHost;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;
using Path = System.Windows.Shapes.Path;

namespace Zidimi.Browser.Views;

public partial class BrowserView : UserControl
{
    private readonly MainViewModel _vm;
    private TabViewModel? _currentTab;
    private ChromiumWebBrowser? _currentBrowser;
    private readonly System.Collections.Generic.Dictionary<TabViewModel, ChromiumWebBrowser> _browsers = new();
    private readonly System.Collections.Generic.Dictionary<TabViewModel, FrameworkElement> _appViews = new();
    private bool _suppressAddressUpdate;
    private readonly System.Collections.Generic.List<Models.AutocompleteSuggestion> _allSuggestions = new();
    private readonly LoadingSpinner _loadingSpinner = new();
    private bool _extensionInstallInProgress;

    private const string WebStoreInstallMessagePrefix = "zidimi:webstore-install:";

    // Zidimi intercepts the Web Store CTA before the site's native installer runs.
    // The tab uses Chrome-runtime HwndHost, then Zidimi loads the validated/unpacked
    // package into Chromium's real extension runtime.
    private const string WebStoreInstallBridgeScript = """
        (() => {
            if (window.__zidimiWebStoreInstallHook) return;
            window.__zidimiWebStoreInstallHook = true;

            const isStore = () => location.hostname === 'chromewebstore.google.com';
            const isDetail = () => /^\/detail\//i.test(location.pathname);
            const installLabels = [
                'add to chrome', 'install extension',
                'thêm vào chrome', 'cài đặt tiện ích',
                'ajouter à chrome', 'zu chrome hinzufügen',
                'aggiungi a chrome', 'añadir a chrome',
                'adicionar ao chrome', 'добавить в chrome'
            ];

            let lastIntercept = 0;
            const findInstallTarget = event => {
                const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
                const target = path.find(node =>
                    node instanceof Element && node.matches('button,[role="button"],a'))
                    || (event.target instanceof Element
                        ? event.target.closest('button,[role="button"],a')
                        : null);
                if (!target) return null;

                const label = [
                    target.innerText || '',
                    target.getAttribute('aria-label') || '',
                    target.getAttribute('title') || ''
                ].join(' ').replace(/\s+/g, ' ').trim().toLowerCase();

                return installLabels.some(text => label.includes(text)) ? target : null;
            };

            const intercept = event => {
                if (!isStore() || !isDetail() || !findInstallTarget(event)) return;

                event.preventDefault();
                event.stopPropagation();
                event.stopImmediatePropagation();

                // pointerdown/mousedown/click can all fire for one press. Notify C# once.
                const now = Date.now();
                if (now - lastIntercept < 750) return;
                lastIntercept = now;

                if (window.CefSharp && typeof CefSharp.PostMessage === 'function') {
                    CefSharp.PostMessage('zidimi:webstore-install:' + location.href);
                }
            };

            // Catch the interaction before the Web Store can enter Chromium's native
            // extension installer. click alone is too late on some Web Store builds.
            for (const name of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
                window.addEventListener(name, intercept, true);
            }

            window.addEventListener('keydown', event => {
                if ((event.key === 'Enter' || event.key === ' ') && findInstallTarget(event)) {
                    intercept(event);
                }
            }, true);
        })();
        """;

    public BrowserView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        foreach (var t in _vm.Tabs) SubscribeTab(t);
        _vm.Tabs.CollectionChanged += OnTabsChanged;
        App.CefReadyChanged += OnCefReady;

        SwitchToTab(_vm.ActiveTab);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>CEF just finished initializing — create the browser for the visible tab if it was previously waiting.</summary>
    private void OnCefReady()
    {
        if (_currentTab != null && _currentTab.Kind == TabKind.Web)
            SwitchToTab(_currentTab);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown += OnPreviewKeyDown;

        ExtensionService.Instance.ExtensionsChanged -= OnExtensionsChanged;
        ExtensionService.Instance.ExtensionsChanged += OnExtensionsChanged;
        ExtensionService.Instance.RefreshForCurrentProfile();
        RefreshExtensionSurfaces();

        if (_currentBrowser?.IsBrowserInitialized == true)
            _ = ExtensionService.Instance.EnsureProfileRuntimeLoadedAsync(_currentBrowser);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown -= OnPreviewKeyDown;

        ExtensionService.Instance.ExtensionsChanged -= OnExtensionsChanged;
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
                _vm.OpenAppTab(TabKind.History);
                e.Handled = true;
                break;
            case Key.A when mods == (ModifierKeys.Control | ModifierKeys.Shift):
                (Application.Current?.MainWindow as MainWindow)?.OpenTabSearch();
                e.Handled = true;
                break;
            case Key.J when mods == ModifierKeys.Control:
                _vm.OpenAppTab(TabKind.Downloads);
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
            // Ctrl+1..8: jump to the Nth tab
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
        // Internal app-tab (Settings/History/...): don't create a ChromiumWebBrowser,
        // hide the toolbar, and show the corresponding view. (spec 7.4 — Settings opens in a tab)
        if (tab.Kind != TabKind.Web)
        {
            _appViews[tab] = CreateAppView(tab.Kind);
            return;
        }
        // The CEF browser is created LAZILY when the tab is shown (see EnsureBrowser) —
        // this avoids initializing every tab at once when the window opens (a cause of slow startup).
        _browsers[tab] = null!;
    }

    /// <summary>Create a ChromiumWebBrowser for the tab if none exists. Only called when CEF is ready.</summary>
    private void EnsureBrowser(TabViewModel tab)
    {
        if (tab.Kind != TabKind.Web) return;
        if (_browsers.TryGetValue(tab, out var existing) && existing != null && !existing.IsDisposed) return;
        if (!App.CefReady) return; // CEF not initialized yet — it will be created when the app reports ready
        var browser = CreateBrowser(tab);
        _browsers[tab] = browser;
        _vm.RegisterBrowser(tab, browser);
    }

    private ChromiumWebBrowser CreateBrowser(TabViewModel tab)
    {
        var browser = new ChromiumWebBrowser
        {
            Address = NormalizeUrl(tab.Address),
            RequestContext = _vm.GetRequestContext(),
            BrowserSettings = BuildBrowserSettings()
        };

        browser.IsBrowserInitializedChanged += async (_, _) =>
        {
            if (!browser.IsBrowserInitialized || browser.IsDisposed || _vm.IsGuestMode) return;
            try
            {
                await ExtensionService.Instance.EnsureProfileRuntimeLoadedAsync(browser);
            }
            catch (Exception ex)
            {
                AppLogger.Log("ExtensionRuntime", ex, "Loading profile extensions after browser initialization.");
            }
        };

        // Zoom level is handled automatically by CEF's partition.default_zoom_level

        // CEF handlers (spec 11.2)
        browser.LifeSpanHandler = new LifeSpanHandler(tab);
        var downloadHandler = new DownloadHandler();
        downloadHandler.DownloadStarted += entry =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _vm.AddDownload(entry);
                // If AppSettings requires opening the Downloads bar when a download starts → open the Downloads page.
                if (Models.AppSettings.Profile.ShowDownloadBar)
                    _vm.OpenAppTab(TabKind.Downloads);
            });
        };
        downloadHandler.DownloadUpdated += entry =>
        {
            if (DownloadHandler.IsWebStoreCrx(entry.Url)) return;
            Dispatcher.BeginInvoke(() =>
            {
                _vm.UpdateDownload(entry);
                var existing = _vm.Downloads.FirstOrDefault(d => d.Guid == entry.Guid);
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
        downloadHandler.CrxInstallRequested += url =>
        {
            Dispatcher.BeginInvoke(() => HandleWebStoreCrxInstall(url));
        };
        browser.DownloadHandler = downloadHandler;
        browser.MenuHandler = new ContextMenuHandler();
        browser.KeyboardHandler = new KeyboardHandler();
        browser.JsDialogHandler = new JsDialogHandler();
        browser.DialogHandler = new DialogHandler();
        browser.RequestHandler = new RequestHandler();
        browser.PermissionHandler = new ZidimiPermissionHandler();

        var loadHandler = new ZidimiLoadHandler();
        loadHandler.LoadingStateChanged += e =>
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
        browser.LoadHandler = loadHandler;

        // Favicon (spec 10.4): load the image asynchronously when the favicon URL changes
        var faviconHandler = new FaviconHandler();
        faviconHandler.FaviconUrlChanged += faviconUrl =>
        {
            Dispatcher.BeginInvoke(() => LoadFaviconAsync(tab, faviconUrl));
        };
        browser.DisplayHandler = faviconHandler;

        // Audio indicator (spec 10.4): turn on when the tab plays audio
        var audioHandler = new AudioHandler();
        audioHandler.PlaybackStateChanged += playing =>
        {
            Dispatcher.BeginInvoke(() => tab.IsAudioPlaying = playing);
        };
        browser.AudioHandler = audioHandler;
        browser.FocusHandler = new ZidimiFocusHandler();

        var renderHandler = new ZidimiRenderProcessMessageHandler();
        renderHandler.EditableFocused += isEditable =>
        {
            if (isEditable && FindBar.Visibility == Visibility.Visible)
            {
                Dispatcher.BeginInvoke(HideFindBar);
            }
        };
        browser.RenderProcessMessageHandler = renderHandler;
        browser.DragHandler = new ZidimiDragHandler();

        var findHandler = new FindHandler();
        findHandler.FindResult += (count, activeMatchOrdinal, finalUpdate) =>
        {
            if (browser.IsDisposed || FindCount == null)
                return;
            Dispatcher.BeginInvoke(() => UpdateFindCount(count, activeMatchOrdinal));
        };
        browser.FindHandler = findHandler;

        ZidimiJsBinding.Bind(browser);

        browser.JavascriptMessageReceived += (_, e) =>
        {
            if (e.Message is not string message ||
                !message.StartsWith(WebStoreInstallMessagePrefix, StringComparison.Ordinal))
            {
                return;
            }

            var url = message[WebStoreInstallMessagePrefix.Length..];
            if (!IsChromeWebStoreDetailUrl(url)) return;

            AppLogger.Log("WebStoreBridge", $"Intercepted Add to Chrome before native install flow. Url={url}");
            Dispatcher.BeginInvoke(() => HandleWebStoreCrxInstall(url));
        };

        browser.FrameLoadEnd += (_, e) =>
        {
            if (!e.Frame.IsMain || !IsChromeWebStoreUrl(e.Url)) return;

            try
            {
                e.Frame.ExecuteJavaScriptAsync(WebStoreInstallBridgeScript);
            }
            catch (Exception ex)
            {
                AppLogger.Log("WebStoreBridge", ex);
            }
        };

        browser.TitleChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                var t = (string?)args.NewValue ?? LanguageManager.Instance["Browser_ZidimiBrowser"];
                tab.Title = string.IsNullOrEmpty(t) ? LanguageManager.Instance["Browser_ZidimiBrowser"] : t;
                if (ReferenceEquals(_currentTab, tab))
                    UpdateStarState(tab);
            });
        };

        // CefSharp.Wpf.HwndHost exposes Address as a DependencyProperty, but unlike
        // CefSharp.Wpf it does not expose a public AddressChanged event. Observe the
        // dependency property directly so SPA/history navigations still update the tab
        // and address bar without falling back to LoadingStateChanged.
        var addressDescriptor = DependencyPropertyDescriptor.FromProperty(
            ChromiumWebBrowser.AddressProperty, typeof(ChromiumWebBrowser));
        addressDescriptor?.AddValueChanged(browser, (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (browser.IsDisposed) return;

                var newUrl = browser.Address ?? "";
                tab.Address = newUrl;
                if (ReferenceEquals(_currentTab, tab) && !_suppressAddressUpdate)
                {
                    AddressBox.Text = newUrl;
                    UpdateSecurityIcon(newUrl);
                }
            });
        });

        return browser;
    }

    /// <summary>
    /// Extends BrowserSettings configuration based on AppSettings:
    /// font size (page & fixed), MinimumFontSize tracking the font size, background color per theme
    /// (to avoid a white flash when loading pages on a dark background), and WindowlessFrameRate.
    /// </summary>
    private static CefSharp.BrowserSettings BuildBrowserSettings()
    {
        var profile = Models.AppSettings.Profile;

        // BackgroundColor follows the active theme (dark → dark, light/classic → white/light).
        var themeKey = Infrastructure.ThemeManager.NormalizeThemeKey(profile.Theme);
        var effectiveTheme = themeKey switch
        {
            "light" => Infrastructure.ThemeManager.AppTheme.Light,
            "dark" => Infrastructure.ThemeManager.AppTheme.Dark,
            "classic" => Infrastructure.ThemeManager.AppTheme.Classic,
            _ => Infrastructure.ThemeManager.DetectSystemTheme()
        };
        uint bg = effectiveTheme switch
        {
            Infrastructure.ThemeManager.AppTheme.Dark => 0xFF1E1F24u,
            Infrastructure.ThemeManager.AppTheme.Classic => 0xFFFEFEFEu,
            _ => 0xFFFFFFFFu
        };

        return new CefSharp.BrowserSettings
        {
            WindowlessFrameRate = 60,
            BackgroundColor = bg,
        };
    }

    private void UnsubscribeTab(TabViewModel tab)
    {
        if (_browsers.TryGetValue(tab, out var b))
        {
            b?.Dispose();
            _browsers.Remove(tab);
        }
        _appViews.Remove(tab);
        _vm.UnregisterBrowser(tab);
        if (ReferenceEquals(_currentTab, tab)) _currentTab = null;
    }

    /// <summary>Create the internal view for an app-tab (Settings/History/Downloads/Bookmarks).</summary>
    private static FrameworkElement CreateAppView(TabKind kind) => kind switch
    {
        TabKind.Settings => new PreferencesView(),
        TabKind.History => new HistoryView(),
        TabKind.Bookmarks => new BookmarksView(),
        TabKind.Downloads => new DownloadsView(),
        TabKind.Extensions => new ExtensionsView(),
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
            // favicon errored/timed out — keep the fallback icon
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

        // Internal app-tab: hide the browser toolbar and show the internal view.
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

        EnsureBrowser(tab);
        if (!_browsers.TryGetValue(tab, out var browser) || browser == null)
        {
            // CEF not ready — show a spinner; the browser will be created and shown when ready.
            _currentBrowser = null;
            BrowserHost.Content = _loadingSpinner;
            return;
        }

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

    private static string NormalizeUrl(string raw)
    {
        raw = (raw ?? "").Trim();
        // New tab / startup: open the search engine's home page (don't escape — escaping would break the URL).
        if (string.IsNullOrEmpty(raw) || raw == "about:newtab")
            return SearchEngines.GetEngineUrl(Zidimi.Browser.Models.AppSettings.Profile.SearchEngine);
        if (Uri.IsWellFormedUriString(raw, UriKind.Absolute)) return raw;
        if (raw.Contains('.') && !raw.Contains(' ')) return "https://" + raw;
        
        var engine = Zidimi.Browser.Models.AppSettings.Profile.SearchEngine;
        return Zidimi.Browser.Models.SearchEngines.BuildUrl(engine, Uri.EscapeDataString(raw));
    }

    private static Brush WithAlpha(Brush source, byte alpha)
    {
        if (source is SolidColorBrush scc)
            return new SolidColorBrush(Color.FromArgb(alpha, scc.Color.R, scc.Color.G, scc.Color.B));
        return source;
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
            SecurityIcon.Fill = WithAlpha(SecurityIcon.Stroke, 0x26);
            SecurityIcon.Data = Geometry.Parse("M8 10 V7 a4 4 0 0 1 8 0 v3 M5 10 H19 V20 H5 Z");
            SecurityIcon.ToolTip = LanguageManager.Instance["Browser_SecureConnHttps"];
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            // HTTP is not secure — a warning "info" icon
            SecurityIcon.Stroke = (Brush)FindResource("WarnBrush");
            SecurityIcon.Fill = WithAlpha(SecurityIcon.Stroke, 0x26);
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
            : Geometry.Parse("M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8 M21 3v5h-5");
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
            ? (Brush)FindResource("ZidimiPurpleBrush")
            : Brushes.Transparent;
        StarIcon.Stroke = active
            ? (Brush)FindResource("ZidimiPurpleBrush")
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
        NavigateTo(Zidimi.Browser.Models.AppSettings.Profile.HomePageUrl);
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
        AddressBarBorder.BorderBrush = (Brush)FindResource("ZidimiPurpleBrush");
        AddressBarBorder.Background = (Brush)FindResource("OmniboxFocusBgBrush");
        UpdateAutocomplete();
    }

    private void Address_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Delay the hide so the dropdown can be clicked
        AddressBox.Dispatcher.BeginInvoke(() =>
        {
            if (!AutocompletePopup.IsMouseOver)
                AutocompletePopup.IsOpen = false;
        }, System.Windows.Threading.DispatcherPriority.Background);

        AddressBarBorder.BorderBrush = (Brush)FindResource("StrokeBrush");
        AddressBarBorder.Background = (Brush)FindResource("ZidimiBgSurfaceBrush");
    }

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateAutocomplete();
    }

    private void UpdateAutocomplete()
    {
        if (AutocompletePopup == null || AutocompleteList == null) return;

        if (!AddressBox.IsKeyboardFocusWithin || !Zidimi.Browser.Models.AppSettings.Profile.SearchSuggestEnabled)
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
            var engine = Zidimi.Browser.Models.AppSettings.Profile.SearchEngine;
            var engineUrl = Zidimi.Browser.Models.SearchEngines.BuildUrl(engine, "");
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
        _vm.OpenAppTab(TabKind.History);
        MenuPopup.IsOpen = false;
    }
    private void Menu_Bookmarks(object sender, RoutedEventArgs e)
    {
        _vm.OpenAppTab(TabKind.Bookmarks);
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

    private void UpdateFindCount(int count, int activeMatchOrdinal)
    {
        if (FindBar.Visibility != Visibility.Visible || FindCount == null)
            return;

        if (count == 0)
        {
            FindCount.Text = LanguageManager.Instance["Page_Find_NoResults"];
            FindCount.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }
        else
        {
            FindCount.Text = string.Format("{0}/{1}", activeMatchOrdinal, count);
            FindCount.Foreground = (System.Windows.Media.Brush)FindResource("Ink400Brush");
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


    // ===== Profile / Guest mode =====
    private void Avatar_Click(object sender, RoutedEventArgs e)
    {
        LoadProfileAvatar();
        AvatarInitial.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_GuestInitial"] : LanguageManager.Instance["Browser_ZidimiInitial"];
        AvatarInitial2.Text = AvatarInitial.Text;
        ProfileNameText.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_Guest"] : LanguageManager.Instance["Browser_ZidimiBrowser"];
        ProfileModeText.Text = _vm.IsGuestMode ? LanguageManager.Instance["Browser_NoDataSaved"] : LanguageManager.Instance["Browser_DefaultProfile"];
        GuestModeCheck.IsChecked = _vm.IsGuestMode;
        AvatarPopup.IsOpen = !AvatarPopup.IsOpen;
    }

    /// <summary>Loads the profile's avatar.ico (platform profile folder) and shows it on the toolbar &
    /// profile popup. Falls back to the purple initial letter when the file is missing or guest mode.</summary>
    private void LoadProfileAvatar()
    {
        var guest = _vm.IsGuestMode;
        if (guest)
        {
            AvatarImage.Visibility = Visibility.Collapsed;
            AvatarImage2.Visibility = Visibility.Collapsed;
            AvatarFallback.Visibility = Visibility.Visible;
            AvatarFallback2.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var name = Zidimi.Browser.Models.AppSettings.Global.CurrentProfile;
            var ico = UserDataPaths.AvatarIconFile(name);
            if (File.Exists(ico))
            {
                var source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.UriSource = new Uri(ico);
                source.EndInit();
                source.Freeze();

                AvatarImage.Source = source;
                AvatarImage.Visibility = Visibility.Visible;
                AvatarImage2.Source = source;
                AvatarImage2.Visibility = Visibility.Visible;
                AvatarFallback.Visibility = Visibility.Collapsed;
                AvatarFallback2.Visibility = Visibility.Collapsed;
                return;
            }
        }
        catch { /* fall through to the default initial letter */ }

        AvatarImage.Visibility = Visibility.Collapsed;
        AvatarImage2.Visibility = Visibility.Collapsed;
        AvatarFallback.Visibility = Visibility.Visible;
        AvatarFallback2.Visibility = Visibility.Visible;
    }

    private void GuestMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_vm.IsGuestMode != (GuestModeCheck.IsChecked == true))
            _vm.ToggleGuestMode();
    }

    private void Avatar_ManageProfiles(object sender, RoutedEventArgs e)
    {
        AvatarPopup.IsOpen = false;
        new ProfileSelectorWindow { Owner = Window.GetWindow(this) }.ShowDialog();
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

        // Permissions placeholder (needs a CEF permission handler to get real values)
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
        var url = _currentTab?.Address?.Trim() ?? "";
        if (string.IsNullOrEmpty(url) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            ZidimiMessageBox.Show(LanguageManager.Instance["Cookie_NoSite"],
                LanguageManager.Instance["Browser_ZidimiBrowser"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
            return;
        }
        new CookieManagerWindow(url) { Owner = Window.GetWindow(this) }.ShowDialog();
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
        ZidimiMessageBox.Show(LanguageManager.Instance["Browser_SiteSettingsWIP"],
            LanguageManager.Instance["Browser_ZidimiBrowser"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
    }

    // ===== Toolbar quick panels: History / Downloads / Extensions =====

    private void HistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        PopulateHistoryRecent();
        HistoryPopup.PlacementTarget = sender as FrameworkElement ?? HistoryBtn;
        HistoryPopup.IsOpen = !HistoryPopup.IsOpen;
    }

    private void DownloadsBtn_Click(object sender, RoutedEventArgs e)
    {
        PopulateDownloadsRecent();
        DownloadsPopup.PlacementTarget = sender as FrameworkElement ?? DownloadsBtn;
        DownloadsPopup.IsOpen = !DownloadsPopup.IsOpen;
    }

    private void ExtensionsBtn_Click(object sender, RoutedEventArgs e)
    {
        PopulateExtensions();
        ExtensionsPopup.PlacementTarget = sender as FrameworkElement ?? ExtensionsBtn;
        ExtensionsPopup.IsOpen = !ExtensionsPopup.IsOpen;
    }

    private void PopulateHistoryRecent()
    {
        HistoryRecentList.Items.Clear();
        var host = Window.GetWindow(this);
        foreach (var entry in _vm.History.OrderByDescending(h => h.VisitedAt).Take(10))
        {
            var item = new ListBoxItem
            {
                Content = string.IsNullOrWhiteSpace(entry.Title) ? entry.Url : entry.Title,
                ToolTip = entry.Url,
                Tag = entry.Url,
                Margin = new Thickness(2, 1, 2, 1),
                Padding = new Thickness(10, 5, 10, 5),
            };
            HistoryRecentList.Items.Add(item);
        }
    }

    private void HistoryRecentList_LeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HistoryRecentList.SelectedItem is ListBoxItem li && li.Tag is string url && !string.IsNullOrEmpty(url))
        {
            HistoryPopup.IsOpen = false;
            _vm.NewTab(url);
        }
    }

    private void HistoryPopup_ViewAll(object sender, RoutedEventArgs e)
    {
        HistoryPopup.IsOpen = false;
        _vm.OpenAppTab(TabKind.History);
    }

    private void PopulateDownloadsRecent()
    {
        DownloadsRecentList.Items.Clear();
        var items = _vm.Downloads.Take(10).ToList();
        foreach (var d in items)
        {
            var display = string.IsNullOrWhiteSpace(d.SuggestedFileName) ? d.Url : d.SuggestedFileName;
            var status = d.IsComplete ? LanguageManager.Instance["Browser_DlComplete"]
                        : d.IsCancelled ? LanguageManager.Instance["Browser_DlCancelled"]
                        : LanguageManager.Instance["Browser_DlInProgress"];
            var item = new ListBoxItem { Tag = d, Padding = new Thickness(10, 5, 10, 5) };
            item.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = display, FontSize = 13, Foreground = (Brush)FindResource("Ink100Brush"), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = status, FontSize = 11, Foreground = (Brush)FindResource("Ink400Brush") },
                }
            };
            DownloadsRecentList.Items.Add(item);
        }
    }

    private void DownloadsPopup_ViewAll(object sender, RoutedEventArgs e)
    {
        DownloadsPopup.IsOpen = false;
        _vm.OpenAppTab(TabKind.Downloads);
    }

    private void OnExtensionsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RefreshExtensionSurfaces));
    }

    private void RefreshExtensionSurfaces()
    {
        PopulatePinnedExtensionsToolbar();
        PopulateExtensions();
    }

    private void PopulatePinnedExtensionsToolbar()
    {
        PinnedExtensionsHost.Children.Clear();

        var pinned = ExtensionService.Instance.InstalledExtensions
            .Where(ext => ext.IsPinned)
            .OrderBy(ext => ext.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var ext in pinned)
        {
            var button = new Button
            {
                Style = (Style)FindResource("ToolIconButton"),
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 2, 0),
                Tag = ext,
                ToolTip = ext.IsEnabled ? ext.Name : $"{ext.Name} ({ResolveLang("Browser_ExtensionOff", "Off")})",
                Opacity = ext.IsEnabled ? 1.0 : 0.5,
                Content = CreateExtensionIconElement(ext, 16)
            };
            button.Click += PinnedExtensionButton_Click;
            PinnedExtensionsHost.Children.Add(button);
        }

        ExtensionsToolbarSeparator.Visibility = pinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement CreateExtensionIconElement(ExtensionInfo ext, double size)
    {
        var bitmap = TryLoadExtensionBitmap(ext.IconPath);
        if (bitmap != null)
        {
            return new Image
            {
                Source = bitmap,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };
        }

        return new Path
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Fill = (Brush)FindResource("Ink300Brush"),
            Data = Geometry.Parse("M20.5 11H19V7a2 2 0 0 0-2-2h-4V3.5A2.5 2.5 0 0 0 10.5 1 2.5 2.5 0 0 0 8 3.5V5H4a2 2 0 0 0-2 2v4h1.5a2.5 2.5 0 0 1 0 5H2v3.8a2 2 0 0 0 2 2h4c0-1.55 1.12-2.5 2.5-2.5s2.5 1.12 2.5 2.5h4a2 2 0 0 0 2-2v-4h1.5a2.5 2.5 0 0 1 0-5z")
        };
    }

    private BitmapSource? TryLoadExtensionBitmap(string? iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) return null;

        try
        {
            // Stream-based WIC decoding avoids Uri escaping issues for extension folders
            // containing #, %, spaces, non-ASCII characters, etc. BitmapCacheOption.OnLoad
            // releases the extension file immediately so updates can replace it later.
            using var stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame == null) return null;
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionIcon", ex, iconPath);
            return null;
        }
    }

    private string ResolveLang(string key, string fallback)
    {
        var value = LanguageManager.Instance[key];
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ExtensionInfo ext)
        {
            ExtensionService.Instance.TogglePinned(ext, !ext.IsPinned);
        }
    }

    private FrameworkElement CreatePinIconElement(bool isPinned)
    {
        // Vector push-pin keeps the icon consistent with the current Zidimi theme.
        return new Path
        {
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            Stroke = (Brush)FindResource(isPinned ? "ZidimiPurpleLightBrush" : "Ink300Brush"),
            Fill = isPinned ? (Brush)FindResource("ZidimiPurpleLightBrush") : Brushes.Transparent,
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse("M8 2 H16 L15 7 L19 11 V13 H13 V21 L12 23 L11 21 V13 H5 V11 L9 7 Z")
        };
    }

    private async void PinnedExtensionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not ExtensionInfo ext) return;
        if (_currentBrowser == null || !_currentBrowser.IsBrowserInitialized || _currentBrowser.IsDisposed)
        {
            PopulateExtensions();
            ExtensionsPopup.PlacementTarget = fe;
            ExtensionsPopup.IsOpen = true;
            return;
        }

        // This is the real Chromium equivalent of clicking an extension toolbar action.
        // If the manifest has action.default_popup/browser_action.default_popup Chromium
        // opens that exact popup; otherwise the extension's onClicked handler is invoked.
        var result = await ExtensionService.Instance.TriggerDefaultActionAsync(ext, _currentBrowser);
        if (!result.success)
        {
            AppLogger.Log("ExtensionAction", $"{ext.Name}: {result.message}");
            ZidimiMessageBox.Show(
                string.IsNullOrWhiteSpace(result.message) ? "Unable to open the extension action." : result.message,
                ext.Name, ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
        }
    }

    private void PopulateExtensions()
    {
        ExtensionsList.Items.Clear();
        var extensions = ExtensionService.Instance.InstalledExtensions
            .OrderByDescending(e => e.IsPinned)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (extensions.Count == 0)
        {
            var none = new ListBoxItem
            {
                Content = LanguageManager.Instance["Browser_NoExtensions"],
                Padding = new Thickness(10, 5, 10, 5),
            };
            none.IsHitTestVisible = false;
            ExtensionsList.Items.Add(none);
            return;
        }

        foreach (var ext in extensions)
        {
            var item = new ListBoxItem
            {
                Padding = new Thickness(8, 6, 8, 6),
                Tag = ext
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = (Brush)FindResource("ZidimiBgElevatedBrush"),
                Margin = new Thickness(0, 0, 10, 0),
                Child = CreateExtensionIconElement(ext, 16)
            };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            var textPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            textPanel.Children.Add(new TextBlock
            {
                Text = ext.Name,
                FontSize = 13,
                Foreground = (Brush)FindResource("Ink100Brush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = ext.IsEnabled ? (ext.IsPinned ? ResolveLang("Ext_Pinned", "Pinned on toolbar") : ResolveLang("Ext_NotPinned", "Not pinned"))
                                     : $"{ResolveLang("Browser_ExtensionOff", "Off")} • {(ext.IsPinned ? ResolveLang("Ext_Pinned", "Pinned on toolbar") : ResolveLang("Ext_NotPinned", "Not pinned"))}",
                FontSize = 11,
                Foreground = (Brush)FindResource("Ink400Brush")
            });
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var pinButton = new Button
            {
                Style = (Style)FindResource("ToolIconButton"),
                Width = 28,
                Height = 28,
                Tag = ext,
                ToolTip = ext.IsPinned ? ResolveLang("Ext_Unpin", "Unpin from toolbar") : ResolveLang("Ext_Pin", "Pin to toolbar"),
                Content = CreatePinIconElement(ext.IsPinned)
            };
            pinButton.Click += TogglePin_Click;
            Grid.SetColumn(pinButton, 2);
            grid.Children.Add(pinButton);

            item.Content = grid;
            ExtensionsList.Items.Add(item);
        }
    }

    private void ExtensionsPopup_Manage(object sender, RoutedEventArgs e)
    {
        ExtensionsPopup.IsOpen = false;
        _vm.OpenAppTab(TabKind.Extensions);
    }

    private static bool IsChromeWebStoreUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals("chromewebstore.google.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChromeWebStoreDetailUrl(string? url)
    {
        if (!IsChromeWebStoreUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.AbsolutePath.StartsWith("/detail/", StringComparison.OrdinalIgnoreCase)
            && ExtensionService.ExtractExtensionId(url!) != null;
    }

    /// <summary>
    /// Fired when the user clicks "Add to Chrome" on the Web Store. Zidimi bypasses the
    /// Web Store native installer, validates/unpacks the CRX, then loads the unpacked folder
    /// into Chromium's extension runtime.
    /// </summary>
    private async void HandleWebStoreCrxInstall(string url)
    {
        if (_extensionInstallInProgress) return;
        _extensionInstallInProgress = true;

        AppLogger.Log("ExtensionInstallUi", $"Starting Web Store install. Input={url}");
        var owner = Window.GetWindow(this);
        try
        {
            var confirm = ZidimiMessageBox.Show(
                LanguageManager.Instance["Ext_InstallFromStoreConfirm"],
                LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.YesNo,
                ZidimiMessageBoxImage.Question,
                owner);

            if (confirm != ZidimiMessageBoxResult.Yes) return;

            var context = _vm.GetRequestContext();
            var res = await ExtensionService.Instance.DownloadAndInstallFromWebStoreAsync(url, context);

            // The browser is already initialized in the common install flow, so make the
            // extension live immediately. This also makes action.default_popup available
            // without requiring a browser restart.
            if (res.success && res.ext != null && _currentBrowser is { IsBrowserInitialized: true, IsDisposed: false })
            {
                var runtime = await ExtensionService.Instance.EnsureExtensionRuntimeLoadedAsync(res.ext, _currentBrowser);
                if (!runtime.success)
                    AppLogger.Log("ExtensionRuntime", $"Installed {res.ext.Name}, but runtime load failed: {runtime.message}");
            }

            RefreshExtensionSurfaces();

            var icon = res.success ? ZidimiMessageBoxImage.Information : ZidimiMessageBoxImage.Warning;
            ZidimiMessageBox.Show(res.message, LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.OK, icon, owner);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionInstallUi", ex);
            ZidimiMessageBox.Show(ex.Message, LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, owner);
        }
        finally
        {
            _extensionInstallInProgress = false;
        }
    }
}

