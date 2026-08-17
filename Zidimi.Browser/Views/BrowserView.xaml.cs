using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;
using CefSharp;
using CefSharp.Enums;
using CefSharp.Wpf.HwndHost;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;
using Path = System.Windows.Shapes.Path;
using WinForms = System.Windows.Forms;

namespace Zidimi.Browser.Views;

public partial class BrowserView : UserControl, IDisposable
{
    private readonly MainViewModel _vm;
    private TabViewModel? _currentTab;
    private ChromiumWebBrowser? _currentBrowser;
    private readonly Dictionary<TabViewModel, ChromiumWebBrowser?> _browsers = new();
    private readonly Dictionary<TabViewModel, FrameworkElement> _appViews = new();
    private readonly Dictionary<ChromiumWebBrowser, (DependencyPropertyDescriptor Descriptor, EventHandler Handler)> _addressObservers = new();
    private readonly Dictionary<TabViewModel, CancellationTokenSource> _faviconLoads = new();
    private FrameworkElement? _visibleSurface;
    private int _backgroundBrowserWarmupGeneration;
    private int _disposed;

    // One HTTP pool for favicons instead of creating a new socket pool on every navigation.
    private static readonly HttpClient FaviconHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 4,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromSeconds(6),
    };
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> FaviconCache =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressAddressUpdate;
    private readonly System.Collections.Generic.List<Models.AutocompleteSuggestion> _allSuggestions = new();
    private readonly LoadingSpinner _loadingSpinner = new();
    private readonly System.Windows.Threading.DispatcherTimer _autocompleteTimer;
    private ExtensionActionPopup? _extensionActionPopup;

    // Docked DevTools host. CEF renders DevTools as a native child HWND inside this WinForms
    // panel, which is itself hosted by WPF through WindowsFormsHost.
    private WinForms.Panel? _devToolsPanel;
    private ChromiumWebBrowser? _devToolsOwner;
    private IBrowser? _devToolsBrowser;
    private bool _devToolsOpen;

    public BrowserView()
    {
        InitializeComponent();
        _autocompleteTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(120),
        };
        _autocompleteTimer.Tick += (_, _) =>
        {
            _autocompleteTimer.Stop();
            UpdateAutocomplete();
        };

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

    /// <summary>
    /// CEF just finished initializing. Real browsers keep every open web tab alive, not only the
    /// selected one, so instantiate all Zidimi web tabs into the shared profile runtime now.
    /// </summary>
    private void OnCefReady()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(OnCefReady));
            return;
        }

        EnsureAllWebBrowsers();
        SwitchToTab(_vm.ActiveTab);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown += OnPreviewKeyDown;

        ExtensionService.Instance.ExtensionsChanged -= OnExtensionsChanged;
        ExtensionService.Instance.ExtensionsChanged += OnExtensionsChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
        // Do not scan extension folders/manifests during the first WPF load. Runtime
        // initialization refreshes metadata after Chromium itself is ready.
        RefreshExtensionSurfaces();

        if (_currentBrowser?.IsBrowserInitialized == true)
            _ = ExtensionService.Instance.EnsureProfileRuntimeLoadedAsync(_currentBrowser);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Interlocked.Increment(ref _backgroundBrowserWarmupGeneration);
        _autocompleteTimer.Stop();
        CloseExtensionActionPopup();
        CloseDevToolsDock();
        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown -= OnPreviewKeyDown;

        ExtensionService.Instance.ExtensionsChanged -= OnExtensionsChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(ThemeManager.AppTheme changedTheme)
    {
        // Most XAML uses DynamicResource and updates automatically. A few browser surfaces are
        // created in code-behind (extension rows, recent lists, site-info rows) and therefore
        // hold the brush instance returned by FindResource. Rebuild/rebind those small surfaces
        // so a live theme switch never leaves stale colors behind.
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnThemeChanged(ThemeManager.Current));
            return;
        }

        UpdateSecurityIcon(_currentTab?.Address ?? AddressBox.Text);
        if (_currentTab != null)
            UpdateStarState(_currentTab);

        if (AddressBox.IsKeyboardFocusWithin)
        {
            AddressBarBorder.SetResourceReference(Border.BorderBrushProperty, "ZidimiPurpleBrush");
            AddressBarBorder.SetResourceReference(Border.BackgroundProperty, "OmniboxFocusBgBrush");
        }
        else
        {
            AddressBarBorder.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
            AddressBarBorder.SetResourceReference(Border.BackgroundProperty, "ZidimiBgSurfaceBrush");
        }

        PopulateHistoryRecent();
        PopulateDownloadsRecent();
        RefreshExtensionSurfaces();
        if (AutocompletePopup.IsOpen)
            UpdateAutocomplete();
        if (SiteInfoPopup.IsOpen)
            _ = UpdateSiteInfoAsync();
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
        var target = tabs[next];
        if (target.TabId > 0) _vm.SelectTabById(target.TabId);
        else _vm.ActiveTab = target;
    }

    private void JumpToTab(int index)
    {
        if (index < 0 || index >= _vm.Tabs.Count) return;
        var target = _vm.Tabs[index];
        if (target.TabId > 0) _vm.SelectTabById(target.TabId);
        else _vm.ActiveTab = target;
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
        // A Move notification contains the same tab in both OldItems and NewItems. Treating it
        // like Remove+Add would dispose the live CefBrowser simply because the user dragged a
        // tab. Browser-like tab reordering must only change visual/runtime order, never lifecycle.
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (TabViewModel t in e.NewItems) SubscribeTab(t);
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (TabViewModel t in e.OldItems) UnsubscribeTab(t);
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                    foreach (TabViewModel t in e.OldItems) UnsubscribeTab(t);
                if (e.NewItems != null)
                    foreach (TabViewModel t in e.NewItems) SubscribeTab(t);
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                // ObservableCollection.Clear() does not provide OldItems. Reconcile against the
                // dictionaries so profile/guest switches close the old native tabs exactly once.
                var liveTabs = _vm.Tabs.ToHashSet();
                foreach (var oldTab in _browsers.Keys.Concat(_appViews.Keys).Distinct().ToArray())
                    if (!liveTabs.Contains(oldTab)) UnsubscribeTab(oldTab);
                foreach (var tab in _vm.Tabs.ToArray())
                    if (!_browsers.ContainsKey(tab) && !_appViews.ContainsKey(tab)) SubscribeTab(tab);
                break;

            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                // Preserve the browser and its extension/content-script runtime. Only the tab
                // index/order changes below.
                break;
        }

        SyncExtensionTabOrder();
    }

    /// <summary>
    /// Mirrors the WPF TabStrip order into the profile-wide extension tab registry. Only real web
    /// tabs with an initialized CefBrowser.Identifier participate; background tabs stay present.
    /// </summary>
    private void SyncExtensionTabOrder()
    {
        if (_vm.IsGuestMode) return;
        var context = _vm.GetRequestContext();
        if (context == null) return;

        ExtensionRuntimeCoordinator.Instance.SetTabOrder(
            context,
            _vm.Tabs
                .Where(tab => tab.Kind == TabKind.Web && tab.TabId > 0)
                .Select(tab => tab.TabId));
    }

    private void SubscribeTab(TabViewModel tab)
    {
        // Internal app-tab (Settings/History/...): don't create a ChromiumWebBrowser,
        // hide the toolbar, and show the corresponding view. (spec 7.4 — Settings opens in a tab)
        if (tab.Kind != TabKind.Web)
        {
            _appViews[tab] = CreateAppView(tab);
            return;
        }
        // Every open web tab receives a live Chromium browser as soon as CEF is ready. Keeping
        // background tabs instantiated is what allows extension content scripts/messaging/network
        // hooks to continue working even when the user selects another tab.
        _browsers[tab] = null;
        if (App.CefReady)
        {
            if (ReferenceEquals(tab, _vm.ActiveTab))
                EnsureBrowser(tab);
            else
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_vm.Tabs.Contains(tab) && tab.Kind == TabKind.Web)
                        EnsureBrowser(tab);
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private void EnsureAllWebBrowsers()
    {
        if (!App.CefReady) return;

        // Restore the selected tab immediately so first paint/navigation is fast. Background tabs
        // are warmed one at a time at ContextIdle priority; they still become fully live Chromium
        // tabs (and therefore participate in extension/content-script runtime), but session restore
        // no longer creates every renderer/HTTP load in the same dispatcher turn.
        if (_vm.ActiveTab is { Kind: TabKind.Web } active)
            EnsureBrowser(active);

        var pending = _vm.Tabs
            .Where(t => t.Kind == TabKind.Web && !ReferenceEquals(t, _vm.ActiveTab))
            .ToArray();
        var generation = Interlocked.Increment(ref _backgroundBrowserWarmupGeneration);
        _ = WarmBackgroundBrowsersAsync(pending, generation);
    }

    private async Task WarmBackgroundBrowsersAsync(TabViewModel[] tabs, int generation)
    {
        // Creating several Chromium renderers at once is a classic session-restore thundering herd:
        // every tab starts renderer initialization, extension injection and network work together.
        // Keep every background tab live as requested, but stagger creation slightly so foreground
        // input/paint wins and CPU/disk/network peaks stay flatter.
        foreach (var tab in tabs)
        {
            if (generation != Volatile.Read(ref _backgroundBrowserWarmupGeneration)) return;

            await Dispatcher.InvokeAsync(() =>
            {
                if (generation != Volatile.Read(ref _backgroundBrowserWarmupGeneration)) return;
                if (_vm.Tabs.Contains(tab) && tab.Kind == TabKind.Web)
                    EnsureBrowser(tab);
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            if (generation != Volatile.Read(ref _backgroundBrowserWarmupGeneration)) return;
            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    /// <summary>Create a live ChromiumWebBrowser for a web tab if none exists.</summary>
    private void EnsureBrowser(TabViewModel tab)
    {
        if (tab.Kind != TabKind.Web) return;
        if (_browsers.TryGetValue(tab, out var existing) && existing != null && !existing.IsDisposed) return;
        if (!App.CefReady) return;

        var browser = CreateBrowser(tab);
        _browsers[tab] = browser;
        _vm.RegisterBrowser(tab, browser);
        AttachSurface(browser);
    }

    private ChromiumWebBrowser CreateBrowser(TabViewModel tab)
    {
        var initialAddress = NormalizeUrl(tab.Address);
        ChromiumTopLevelTargetRouter.Instance.ExpectZidimiNavigation(initialAddress);

        // Capture the context once for the entire lifetime of this Chromium browser. This makes
        // profile/extension isolation deterministic even if the user switches profiles while a
        // browser is still finishing native initialization.
        var requestContext = _vm.GetRequestContext();
        var browser = new ChromiumWebBrowser
        {
            Address = initialAddress,
            RequestContext = requestContext,
            BrowserSettings = BuildBrowserSettings()
        };

        browser.IsBrowserInitializedChanged += async (_, _) =>
        {
            if (!browser.IsBrowserInitialized || browser.IsDisposed) return;

            try
            {
                // The native browser id belongs to the tab itself, not to extension support.
                // Bind it for every web tab (including Guest) so Zidimi/TabStrip consistently use
                // the same Chromium TabId for selection, close, move, mute and browser lookup.
                var nativeTabId = browser.GetBrowser()?.Identifier ?? 0;
                if (nativeTabId > 0)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (browser.IsDisposed || !_browsers.ContainsKey(tab)) return;
                        _vm.BindBrowserTabId(tab, nativeTabId, browser);
                    });
                }

                // Guest browsing intentionally does not expose the normal profile extension
                // runtime. Normal profile tabs below are all registered, selected or not.
                if (_vm.IsGuestMode) return;

                await ExtensionService.Instance.EnsureProfileRuntimeLoadedAsync(browser);

                // Register every web browser — foreground or background — in the profile-wide
                // extension runtime. CefBrowser.Identifier is also the extension API tabId.
                var runtimeTabId = ExtensionRuntimeCoordinator.Instance.RegisterWebBrowser(browser, requestContext);
                if (runtimeTabId > 0 && ReferenceEquals(_currentTab, tab))
                    ExtensionRuntimeCoordinator.Instance.SetActiveTab(requestContext, runtimeTabId);

                // Native identifiers can arrive in a different order than WPF browser creation.
                // Re-sync after registration so extension tabs.query()/tabs.get() see TabStrip order.
                await Dispatcher.InvokeAsync(SyncExtensionTabOrder);

                await ChromiumTopLevelTargetRouter.Instance.RegisterBrowserAsync(browser);
            }
            catch (Exception ex)
            {
                AppLogger.Log("ExtensionRuntime", ex, "Loading profile extensions after browser initialization.");
            }
        };

        // Zoom level is handled automatically by CEF's partition.default_zoom_level

        // CEF handlers (spec 11.2)
        browser.LifeSpanHandler = new LifeSpanHandler(sourceName: "WebTab", browserCreated: OnCefBrowserCreated, browserClosed: OnCefBrowserClosed);
        var downloadHandler = new DownloadHandler();
        downloadHandler.DownloadStarted += entry =>
        {
            Dispatcher.BeginInvoke(() => _vm.AddDownload(entry));
        };
        downloadHandler.DownloadUpdated += entry =>
        {
            Dispatcher.BeginInvoke(() => _vm.UpdateDownload(entry));
        };
        browser.DownloadHandler = downloadHandler;
        browser.MenuHandler = new ContextMenuHandler((x, y) => OpenDevTools(x, y));
        browser.KeyboardHandler = new KeyboardHandler(() => OpenDevTools());
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
                if (ReferenceEquals(_currentBrowser, browser))
                {
                    UpdateReloadIcon(e.IsLoading);
                    UpdateLoadingProgress(e.IsLoading);
                }

                if (!e.IsLoading && tab.Kind == TabKind.Web && !_vm.IsGuestMode)
                {
                    _vm.RecordHistory(browser.Address ?? tab.Address ?? string.Empty, tab.Title ?? string.Empty);
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

        browser.TitleChanged += (s, args) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (tab.Kind != TabKind.Web) return;
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
        if (addressDescriptor != null)
        {
            EventHandler addressChanged = (_, _) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (browser.IsDisposed || tab.Kind != TabKind.Web) return;

                    var newUrl = browser.Address ?? "";
                    tab.Address = newUrl;
                    tab.RecordNavigation(newUrl);
                    if (ReferenceEquals(_currentTab, tab) && !_suppressAddressUpdate)
                    {
                        AddressBox.Text = newUrl;
                        UpdateSecurityIcon(newUrl);
                    }
                });
            };
            addressDescriptor.AddValueChanged(browser, addressChanged);
            _addressObservers[browser] = (addressDescriptor, addressChanged);
        }

        if (!tab.HasNavigationHistory)
            tab.ResetNavigation(browser.Address ?? NormalizeUrl(tab.Address));

        return browser;
    }

    /// <summary>
    /// Builds the per-tab BrowserSettings that really belong at browser creation time. Persistent
    /// Chromium preferences (font sizes, zoom, downloads, cookies, DNT, Safe Browsing, ...) are
    /// stored through the profile RequestContext instead of being duplicated here.
    /// </summary>
    private static CefSharp.BrowserSettings BuildBrowserSettings()
    {
        var profile = Models.AppSettings.Profile;

        // BackgroundColor follows Chromium's active system/light/dark color scheme.
        var themeKey = Infrastructure.ThemeManager.NormalizeThemeKey(profile.Theme);
        var effectiveTheme = themeKey switch
        {
            "light" => Infrastructure.ThemeManager.AppTheme.Light,
            "dark" => Infrastructure.ThemeManager.AppTheme.Dark,
            _ => Infrastructure.ThemeManager.DetectSystemTheme()
        };
        uint bg = effectiveTheme == Infrastructure.ThemeManager.AppTheme.Dark
            ? 0xFF1E1F24u
            : 0xFFFFFFFFu;

        return new CefSharp.BrowserSettings
        {
            // HwndHost is windowed Chromium; WindowlessFrameRate is an OSR setting and does not
            // improve this path. Leave Chromium to schedule frames naturally.
            BackgroundColor = bg,
        };
    }

    private void DisposeBrowserForTab(TabViewModel tab)
    {
        ReleaseBrowserResources(tab, removeSlot: false);
        SyncExtensionTabOrder();
    }

    private void UnsubscribeTab(TabViewModel tab)
    {
        ReleaseBrowserResources(tab, removeSlot: true);

        if (_appViews.Remove(tab, out var appView))
            RemoveSurface(appView);
        if (ReferenceEquals(_currentTab, tab)) _currentTab = null;
    }

    /// <summary>
    /// Single exit path for a Chromium tab. Remove shell/runtime references before Dispose so
    /// browser-close callbacks cannot re-enter and find a half-alive tab. This also removes the
    /// DependencyPropertyDescriptor observer (which otherwise holds a strong reference) and
    /// cancels stale favicon work. Closing one tab therefore releases its renderer/host resources
    /// without touching any remaining tab/request context.
    /// </summary>
    private void ReleaseBrowserResources(TabViewModel tab, bool removeSlot)
    {
        _faviconLoads.Remove(tab, out var faviconCts);
        if (faviconCts != null)
        {
            try { faviconCts.Cancel(); } catch { }
            faviconCts.Dispose();
        }

        _browsers.TryGetValue(tab, out var browser);
        if (removeSlot) _browsers.Remove(tab);
        else _browsers[tab] = null;

        if (browser != null)
        {
            if (ReferenceEquals(_devToolsOwner, browser)) CloseDevToolsDock();
            ExtensionRuntimeCoordinator.Instance.UnregisterWebBrowser(browser);
            ChromiumTopLevelTargetRouter.Instance.UnregisterBrowser(browser);

            if (_addressObservers.Remove(browser, out var observer))
            {
                try { observer.Descriptor.RemoveValueChanged(browser, observer.Handler); }
                catch { }
            }

            RemoveSurface(browser);
            try { browser.Dispose(); }
            catch (Exception ex) { AppLogger.Log("Tabs", ex, $"Disposing browser for tab {tab.TabId}."); }
        }

        _vm.UnregisterBrowser(tab);
    }

    /// <summary>
    /// Keeps every browser control attached to the visual tree. Inactive Chromium tabs are
    /// Collapsed (not removed/disposed), eliminating WPF measure/arrange work while their native
    /// browser, content scripts and extension messaging remain alive like normal background tabs.
    /// </summary>
    private void AttachSurface(FrameworkElement surface)
    {
        if (ReferenceEquals(surface.Parent, BrowserHost)) return;
        if (surface.Parent is Panel oldPanel) oldPanel.Children.Remove(surface);
        else if (surface.Parent is ContentControl oldContent && ReferenceEquals(oldContent.Content, surface))
            oldContent.Content = null;

        surface.HorizontalAlignment = HorizontalAlignment.Stretch;
        surface.VerticalAlignment = VerticalAlignment.Stretch;
        surface.Visibility = Visibility.Collapsed;
        BrowserHost.Children.Add(surface);
    }

    private void RemoveSurface(FrameworkElement surface)
    {
        if (ReferenceEquals(_visibleSurface, surface))
            _visibleSurface = null;
        if (ReferenceEquals(surface.Parent, BrowserHost))
            BrowserHost.Children.Remove(surface);
    }

    private void ShowSurface(FrameworkElement? activeSurface)
    {
        // Tab switching is a hot path. Do not walk every background Chromium control just to hide
        // the one surface that was previously visible; with many tabs that turns a simple switch
        // into O(n) WPF dependency-property/layout work. Background controls stay attached and
        // Collapsed so their Chromium runtime remains alive while WPF ignores them for layout.
        if (ReferenceEquals(_visibleSurface, activeSurface)) return;

        if (_visibleSurface != null)
            _visibleSurface.Visibility = Visibility.Collapsed;

        if (activeSurface != null)
        {
            AttachSurface(activeSurface);
            activeSurface.Visibility = Visibility.Visible;
        }

        _visibleSurface = activeSurface;
    }

    /// <summary>Create the native view for a zidimi:// page.</summary>
    private FrameworkElement CreateAppView(TabViewModel tab)
    {
        if (tab.Kind == TabKind.Settings)
        {
            var view = new PreferencesView();
            if (InternalUrlRouter.TryParse(tab.Address, out var route) &&
                !string.IsNullOrWhiteSpace(route.SettingsSection))
            {
                view.NavigateToSection(route.SettingsSection, notifyRoute: false);
            }

            view.SectionChanged += section => OnSettingsSectionChanged(tab, section);
            return view;
        }

        return tab.Kind switch
        {
            _ => new TextBlock { Text = "?" },
        };
    }

    private void OnSettingsSectionChanged(TabViewModel tab, string section)
    {
        if (tab.Kind != TabKind.Settings) return;

        var url = InternalUrlRouter.UrlForSettingsSection(section);
        tab.Address = url;
        tab.RecordNavigation(url);

        if (!ReferenceEquals(_currentTab, tab)) return;
        _suppressAddressUpdate = true;
        AddressBox.Text = url;
        _suppressAddressUpdate = false;
        UpdateSecurityIcon(url);
    }

    private async void LoadFaviconAsync(TabViewModel tab, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (FaviconCache.TryGetValue(url, out var weak) && weak.TryGetTarget(out var cached))
        {
            tab.Favicon = cached;
            return;
        }

        if (_faviconLoads.Remove(tab, out var previous))
        {
            try { previous.Cancel(); } catch { }
            previous.Dispose();
        }

        var cts = new CancellationTokenSource();
        _faviconLoads[tab] = cts;
        try
        {
            var bytes = await FaviconHttpClient.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) return;

            using var ms = new MemoryStream(bytes, writable: false);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            FaviconCache[url] = new WeakReference<BitmapSource>(bmp);

            // Avoid letting dead weak-reference keys grow forever on long browsing sessions.
            if (FaviconCache.Count > 256)
            {
                foreach (var pair in FaviconCache.ToArray())
                    if (!pair.Value.TryGetTarget(out _)) FaviconCache.TryRemove(pair.Key, out _);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (_vm.Tabs.Contains(tab) && !cts.IsCancellationRequested)
                    tab.Favicon = bmp;
            });
        }
        catch (OperationCanceledException)
        {
            // A newer favicon request or tab close superseded this one.
        }
        catch
        {
            // favicon errored/timed out — keep the fallback icon
        }
        finally
        {
            if (_faviconLoads.TryGetValue(tab, out var current) && ReferenceEquals(current, cts))
                _faviconLoads.Remove(tab);
            cts.Dispose();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveTab))
            SwitchToTab(_vm.ActiveTab);
    }

    private void SwitchToTab(TabViewModel? tab)
    {
        CloseExtensionActionPopup();
        if (_devToolsOpen && !ReferenceEquals(_currentTab, tab)) CloseDevToolsDock();
        ToolbarRow.Visibility = Visibility.Visible;

        if (tab == null)
        {
            ShowSurface(null);
            EmptyHint.Visibility = Visibility.Visible;
            _currentTab = null;
            _currentBrowser = null;
            ExtensionRuntimeCoordinator.Instance.SetActiveWebBrowser(null);
            return;
        }

        EmptyHint.Visibility = Visibility.Collapsed;
        _currentTab = tab;

        if (tab.Kind != TabKind.Web)
        {
            PresentInternalTab(tab);
            return;
        }

        EnsureBrowser(tab);
        if (!_browsers.TryGetValue(tab, out var browser) || browser == null)
        {
            // CEF not ready — keep the full toolbar visible while the browser starts.
            _currentBrowser = null;
            ExtensionRuntimeCoordinator.Instance.SetActiveWebBrowser(null);
            ShowSurface(_loadingSpinner);
            _suppressAddressUpdate = true;
            AddressBox.Text = NormalizeUrl(tab.Address);
            _suppressAddressUpdate = false;
            StarBtn.IsEnabled = true;
            return;
        }

        _currentBrowser = browser;
        ShowSurface(browser);
        if (tab.TabId > 0)
            ExtensionRuntimeCoordinator.Instance.SetActiveTab(browser.RequestContext, tab.TabId);
        else
            ExtensionRuntimeCoordinator.Instance.SetActiveWebBrowser(browser);
        var address = browser.Address ?? tab.Address ?? string.Empty;
        _suppressAddressUpdate = true;
        AddressBox.Text = address;
        _suppressAddressUpdate = false;
        UpdateSecurityIcon(address);
        UpdateReloadIcon(tab.IsLoading);
        UpdateLoadingProgress(tab.IsLoading);
        StarBtn.IsEnabled = true;
        UpdateStarState(tab);
    }

    private void PresentInternalTab(TabViewModel tab)
    {
        _currentBrowser = null;
        ExtensionRuntimeCoordinator.Instance.SetActiveWebBrowser(null);
        UpdateLoadingProgress(false);
        UpdateReloadIcon(false);
        StarBtn.IsEnabled = false;

        if (!_appViews.TryGetValue(tab, out var view))
        {
            view = CreateAppView(tab);
            _appViews[tab] = view;
        }
        else if (view is PreferencesView preferences &&
                 InternalUrlRouter.TryParse(tab.Address, out var route) &&
                 !string.IsNullOrWhiteSpace(route.SettingsSection))
        {
            preferences.NavigateToSection(route.SettingsSection, notifyRoute: false);
        }

        AttachSurface(view);
        ShowSurface(view);
        _suppressAddressUpdate = true;
        AddressBox.Text = tab.Address ?? InternalUrlRouter.UrlForKind(tab.Kind);
        _suppressAddressUpdate = false;
        UpdateSecurityIcon(AddressBox.Text);
    }

    private static string NormalizeUrl(string raw)
    {
        raw = (raw ?? "").Trim();
        // Let Chromium own the new-tab surface.
        if (string.IsNullOrEmpty(raw) || raw == "about:newtab")
            return "chrome://newtab/";
        if (Uri.IsWellFormedUriString(raw, UriKind.Absolute)) return raw;
        if (raw.Contains('.') && !raw.Contains(' ')) return "https://" + raw;

        var profile = Zidimi.Browser.Models.AppSettings.Profile;
        return Zidimi.Browser.Models.SearchEngines.BuildFromChromiumTemplate(
            profile.SearchUrlTemplate, profile.SearchEngine, raw);
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
        if (_currentTab == null) return;
        var target = _currentTab.MoveBack();
        if (!string.IsNullOrWhiteSpace(target))
            NavigateToLocation(target, recordHistory: false);
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTab == null) return;
        var target = _currentTab.MoveForward();
        if (!string.IsNullOrWhiteSpace(target))
            NavigateToLocation(target, recordHistory: false);
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTab == null) return;

        if (_currentTab.Kind != TabKind.Web)
        {
            // Native pages do not have a CEF frame. Rebuild the view from current
            // services/settings while keeping the same tab and zidimi:// address.
            _appViews.Remove(_currentTab);
            PresentInternalTab(_currentTab);
            return;
        }

        if (_currentBrowser == null) return;
        if (_currentTab.IsLoading) _currentBrowser.Stop();
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
        NavigateToLocation(input, recordHistory: true);
    }

    private void NavigateToLocation(string input, bool recordHistory)
    {
        if (_currentTab == null) return;

        if (InternalUrlRouter.TryParse(input, out var route))
        {
            NavigateToInternal(route, recordHistory);
            return;
        }

        // Do not hand unknown zidimi:// URLs to CEF. Zidimi native pages are
        // application routes, not network requests/custom CEF scheme handlers.
        if (Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var maybeInternal) &&
            maybeInternal.Scheme.Equals(InternalUrlRouter.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            _suppressAddressUpdate = true;
            AddressBox.Text = _currentTab.Address ?? string.Empty;
            _suppressAddressUpdate = false;
            return;
        }

        NavigateToWeb(NormalizeUrl(raw: input??""), recordHistory);
    }

    private void NavigateToInternal(InternalUrlRouter.Route route, bool recordHistory)
    {
        if (_currentTab == null) return;
        var tab = _currentTab;
        var wasWeb = tab.Kind == TabKind.Web;
        var kindChanged = tab.Kind != route.Kind;

        if (wasWeb)
            DisposeBrowserForTab(tab);

        tab.Kind = route.Kind;
        tab.Address = route.Url;
        tab.Title = InternalUrlRouter.TitleFor(route);
        tab.IsLoading = false;
        tab.Favicon = null;
        tab.IsAudioPlaying = false;
        if (recordHistory) tab.RecordNavigation(route.Url);

        if (kindChanged && _appViews.Remove(tab, out var oldAppView))
            RemoveSurface(oldAppView);

        PresentInternalTab(tab);
    }

    private void NavigateToWeb(string url, bool recordHistory)
    {
        if (_currentTab == null) return;
        var tab = _currentTab;

        tab.Kind = TabKind.Web;
        tab.Address = url;
        if (recordHistory) tab.RecordNavigation(url);
        if (_appViews.Remove(tab, out var oldAppView))
            RemoveSurface(oldAppView);

        EnsureBrowser(tab);
        if (!_browsers.TryGetValue(tab, out var browser) || browser == null)
        {
            SwitchToTab(tab);
            return;
        }

        _currentBrowser = browser;
        ShowSurface(browser);
        if (tab.TabId > 0)
            ExtensionRuntimeCoordinator.Instance.SetActiveTab(browser.RequestContext, tab.TabId);
        else
            ExtensionRuntimeCoordinator.Instance.SetActiveWebBrowser(browser);
        StarBtn.IsEnabled = true;
        if (!string.Equals(browser.Address, url, StringComparison.OrdinalIgnoreCase))
            browser.Load(url);

        _suppressAddressUpdate = true;
        AddressBox.Text = url;
        _suppressAddressUpdate = false;
        UpdateSecurityIcon(url);
        UpdateStarState(tab);
    }

    private void Address_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressBox.Dispatcher.BeginInvoke(new Action(AddressBox.SelectAll),
            System.Windows.Threading.DispatcherPriority.Input);
        AddressBarBorder.SetResourceReference(Border.BorderBrushProperty, "ZidimiPurpleBrush");
        AddressBarBorder.SetResourceReference(Border.BackgroundProperty, "OmniboxFocusBgBrush");
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

        AddressBarBorder.SetResourceReference(Border.BorderBrushProperty, "StrokeBrush");
        AddressBarBorder.SetResourceReference(Border.BackgroundProperty, "ZidimiBgSurfaceBrush");
    }

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce typing. History/bookmark matching is in-memory and fast, but performing it for
        // every intermediate TextChanged event causes needless allocations on rapid input.
        _autocompleteTimer.Stop();
        _autocompleteTimer.Start();
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

        // Keep suggestion construction bounded. The HistoryService keeps only a recent UI window
        // in memory, and the omnibox needs at most a handful of rows rather than allocating one
        // object for every matching history entry before calling Take(10).
        var historyMatches = 0;
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
                if (++historyMatches >= 6) break;
            }
        }

        var bookmarkMatches = 0;
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
                if (++bookmarkMatches >= 3) break;
            }
        }

        // Search suggestion
        if (!string.IsNullOrWhiteSpace(query))
        {
            var profile = Zidimi.Browser.Models.AppSettings.Profile;
            var engine = profile.SearchEngine;
            _allSuggestions.Add(new Models.AutocompleteSuggestion
            {
                Title = LanguageManager.Instance["Browser_SearchQuery"].Replace("{query}", query),
                Subtitle = LanguageManager.Instance["Browser_SearchOnEngine"].Replace("{engine}", engine),
                IconPath = "M15.5 14 h-.79 l-.28-.27 a6.5 6.5 0 1 0 -.7.7 l.27.28 v.79 l5 4.99 L20.49 19 z",
                TypeLabel = LanguageManager.Instance["Browser_Search"],
                TargetUrl = Zidimi.Browser.Models.SearchEngines.BuildFromChromiumTemplate(
                    profile.SearchUrlTemplate, engine, query)
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
        // CefSharp does not expose Chromium's BookmarkModel mutation API. Do not fake a bookmark
        // in a Zidimi collection or rewrite Bookmarks JSON behind Chromium's back; hand management
        // to Chromium's native bookmark WebUI instead.
        _vm.OpenAppTab(TabKind.Bookmarks);
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
        var profileDisplayName = AppSettings.CurrentProfileDisplayName;
        AvatarInitial.Text = _vm.IsGuestMode
            ? LanguageManager.Instance["Browser_GuestInitial"]
            : (string.IsNullOrWhiteSpace(profileDisplayName) ? "Z" : profileDisplayName[..1].ToUpperInvariant());
        AvatarInitial2.Text = AvatarInitial.Text;
        ProfileNameText.Text = _vm.IsGuestMode
            ? LanguageManager.Instance["Browser_Guest"]
            : profileDisplayName;
        ProfileModeText.Text = _vm.IsGuestMode
            ? LanguageManager.Instance["Browser_NoDataSaved"]
            : $@"User Data\{AppSettings.Global.CurrentProfile}";
        GuestModeCheck.IsChecked = _vm.IsGuestMode;
        AvatarPopup.IsOpen = !AvatarPopup.IsOpen;
    }

    /// <summary>Creates a presentation-only in-memory avatar without writing an extra file into Chromium's profile folder.</summary>
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
            var profileId = Zidimi.Browser.Models.AppSettings.Global.CurrentProfile;
            var source = AvatarGenerator.CreateImageSource(profileId);
            AvatarImage.Source = source;
            AvatarImage.Visibility = Visibility.Visible;
            AvatarImage2.Source = source;
            AvatarImage2.Visibility = Visibility.Visible;
            AvatarFallback.Visibility = Visibility.Collapsed;
            AvatarFallback2.Visibility = Visibility.Collapsed;
            return;
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

    private void OpenDevTools(int inspectElementAtX = -1, int inspectElementAtY = -1)
    {
        var browser = _currentBrowser;
        if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized) return;

        // F12 behaves as a true toggle. Inspect Element while the same dock is already open
        // reuses the existing DevTools browser and asks CEF to inspect the requested point.
        if (_devToolsOpen && ReferenceEquals(_devToolsOwner, browser))
        {
            if (inspectElementAtX < 0 || inspectElementAtY < 0)
            {
                CloseDevToolsDock();
                return;
            }

            ShowDevToolsInCurrentDock(browser, inspectElementAtX, inspectElementAtY);
            return;
        }

        if (_devToolsOpen) CloseDevToolsDock();

        _devToolsOwner = browser;
        _devToolsOpen = true;
        DevToolsColumn.Width = new GridLength(Math.Max(420, Math.Min(620, ActualWidth * 0.42)));
        DevToolsSplitterColumn.Width = new GridLength(5);
        DevToolsDock.Visibility = Visibility.Visible;
        DevToolsSplitter.Visibility = Visibility.Visible;

        var panel = new WinForms.Panel
        {
            Dock = WinForms.DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(32, 33, 36)
        };
        _devToolsPanel = panel;
        DevToolsHost.Child = panel;
        panel.CreateControl();
        UpdateLayout();

        // Wait for WindowsFormsHost to receive its final arranged size before creating the
        // DevTools child HWND, otherwise CEF can start with a 1x1 surface.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_devToolsOpen && ReferenceEquals(_devToolsOwner, browser) && ReferenceEquals(_devToolsPanel, panel))
                ShowDevToolsInCurrentDock(browser, inspectElementAtX, inspectElementAtY);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ShowDevToolsInCurrentDock(ChromiumWebBrowser browser, int inspectElementAtX, int inspectElementAtY)
    {
        var panel = _devToolsPanel;
        if (panel == null || panel.IsDisposed || !panel.IsHandleCreated) return;

        var width = Math.Max(1, panel.ClientSize.Width);
        var height = Math.Max(1, panel.ClientSize.Height);

        using var windowInfo = CefSharp.Core.ObjectFactory.CreateWindowInfo();
        windowInfo.RuntimeStyle = CefSharpSettings.RuntimeStyle ?? CefRuntimeStyle.Alloy;
        windowInfo.SetAsChild(panel.Handle, 0, 0, width, height);
        browser.GetBrowserHost().ShowDevTools(windowInfo, inspectElementAtX, inspectElementAtY);
    }

    private void CloseDevToolsDock()
    {
        if (!_devToolsOpen && _devToolsPanel == null) return;

        _devToolsOpen = false;
        var owner = _devToolsOwner;
        _devToolsOwner = null;
        DevToolsDock.Visibility = Visibility.Collapsed;
        DevToolsSplitter.Visibility = Visibility.Collapsed;
        DevToolsColumn.Width = new GridLength(0);
        DevToolsSplitterColumn.Width = new GridLength(0);

        try { owner?.CloseDevTools(); }
        catch (Exception ex) { AppLogger.Log("DevTools", ex, "Closing docked DevTools."); }

        // The host control must stay alive until CEF reports OnBeforeClose for the DevTools
        // popup browser. OnCefBrowserClosed performs the actual native-host cleanup.
        if (_devToolsBrowser == null)
            CleanupDevToolsHost();
    }

    private void DevToolsClose_Click(object sender, RoutedEventArgs e) => CloseDevToolsDock();

    private void OnCefBrowserCreated(IBrowser browser)
    {
        try
        {
            var url = browser.MainFrame?.Url ?? string.Empty;
            if (!url.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase)) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _devToolsBrowser = browser;
            }));
        }
        catch { }
    }

    private void OnCefBrowserClosed(IBrowser browser)
    {
        var isTracked = ReferenceEquals(_devToolsBrowser, browser);
        var url = string.Empty;
        try { url = browser.MainFrame?.Url ?? string.Empty; } catch { }
        if (!isTracked && !url.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase)) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(_devToolsBrowser, browser)) _devToolsBrowser = null;
            CleanupDevToolsHost();
        }));
    }

    private void CleanupDevToolsHost()
    {
        var panel = _devToolsPanel;
        _devToolsPanel = null;
        if (panel == null) return;
        try
        {
            if (ReferenceEquals(DevToolsHost.Child, panel)) DevToolsHost.Child = null;
            panel.Dispose();
        }
        catch { }
    }

    // ===== Site Info Popup =====
    private async void SecurityBtn_Click(object sender, RoutedEventArgs e)
    {
        await UpdateSiteInfoAsync();
        SiteInfoPopup.IsOpen = !SiteInfoPopup.IsOpen;
    }

    private async Task UpdateSiteInfoAsync()
    {
        if (_currentTab == null) return;
        var url = _currentTab.Address ?? string.Empty;
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

        PermissionsPanel.Children.Clear();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        var context = _vm.GetRequestContext();
        if (context == null) return;

        var permissionTypes = new[]
        {
            (LanguageManager.Instance["Perm_Camera"], ContentSettingTypes.MediaStreamCamera, AppSettings.Profile.SitePermissions.Camera),
            (LanguageManager.Instance["Perm_Microphone"], ContentSettingTypes.MediaStreamMic, AppSettings.Profile.SitePermissions.Microphone),
            (LanguageManager.Instance["Perm_Location"], ContentSettingTypes.Geolocation, AppSettings.Profile.SitePermissions.Geolocation),
            (LanguageManager.Instance["Perm_Notifications"], ContentSettingTypes.Notifications, AppSettings.Profile.SitePermissions.Notifications),
            (LanguageManager.Instance["Perm_Popups"], ContentSettingTypes.Popups, (ContentPermission?)null),
            (LanguageManager.Instance["Perm_JavaScript"], ContentSettingTypes.JavaScript, (ContentPermission?)null),
        };

        foreach (var (name, contentType, appDefault) in permissionTypes)
        {
            ContentSettingValues setting;
            try
            {
                setting = await CefProfileDataHelper.GetContentSettingAsync(context, url, url, contentType);
            }
            catch (Exception ex)
            {
                AppLogger.Log("SiteInfo", ex, $"Reading {contentType} for {uri.Host}.");
                setting = ContentSettingValues.Default;
            }

            var stateText = setting == ContentSettingValues.Default && appDefault.HasValue
                ? LocalizePermission(appDefault.Value)
                : LocalizeContentSetting(setting);
            AddPermissionRow(name, stateText);
        }
    }

    private void AddPermissionRow(string name, string state)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        item.Children.Add(new Path
        {
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            Stroke = (Brush)FindResource("Ink400Brush"),
            StrokeThickness = 1.5,
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
            Text = state,
            FontSize = 11,
            Foreground = (Brush)FindResource("Ink500Brush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        });
        PermissionsPanel.Children.Add(item);
    }

    private static string LocalizePermission(ContentPermission permission)
        => permission switch
        {
            ContentPermission.Allow => LanguageManager.Instance["Browser_Allowed"],
            ContentPermission.Block => LanguageManager.Instance["Browser_Blocked"],
            _ => LanguageManager.Instance["Browser_AskDefault"]
        };

    private static string LocalizeContentSetting(ContentSettingValues setting)
        => setting switch
        {
            ContentSettingValues.Allow => LanguageManager.Instance["Browser_Allowed"],
            ContentSettingValues.Block => LanguageManager.Instance["Browser_Blocked"],
            ContentSettingValues.Ask => LanguageManager.Instance["Browser_AskDefault"],
            _ => LanguageManager.Instance["Browser_Default"]
        };

    private void SiteInfo_Cookies_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        var url = _currentTab?.Address?.Trim() ?? string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var cookieUri) ||
            (cookieUri.Scheme != Uri.UriSchemeHttp && cookieUri.Scheme != Uri.UriSchemeHttps))
        {
            ZidimiMessageBox.Show(
                LanguageManager.Instance["Cookie_NoSite"],
                LanguageManager.Instance["Browser_ZidimiBrowser"],
                ZidimiMessageBoxButton.OK,
                ZidimiMessageBoxImage.Information,
                Window.GetWindow(this));
            return;
        }

        new CookieManagerWindow(url) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void SiteInfo_Cert_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        OpenDevTools();
    }

    private void SiteInfo_Settings_Click(object sender, RoutedEventArgs e)
    {
        SiteInfoPopup.IsOpen = false;
        new SiteExceptionsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
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
        // Keep only one extension surface visible at a time.
        CloseExtensionActionPopup();
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
        // Rebuilding the pinned-toolbar buttons invalidates any PlacementTarget currently used by
        // an extension action popup, so dismiss it before replacing those visual elements.
        CloseExtensionActionPopup();
        PopulatePinnedExtensionsToolbar();
        PopulateExtensions();
    }

    private void PopulatePinnedExtensionsToolbar()
    {
        PinnedExtensionsHost.Children.Clear();

        var pinned = ExtensionService.Instance.InstalledExtensions
            .Where(ext => ext.IsPinned && ext.IsEnabled && ExtensionService.Instance.IsExtensionAvailable(ext))
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
        if (sender is not FrameworkElement { Tag: ExtensionInfo ext }) return;

        var pin = !ext.IsPinned;
        if (pin && !ExtensionService.Instance.IsExtensionAvailable(ext))
        {
            ZidimiMessageBox.Show(LanguageManager.Instance["Ext_FilesMissing"], LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
            return;
        }

        ExtensionService.Instance.TogglePinned(ext, pin);
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

    private void PinnedExtensionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not ExtensionInfo ext) return;
        OpenExtensionAction(ext, fe);
    }

    private async void OpenExtensionAction(ExtensionInfo ext, FrameworkElement? placementTarget = null)
    {
        var anchor = placementTarget ?? ExtensionsBtn;
        var extensionId = !string.IsNullOrWhiteSpace(ext.RuntimeId) ? ext.RuntimeId : ext.Id;

        // Match Chrome/Edge toolbar behaviour: clicking the same extension icon again toggles
        // its action popup closed instead of destroying/recreating its Chromium surface.
        if (_extensionActionPopup is { IsOpen: true } existing &&
            string.Equals(existing.ExtensionId, extensionId, StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(existing.PlacementTarget, anchor))
        {
            CloseExtensionActionPopup();
            return;
        }

        if (!App.CefReady)
        {
            ZidimiMessageBox.Show(LanguageManager.Instance["Ext_BrowserNotReady"], ext.Name,
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
            return;
        }

        var action = ExtensionService.Instance.ResolveDefaultAction(ext);
        if (!action.success || string.IsNullOrWhiteSpace(action.popupUrl))
        {
            // Extensions are not required to declare default_popup. For action-only extensions,
            // invoke the extension's real default action against Zidimi's active web target so
            // action.onClicked behaves like a normal Chromium toolbar click. This path is generic
            // and does not inspect the extension name/id/type.
            if (ext.HasToolbarAction && string.IsNullOrWhiteSpace(ext.PopupPath) &&
                string.IsNullOrWhiteSpace(ext.SidePanelPath))
            {
                ExtensionsPopup.IsOpen = false;
                CloseExtensionActionPopup();
                var triggered = await ExtensionService.Instance
                    .TriggerToolbarActionAsync(ext, _currentBrowser);
                if (triggered.success) return;

                AppLogger.Log("ExtensionAction", $"{ext.Name}: {triggered.message}");
                ZidimiMessageBox.Show(
                    string.IsNullOrWhiteSpace(triggered.message)
                        ? LanguageManager.Instance["Ext_ActionUnavailable"]
                        : triggered.message,
                    ext.Name, ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
                return;
            }

            AppLogger.Log("ExtensionAction", $"{ext.Name}: {action.message}");
            ZidimiMessageBox.Show(
                string.IsNullOrWhiteSpace(action.message) ? LanguageManager.Instance["Ext_ActionUnavailable"] : action.message,
                ext.Name, ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
            return;
        }

        // The extension surface only needs the profile RequestContext; it does not depend on
        // whichever normal web tab happens to be active. This also makes toolbar actions work
        // while an internal Zidimi page (settings/history/extensions/...) is selected.
        var context = _vm.GetRequestContext();
        if (context == null)
        {
            ZidimiMessageBox.Show(LanguageManager.Instance["Ext_BrowserNotReady"], ext.Name,
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
            return;
        }

        // Keep extension actions inside browser chrome, anchored to the clicked icon (or the
        // puzzle button when launched from the list), and auto-size to the extension document.
        ExtensionsPopup.IsOpen = false;
        CloseExtensionActionPopup();

        // CEF exposes real browser ids to extension APIs, but Zidimi owns the visual WPF tab
        // strip. Give every extension surface the same profile-scoped snapshot so security,
        // ad-blocking, password-manager, developer and other extensions all resolve the same
        // active/current web page without any name/id-specific special cases.
        var tabSnapshot = ExtensionRuntimeCoordinator.Instance.GetSnapshot(context);

        var popup = new ExtensionActionPopup(ext, action.popupUrl, context, anchor, tabSnapshot);
        popup.Closed += ExtensionActionPopup_Closed;
        _extensionActionPopup = popup;
        popup.Show();
    }

    private void ExtensionActionPopup_Closed(object? sender, EventArgs e)
    {
        if (sender is ExtensionActionPopup popup)
            popup.Closed -= ExtensionActionPopup_Closed;

        if (ReferenceEquals(_extensionActionPopup, sender))
            _extensionActionPopup = null;
    }

    private void CloseExtensionActionPopup()
    {
        var popup = _extensionActionPopup;
        _extensionActionPopup = null;
        if (popup == null) return;

        popup.Closed -= ExtensionActionPopup_Closed;
        popup.Dispose();
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
                Text = !ExtensionService.Instance.IsExtensionAvailable(ext)
                    ? ResolveLang("Ext_StatusMissing", "Extension files are missing")
                    : ext.IsEnabled
                        ? (ext.IsPinned ? ResolveLang("Ext_Pinned", "Pinned on toolbar") : ResolveLang("Ext_NotPinned", "Not pinned"))
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
            item.PreviewMouseLeftButtonUp += ExtensionPopupItem_Click;
            ExtensionsList.Items.Add(item);
        }
    }

    private void ExtensionPopupItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { Tag: ExtensionInfo ext }) return;

        // The pin button owns its own click. Do not also open the extension popup when
        // the user only intended to pin/unpin it.
        if (FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) != null) return;

        ExtensionsPopup.IsOpen = false;
        OpenExtensionAction(ext);
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ExtensionsPopup_Manage(object sender, RoutedEventArgs e)
    {
        ExtensionsPopup.IsOpen = false;
        _vm.OpenAppTab(TabKind.Extensions);
    }

    /// <summary>
    /// Deterministic shutdown for all native Chromium tab resources owned by this view. WPF does
    /// not call IDisposable automatically for UserControl, so MainWindow/App invoke this explicitly
    /// before RequestContexts/Cef.Shutdown.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        Interlocked.Increment(ref _backgroundBrowserWarmupGeneration);
        _autocompleteTimer.Stop();
        CloseExtensionActionPopup();
        CloseDevToolsDock();

        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Tabs.CollectionChanged -= OnTabsChanged;
        App.CefReadyChanged -= OnCefReady;
        ExtensionService.Instance.ExtensionsChanged -= OnExtensionsChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;

        var win = Window.GetWindow(this);
        if (win != null) win.PreviewKeyDown -= OnPreviewKeyDown;

        foreach (var tab in _browsers.Keys.ToArray())
            ReleaseBrowserResources(tab, removeSlot: true);
        _browsers.Clear();

        foreach (var view in _appViews.Values.ToArray())
            RemoveSurface(view);
        _appViews.Clear();

        foreach (var cts in _faviconLoads.Values.ToArray())
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        _faviconLoads.Clear();

        _visibleSurface = null;
        _currentBrowser = null;
        _currentTab = null;
    }

}

