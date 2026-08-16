using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser;

public partial class App : Application
{
    public static MainViewModel ViewModel { get; private set; } = null!;
    public static RequestContextFactory RequestContexts { get; private set; } = null!;
    public static TrayIconManager? TrayIcon { get; private set; }

    /// <summary>true once CEF has finished initializing — no ChromiumWebBrowser may be created before that.</summary>
    public static bool CefReady { get; private set; }
    public static event Action? CefReadyChanged;

    private static readonly System.Threading.Mutex SingleInstanceMutex =
        new(false, @"Local\Zidimi.Browser.SingleInstance");

    public static bool ShowPickerOnStartupPreference { get; set; } = true;

    private bool _browserInitialized;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLogger.Init();
        AppSettings.Load();
        UserDataPaths.RegisterProfiles(AppSettings.Global.Profiles);
        ThemeManager.EnsureLoaded();
        ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            // A Zidimi.Browser instance is already running — CEF doesn't allow two instances to share cache.
            ZidimiMessageBox.Show(LanguageManager.Instance["App_AlreadyRunning"],
                "Zidimi Browser", ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        var dummy = LanguageManager.Instance; // Initialize LanguageManager

        base.OnStartup(e);

        bool showPicker = true;
        try
        {
            if (File.Exists(UserDataPaths.LocalStatePath))
            {
                var json = File.ReadAllText(UserDataPaths.LocalStatePath);
                if (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json) is System.Text.Json.Nodes.JsonObject root &&
                    root.TryGetPropertyValue("profile", out var profileNode) &&
                    profileNode is System.Text.Json.Nodes.JsonObject profileObj &&
                    profileObj.TryGetPropertyValue("show_picker_on_startup", out var showPickerNode) && showPickerNode != null)
                {
                    showPicker = showPickerNode.GetValue<bool>();
                }
            }
        }
        catch { }

        ShowPickerOnStartupPreference = showPicker;

        if (showPicker)
        {
            var picker = new Zidimi.Browser.Views.ProfileSelectorWindow();
            picker.Show();
        }
        else
        {
            InitializeBrowser();
        }
    }

    public void InitializeBrowser()
    {
        if (_browserInitialized)
        {
            if (MainWindow is { IsLoaded: true } window)
            {
                if (!window.IsVisible) window.Show();
                window.Activate();
            }
            return;
        }

        _browserInitialized = true;
        try
        {
            var history = new HistoryService();
            var bookmarks = new BookmarkService();
            var downloads = new DownloadService();
            RequestContexts = new RequestContextFactory();
            ViewModel = new MainViewModel(history, bookmarks, downloads);

            TrayIcon = new TrayIconManager();

            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            AppLogger.Log("Lifecycle", "Main window shown.");

            Dispatcher.BeginInvoke(InitializeCefAfterStart,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        catch
        {
            _browserInitialized = false;
            throw;
        }
    }

    private static void InitializeCefAfterStart()
    {
        try
        {
            InitializeCef();
            CefReady = true;
            AppLogger.Log("Lifecycle",
                $"CEF initialized. CefSharp={Cef.CefSharpVersion}, Chromium={Cef.ChromiumVersion}.");

            try
            {
                bool showPicker = true;
                if (File.Exists(UserDataPaths.LocalStatePath))
                {
                    var json = File.ReadAllText(UserDataPaths.LocalStatePath);
                    if (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json) is System.Text.Json.Nodes.JsonObject root &&
                        root.TryGetPropertyValue("profile", out var profileNode) &&
                        profileNode is System.Text.Json.Nodes.JsonObject profileObj &&
                        profileObj.TryGetPropertyValue("show_picker_on_startup", out var showPickerNode) && showPickerNode != null)
                    {
                        showPicker = showPickerNode.GetValue<bool>();
                    }
                }
                var ctx = RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
                ctx?.SetPreferenceSafe("profile.show_picker_on_startup", showPicker);
            }
            catch { }

            CefReadyChanged?.Invoke();
        }
        catch (Exception ex)
        {
            AppLogger.Log("CefInit", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RequestContexts?.Dispose();
        TrayIcon?.Dispose();

        if (Cef.IsInitialized == true)
        {
            try
            {
                Cef.Shutdown();
            }
            catch { }
        }

        // Save all session tabs, app settings, and Local State metadata strictly AFTER CEF shuts down,
        // so CEF's file locks and exit flushes can never overwrite or corrupt any data.
        try
        {
            ViewModel?.SaveSession();
            AppSettings.SaveAll();
            UserDataPaths.SaveLocalStateOnExit();
        }
        catch { }

        try
        {
            AppLogger.Log("Lifecycle", $"Exiting with {ViewModel?.Tabs.Count ?? 0} tab(s).");
        }
        catch { }
        AppLogger.MarkCleanExit();

        base.OnExit(e);
    }

    private static void InitializeCef()
    {
        // CefSharp must be initialized before using any ChromiumWebBrowser control.
        // All CEF tuning (GPU, proxy, stability switches, locale, DevTools, ...) is
        // centralized in CefConfigurator and driven by GlobalSettings.
        var settings = CefConfigurator.BuildSettings();

        // Profile-specific settings like cookies and Do-Not-Track are managed dynamically
        // via CefSharp RequestContext preferences in PreferencesView.xaml.cs.

        var ok = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        if (!ok)
            throw new InvalidOperationException(
                "Cef.Initialize trả về false — kiểm tra log CEF (debug.log) và các subprocess CefSharp.BrowserSubprocess còn sót.");
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

