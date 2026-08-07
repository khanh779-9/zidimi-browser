using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Infrastructure.Handlers;

using Heco.Browser.Models;

namespace Heco.Browser;

public partial class App : Application
{
    public static MainViewModel ViewModel { get; private set; } = null!;
    public static RequestContextFactory RequestContexts { get; private set; } = null!;
    public static TrayIconManager? TrayIcon { get; private set; }

    /// <summary>true once CEF has finished initializing — no ChromiumWebBrowser may be created before that.</summary>
    public static bool CefReady { get; private set; }
    public static event Action? CefReadyChanged;

    private static readonly System.Threading.Mutex SingleInstanceMutex =
        new(false, @"Local\Heco.Browser.SingleInstance");

    public static bool ShowPickerOnStartupPreference { get; set; } = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppSettings.Load();
        UserDataPaths.RegisterProfiles(AppSettings.Global.Profiles);
        ThemeManager.EnsureLoaded();
        ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            // A Heco.Browser instance is already running — CEF doesn't allow two instances to share cache.
            HecoMessageBox.Show(LanguageManager.Instance["App_AlreadyRunning"],
                "Heco Browser", HecoMessageBoxButton.OK, HecoMessageBoxImage.Information);
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
            var picker = new Heco.Browser.Views.ProfileSelectorWindow();
            picker.Show();
        }
        else
        {
            InitializeBrowser();
        }
    }

    public void InitializeBrowser()
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

        Dispatcher.BeginInvoke(InitializeCefAfterStart,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static void InitializeCefAfterStart()
    {
        try
        {
            InitializeCef();
            CefReady = true;

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
                ctx?.SetPreference("profile.show_picker_on_startup", showPicker, out _);
            }
            catch { }

            CefReadyChanged?.Invoke();
        }
        catch (Exception ex)
        {
            File.AppendAllText("heco-browser-crash.log",
                $"[{DateTime.Now:O}] [CefInit] {ex}\n\n");
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

        base.OnExit(e);
    }

    private static void InitializeCef()
    {
        // CefSharp must be initialized before using any ChromiumWebBrowser control.
        var cachePath = UserDataPaths.SharedCacheDir;
        Directory.CreateDirectory(cachePath);

var settings = new CefSettings
        {
            CachePath = cachePath,
            LogSeverity = LogSeverity.Error,
        };
        // Custom scheme "heco://" (spec 11.2 — ISchemeHandlerFactory): lightweight internal pages.
        settings.RegisterScheme(new CefCustomScheme
        {
            SchemeName = "heco",
            IsStandard = true,
            IsSecure = true,
            IsCorsEnabled = true,
            IsLocal = true,
            SchemeHandlerFactory = new HecoSchemeHandlerFactory(),
        });
// Bypass the GPU blacklist so it runs stably across many GPUs.
        // CefSharp 150 adds some default switches itself, so use the indexer to avoid duplicate keys.
        if (!AppSettings.Global.EnableGpu)
        {
            settings.CefCommandLineArgs["disable-gpu"] = "1";
            settings.CefCommandLineArgs["disable-gpu-compositing"] = "1";
        }
        
        if (AppSettings.Global.EnhanceVideos)
        {
            // Enable Chromium's optimized video decoding flag
            settings.CefCommandLineArgs["enable-features"] = "HardwareSecureDecryption,Vulkan";
        }

        if (!AppSettings.Global.UseSystemProxy)
        {
            settings.CefCommandLineArgs["no-proxy-server"] = "1";
        }

        // Profile-specific settings like cookies and Do-Not-Track are now managed dynamically 
        // via CefSharp RequestContext preferences in PreferencesView.xaml.cs.

        var ok = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        if (!ok)
            throw new InvalidOperationException(
                "Cef.Initialize trả về false — kiểm tra log CEF (debug.log) và các subprocess CefSharp.BrowserSubprocess còn sót.");
    }

    private void OnUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        File.AppendAllText("heco-browser-crash.log",
            $"[{DateTime.Now:O}] [Dispatcher] {e.Exception}\n\n");
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            File.AppendAllText("heco-browser-crash.log",
                $"[{DateTime.Now:O}] [AppDomain] {ex}\n\n");
    }
}

