using System.Collections.Concurrent;
using CefSharp;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Tracks downloads — stores entries in a collection so the Downloads panel UI can display them
/// (spec 11.2 — IDownloadHandler). Tests only log; full UI comes in phase 2B.
/// </summary>
public sealed class DownloadHandler : IDownloadHandler
{
    private readonly ConcurrentDictionary<int, string> _downloadGuids = new();

    public event Action<DownloadEntry>? DownloadStarted;
    public event Action<DownloadEntry>? DownloadUpdated;

    /// <summary>Raised when the user initiates a Chrome Web Store extension download (a .crx from Google's update endpoint).</summary>
    public event Action<string>? CrxInstallRequested;

    public bool CanDownload(IWebBrowser browserControl, IBrowser browser, string url, string requestMethod)
    {
        // Chrome Web Store installation is not safe to hand to the native Alloy/WPF
        // extension flow. Intercept it before CEF creates a download item and let
        // Zidimi's own CRX installer handle the package instead.
        if (IsWebStoreCrx(url))
        {
            AppLogger.Log("WebStoreDownload", $"Blocked native CRX download in CanDownload. Url={url}");
            CrxInstallRequested?.Invoke(url);
            return false;
        }

        return true;
    }

    public bool OnBeforeDownload(IWebBrowser browserControl, IBrowser browser, DownloadItem downloadItem,
        IBeforeDownloadCallback callback)
    {
        var entry = new DownloadEntry
        {
            Url = downloadItem.Url ?? "",
            SuggestedFileName = downloadItem.SuggestedFileName ?? "",
            FullPath = downloadItem.FullPath ?? "",
            IsCancelled = downloadItem.IsCancelled,
            IsComplete = downloadItem.IsComplete,
            TotalBytes = downloadItem.TotalBytes,
            ReceivedBytes = downloadItem.ReceivedBytes,
        };

        if (callback.IsDisposed) return false;

        var webStoreCrxUrl = GetWebStoreCrxUrl(downloadItem);
        if (webStoreCrxUrl != null)
        {
            AppLogger.Log("WebStoreDownload", $"Blocked native CRX download in OnBeforeDownload. Url={webStoreCrxUrl}");
            CrxInstallRequested?.Invoke(webStoreCrxUrl);
            callback.Dispose();
            return true;
        }

        bool askBeforeSave = true;
        string defaultDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var ctx = browserControl?.GetBrowserHost()?.RequestContext;
        if (ctx != null)
        {
            if (ctx.GetPreferenceSafe("download.prompt_for_download") is bool ask) askBeforeSave = ask;
            if (ctx.GetPreferenceSafe("download.default_directory") is string dir && !string.IsNullOrEmpty(dir))
                defaultDir = dir;
        }

        if (askBeforeSave)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                callback.Dispose();
                return true;
            }

            // OnBeforeDownload runs on CEF's browser-process UI thread. Keep that thread
            // free and complete the async download callback after the WPF dialog closes.
            dispatcher.BeginInvoke(() =>
            {
                using (callback)
                {
                    if (callback.IsDisposed) return;

                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = entry.SuggestedFileName,
                        Title = LanguageManager.Instance["Download_ChooseLocation"],
                        InitialDirectory = defaultDir,
                    };

                    if (dlg.ShowDialog() != true) return;

                    entry.FullPath = dlg.FileName;
                    _downloadGuids[downloadItem.Id] = entry.Guid;
                    DownloadStarted?.Invoke(entry);
                    callback.Continue(entry.FullPath, showDialog: false);
                }
            });

            return true;
        }

        try
        {
            System.IO.Directory.CreateDirectory(defaultDir);
            entry.FullPath = System.IO.Path.Combine(defaultDir, entry.SuggestedFileName);
        }
        catch
        {
            entry.FullPath = entry.SuggestedFileName;
        }

        _downloadGuids[downloadItem.Id] = entry.Guid;
        DownloadStarted?.Invoke(entry);

        using (callback)
        {
            callback.Continue(entry.FullPath, showDialog: false);
        }
        return true;
    }

    public static bool IsWebStoreCrx(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Host.Equals("clients2.google.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Equals("/service/update2/crx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Google may redirect Web Store packages to a CDN before CEF sees the
        // download. Restrict the fallback to Google's known CRX delivery paths.
        return (uri.Host.EndsWith(".gvt1.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase))
            && uri.AbsolutePath.Contains("chromewebstore", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.EndsWith(".crx", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetWebStoreCrxUrl(DownloadItem downloadItem)
    {
        if (IsWebStoreCrx(downloadItem.OriginalUrl ?? string.Empty)) return downloadItem.OriginalUrl;
        if (IsWebStoreCrx(downloadItem.Url ?? string.Empty)) return downloadItem.Url;
        return null;
    }

    public void OnDownloadUpdated(IWebBrowser browserControl, IBrowser browser, DownloadItem downloadItem,
        IDownloadItemCallback callback)
    {
        var entry = new DownloadEntry
        {
            Url = downloadItem.Url ?? "",
            SuggestedFileName = downloadItem.SuggestedFileName ?? "",
            FullPath = downloadItem.FullPath ?? "",
            IsCancelled = downloadItem.IsCancelled,
            IsComplete = downloadItem.IsComplete,
            TotalBytes = downloadItem.TotalBytes,
            ReceivedBytes = downloadItem.ReceivedBytes,
        };

        if (_downloadGuids.TryGetValue(downloadItem.Id, out var guid))
        {
            entry.Guid = guid;
        }

        DownloadUpdated?.Invoke(entry);

        if (downloadItem.IsCancelled || downloadItem.IsComplete)
        {
            _downloadGuids.TryRemove(downloadItem.Id, out _);
        }
    }
}

