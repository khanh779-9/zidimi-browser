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

    protected override void OnStartup(StartupEventArgs e)
    {
// Config and theme must be loaded first so that if HecoMessageBox is shown (due to an error or the mutex), it isn't transparent.
        // Migrate before Load so that app data still holding the Chromium file names is
        // moved to heco_* before AppSettings.Load reads it (and before CEF opens the profile).
        UserDataPaths.MigrateLegacyData();
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

        // Let WPF create and show MainWindow first (now light because the browser is created lazily),
        // then initialize CEF at low priority so the window doesn't stay "frozen white" for long.
        base.OnStartup(e);

        bool showPicker = true;
        try
        {
            if (File.Exists(UserDataPaths.LocalStatePath))
            {
                var json = File.ReadAllText(UserDataPaths.LocalStatePath);
                if (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonNode>(json) is System.Text.Json.Nodes.JsonObject root &&
                    root.TryGetPropertyValue("profile", out var profileNode) &&
                    profileNode is System.Text.Json.Nodes.JsonObject profileObj)
                {
                    if (profileObj.TryGetPropertyValue("show_picker_on_startup", out var showPickerNode) && showPickerNode != null)
                    {
                        showPicker = showPickerNode.GetValue<bool>();
                    }
                }
            }
        }
        catch { }

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
        ViewModel?.SaveSession();
        RequestContexts?.Dispose();
        TrayIcon?.Dispose();
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

// 3rd-party cookie policy (spec 8.3): CefSharp 150 has no RequestContextSettings.AcceptThirdPartyCookies,
        // so use the Chromium command-line switch below, applied to the whole context.
        if (AppSettings.Profile.BlockThirdPartyCookies)
        {
            settings.CefCommandLineArgs["block-3rd-party-cookies"] = "1";
            // Turn off the "allow" 3rd-party cookies enum to force a hard block.
            settings.CefCommandLineArgs["disable-3rd-party-cookies"] = "1";
        }

        // Do Not Track: Chromium command-line switch — applies to every request.
        if (AppSettings.Profile.SendDoNotTrack)
        {
            settings.CefCommandLineArgs["enable-do-not-track"] = "1";
        }
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

