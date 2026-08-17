using System.Diagnostics;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;

namespace Zidimi.Browser;

public partial class App : Application
{
    public static MainViewModel ViewModel { get; private set; } = null!;
    public static RequestContextFactory RequestContexts { get; private set; } = null!;

    public static bool CefReady { get; private set; }
    public static event Action? CefReadyChanged;

    /// <summary>
    /// True only when the real browser window already exists. ViewModel is intentionally created
    /// before CEF/profile selection, so ViewModel != null must never be used as a proxy for this.
    /// </summary>
    public bool HasLiveBrowserWindow
        => _browserInitialized && _shellWindow is { IsLoaded: true, HasBrowserHost: true };

    private static readonly System.Threading.Mutex SingleInstanceMutex =
        new(false, @"Local\Zidimi.Browser.SingleInstance");

    private readonly Stopwatch _startupClock = Stopwatch.StartNew();
    private bool _browserServicesReady;
    private bool _browserInitialized;
    private bool _cefBootstrapStarted;
    private Zidimi.Browser.MainWindow? _shellWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Init();
        AppSettings.InitializeDefaults();
        _ = LanguageManager.Instance;
        ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            ZidimiMessageBox.Show(LanguageManager.Instance["App_AlreadyRunning"],
                "Zidimi Browser", ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        base.OnStartup(e);

        EnsureBrowserServices();
        ShowStartupShell(LanguageManager.Instance["Startup_Preparing"]);
        StartCefBootstrap();
    }

    private void EnsureBrowserServices()
    {
        if (_browserServicesReady) return;
        _browserServicesReady = true;

        var history = new HistoryService();
        var bookmarks = new BookmarkService();
        var downloads = new DownloadService();
        RequestContexts = new RequestContextFactory();
        ViewModel = new MainViewModel(history, bookmarks, downloads);
        AppLogger.Log("Startup", $"Browser services ready at {_startupClock.ElapsedMilliseconds} ms.");
    }

    private void StartCefBootstrap()
    {
        if (_cefBootstrapStarted) return;
        _cefBootstrapStarted = true;
        Dispatcher.BeginInvoke(InitializeCefAfterStart,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Called after profile selection (or by defensive legacy callers). CEF is initialized first so
    /// every settings decision is based on CEF GetPreference rather than a JSON snapshot.
    /// </summary>
    public void InitializeBrowser()
    {
        EnsureBrowserServices();
        if (!CefReady)
        {
            ShowStartupShell(LanguageManager.Instance["Startup_Chromium"]);
            StartCefBootstrap();
            return;
        }

        if (_browserInitialized)
        {
            if (MainWindow is Zidimi.Browser.MainWindow { IsLoaded: true } window)
            {
                if (!window.IsVisible) window.Show();
                if (window.Opacity > 0.99) window.Activate();
            }
            return;
        }

        _browserInitialized = true;
        ShowStartupShell(LanguageManager.Instance["Startup_Browser"], AppSettings.Profile.DisplayName);
        _ = CreateAndRevealMainWindowObservedAsync();
    }

    private async Task CreateAndRevealMainWindowObservedAsync()
    {
        try
        {
            await ViewModel.InitializeProfileDataAsync();
            await CreateAndRevealMainWindowAsync();
        }
        catch (Exception ex)
        {
            _browserInitialized = false;
            AppLogger.Log("Startup", ex, "Creating main browser window.");
            _shellWindow?.SetStartupStatus(LanguageManager.Instance["Startup_Failed"], ex.Message);
            ZidimiMessageBox.Show(ex.Message, "Zidimi Browser", ZidimiMessageBoxButton.OK,
                ZidimiMessageBoxImage.Error, _shellWindow);
        }
    }

    private Zidimi.Browser.MainWindow EnsureStartupShell()
    {
        if (_shellWindow != null)
            return _shellWindow;

        _shellWindow = new Zidimi.Browser.MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        return _shellWindow;
    }

    private void ShowStartupShell(string status, string? detail = null)
    {
        var shell = EnsureStartupShell();
        shell.SetStartupStatus(status, detail);
        MainWindow = shell;

        if (!shell.IsVisible)
            shell.Show();
        shell.Activate();
    }

    private async void InitializeCefAfterStart()
    {
        try
        {
            _shellWindow?.SetStartupStatus(LanguageManager.Instance["Startup_Extensions"]);
            _shellWindow?.SetStartupStatus(LanguageManager.Instance["Startup_Chromium"]);

            var processHandler = InitializeCef();
            var contextSignal = await Task.WhenAny(processHandler.ContextReady, Task.Delay(TimeSpan.FromSeconds(10)));
            if (!ReferenceEquals(contextSignal, processHandler.ContextReady))
                throw new TimeoutException("CEF global RequestContext initialization timed out.");

            CefReady = true;
            await AppSettings.LoadFromCefAsync();

            // Apply the values that CEF just read. This path deliberately does not write them back.
            LanguageManager.Instance.ApplyFromSettings(AppSettings.Global.DisplayLanguage);
            ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

            AppLogger.Log("Startup",
                $"CEF settings loaded at {_startupClock.ElapsedMilliseconds} ms. " +
                $"Profile={AppSettings.Global.CurrentProfile}; picker={AppSettings.Global.ShowProfilePickerOnStartup}; " +
                $"CefSharp={Cef.CefSharpVersion}; Chromium={Cef.ChromiumVersion}.");

            CefReadyChanged?.Invoke();

            if (AppSettings.Global.ShowProfilePickerOnStartup)
            {
                // The integrated Zidimi shell contains no BrowserView/HwndHost yet. Show the picker
                // first, then hide the shell so WPF always has a live top-level window.
                var picker = new Zidimi.Browser.Views.ProfileSelectorWindow();
                picker.Closed += (_, _) =>
                {
                    // Closing the startup picker without choosing a profile must not leave a
                    // hidden bootstrap shell/process running forever.
                    if (!_browserInitialized && _shellWindow is { IsVisible: false })
                        Shutdown();
                };
                MainWindow = picker;
                picker.Show();
                _shellWindow?.Hide();
                picker.Activate();
                AppLogger.Log("Startup", $"Profile selector shown at {_startupClock.ElapsedMilliseconds} ms.");
                return;
            }

            InitializeBrowser();
        }
        catch (Exception ex)
        {
            AppLogger.Log("CefInit", ex);
            _shellWindow?.SetStartupStatus(LanguageManager.Instance["Startup_Failed"], ex.Message);
            ZidimiMessageBox.Show(ex.Message, "Zidimi Browser", ZidimiMessageBoxButton.OK,
                ZidimiMessageBoxImage.Error, _shellWindow);
            Shutdown(-1);
        }
    }

    private async Task CreateAndRevealMainWindowAsync()
    {
        var mainWindow = EnsureStartupShell();
        MainWindow = mainWindow;

        if (!mainWindow.IsVisible)
            mainWindow.Show();

        mainWindow.SetStartupStatus(
            LanguageManager.Instance["Startup_Browser"],
            AppSettings.Profile.DisplayName);
        mainWindow.Activate();

        // Give the integrated startup card one final paint turn, then remove it BEFORE creating
        // BrowserView/HwndHost. This avoids WPF airspace leaks while keeping the whole bootstrap
        // inside one stable Zidimi top-level window.
        mainWindow.SetStartupReady(LanguageManager.Instance["Startup_Ready"]);
        await Task.Delay(90);
        mainWindow.AttachBrowserHost();
        mainWindow.Activate();

        AppLogger.Log("Startup", $"Integrated main-window startup completed at {_startupClock.ElapsedMilliseconds} ms.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Hand the final in-memory settings snapshot to CEF, then drain the serialized preference
        // queue while RequestContexts and the CEF UI thread are still alive. This closes the old
        // shutdown race without ever patching Local State/Preferences behind Chromium's back.
        try
        {
            if (Cef.IsInitialized == true && CefReady)
            {
                AppSettings.SaveAll();
                var drained = AppSettings.DrainPendingCefWrites(TimeSpan.FromSeconds(4));
                AppLogger.Log("Lifecycle", $"CEF preference queue drained={drained} before shutdown.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Lifecycle", ex, "Saving/draining native settings before Chromium shutdown.");
        }

        // Dispose native tab HWND/browser instances first, then profile RequestContexts, and only
        // then shut down CEF. This mirrors Chromium ownership and prevents a closed tab/profile from
        // keeping renderer, LevelDB or SQLite handles alive after the window is gone.
        try { _shellWindow?.DisposeBrowserHost(); }
        catch (Exception ex) { AppLogger.Log("Lifecycle", ex, "Disposing browser host."); }

        try { ViewModel?.Dispose(); }
        catch (Exception ex) { AppLogger.Log("Lifecycle", ex, "Disposing app data services."); }

        try { ChromiumTopLevelTargetRouter.Instance.Dispose(); } catch { }
        RequestContexts?.Dispose();

        if (Cef.IsInitialized == true)
        {
            try
            {
                Cef.Shutdown();
            }
            catch (Exception ex)
            {
                AppLogger.Log("Lifecycle", ex, "CEF shutdown failed.");
            }
        }

        // Cef.Shutdown is the single persistence boundary. No post-shutdown JSON patching is
        // performed, so a stale shell snapshot cannot overwrite newer Chromium/extension state.

        AppLogger.Log("Lifecycle", $"Exiting with {ViewModel?.Tabs.Count ?? 0} tab(s).");
        AppLogger.MarkCleanExit();

        base.OnExit(e);
    }

    private static CefBootstrapBrowserProcessHandler InitializeCef()
    {
        var settings = CefConfigurator.BuildSettings();
        var dependencyCheckSetting = Environment.GetEnvironmentVariable("ZIDIMI_CEF_DEPENDENCY_CHECK");
        var performDependencyCheck = string.Equals(dependencyCheckSetting, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dependencyCheckSetting, "true", StringComparison.OrdinalIgnoreCase);

        var handler = new CefBootstrapBrowserProcessHandler();
        var ok = Cef.Initialize(settings, performDependencyCheck, handler);
        if (!ok)
            throw new InvalidOperationException(
                "Cef.Initialize trả về false — kiểm tra log CEF và các subprocess CefSharp.BrowserSubprocess còn sót.");
        return handler;
    }

    private void OnUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Log("Dispatcher", e.Exception, "Unhandled UI-thread exception.");
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            AppLogger.Log("AppDomain", ex,
                $"Unhandled exception on {(e.IsTerminating ? "terminating" : "non-terminating")} AppDomain event.");
    }
}

