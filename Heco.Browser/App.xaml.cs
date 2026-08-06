using System.IO;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;

using Heco.Browser.Models;

namespace Heco.Browser;

public partial class App : Application
{
    public static MainViewModel ViewModel { get; private set; } = null!;
    public static RequestContextFactory RequestContexts { get; private set; } = null!;
    public static TrayIconManager? TrayIcon { get; private set; }

    private static readonly System.Threading.Mutex SingleInstanceMutex =
        new(false, @"Local\Heco.Browser.SingleInstance");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Phải nạp cấu hình và theme trước để nếu gọi HecoMessageBox (do lỗi hay mutex) thì không bị trong suốt
        AppSettings.Load();
        UserDataPaths.MigrateLegacyData();
        foreach (var p in AppSettings.Global.Profiles)
            UserDataPaths.RegisterProfile(p);
        ThemeManager.EnsureLoaded();
        ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            // Đã có instance Heco.Browser đang chạy — CEF không cho 2 instance dùng chung cache.
            HecoMessageBox.Show(LanguageManager.Instance["App_AlreadyRunning"],
                "Heco Browser", HecoMessageBoxButton.OK, HecoMessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        try
        {
            var dummy = LanguageManager.Instance; // Initialize LanguageManager
            InitializeCef();
        }
        catch (Exception ex)
        {
            File.AppendAllText("heco-browser-crash.log",
                $"[{DateTime.Now:O}] [CefInit] {ex}\n\n");
            throw;
        }

        base.OnStartup(e);

        var history = new HistoryService();
        var bookmarks = new BookmarkService();
        RequestContexts = new RequestContextFactory();
        ViewModel = new MainViewModel(history, bookmarks);

        TrayIcon = new TrayIconManager();
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
        // CefSharp phải được khởi tạo trước khi dùng bất kỳ control ChromiumWebBrowser nào.
        var cachePath = UserDataPaths.SharedCacheDir;
        Directory.CreateDirectory(cachePath);

        var settings = new CefSettings
        {
            CachePath = cachePath,
            LogSeverity = LogSeverity.Error,
        };
        // Bỏ qua GPU blacklist để chạy ổn định trên nhiều GPU khác nhau.
        // CefSharp 150 tự thêm một số switch mặc định nên phải dùng indexer để tránh trùng key.
        if (!AppSettings.Global.EnableGpu)
        {
            settings.CefCommandLineArgs["disable-gpu"] = "1";
            settings.CefCommandLineArgs["disable-gpu-compositing"] = "1";
        }
        
        if (AppSettings.Global.EnhanceVideos)
        {
            // Bật cờ tối ưu hóa giải mã video của Chromium
            settings.CefCommandLineArgs["enable-features"] = "HardwareSecureDecryption,Vulkan";
        }

        if (!AppSettings.Global.UseSystemProxy)
        {
            settings.CefCommandLineArgs["no-proxy-server"] = "1";
        }

        // Chính sách Cookie 3rd-party (spec 8.3): CefSharp 150 không có RequestContextSettings.AcceptThirdPartyCookies
        // nên dùng command-line switch của Chromium dưới đây áp dụng cho toàn bộ context.
        if (AppSettings.Profile.BlockThirdPartyCookies)
        {
            settings.CefCommandLineArgs["block-3rd-party-cookies"] = "1";
            // Tắt enums "allow" 3rd-party cookies để ép chặn cứng.
            settings.CefCommandLineArgs["disable-3rd-party-cookies"] = "1";
        }

        // Do Not Track: Chromium command-line switch - áp dụng cho mọi request.
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

