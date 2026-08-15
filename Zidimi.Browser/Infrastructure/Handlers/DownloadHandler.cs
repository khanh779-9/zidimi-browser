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
    public event Action<DownloadEntry>? DownloadStarted;
    public event Action<DownloadEntry>? DownloadUpdated;

    /// <summary>Raised when the user initiates a Chrome Web Store extension download (a .crx from Google's update endpoint).</summary>
    public event Action<string>? CrxInstallRequested;

    public bool CanDownload(IWebBrowser browserControl, IBrowser browser, string url, string requestMethod)
    {
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

        if (callback.IsDisposed) return true;

        // "Add to Chrome" on the Web Store downloads a .crx from Google's update endpoint.
        // Intercept it so the browser installs the extension (v1.5.0) instead of saving a file.
        if (IsWebStoreCrx(entry.Url))
        {
            CrxInstallRequested?.Invoke(entry.Url);
            using (callback) callback.Continue("", showDialog: false);
            return true;
        }

        DownloadStarted?.Invoke(entry);

        string finalPath = entry.FullPath;

        bool askBeforeSave = true;
        string defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var ctx = browserControl?.GetBrowserHost()?.RequestContext;
        if (ctx != null)
        {
            if (ctx.GetPreferenceSafe("download.prompt_for_download") is bool ask) askBeforeSave = ask;
            if (ctx.GetPreferenceSafe("download.default_directory") is string dir && !string.IsNullOrEmpty(dir)) defaultDir = dir;
        }

        // Ask the user where to save on the UI thread, then continue the download on this thread.
        // Continue() must be called exactly once — calling it inside the dialog lambda AND again
        // below would double-execute the callback and crash the native download manager.
        bool? accepted = false;
        if (askBeforeSave)
        {
            try
            {
                accepted = System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = entry.SuggestedFileName,
                        Title = LanguageManager.Instance["Download_ChooseLocation"],
                        InitialDirectory = defaultDir,
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        finalPath = dlg.FileName;
                        return true;
                    }
                    return false;
                });
            }
            catch
            {
                accepted = false;
            }

            if (accepted != true)
            {
                // User cancelled (or no UI was available) — cancel the download with a single Continue().
                if (!callback.IsDisposed)
                {
                    using (callback) callback.Continue("", showDialog: false);
                }
                return true;
            }
        }
        else
        {
            // Save straight into the default download folder using the suggested file name.
            try
            {
                System.IO.Directory.CreateDirectory(defaultDir);
                finalPath = System.IO.Path.Combine(defaultDir, entry.SuggestedFileName);
            }
            catch { }
        }

        using (callback)
        {
            callback.Continue(finalPath, showDialog: false);
        }
        return true;
    }

    public static bool IsWebStoreCrx(string url)
    {
        return !string.IsNullOrEmpty(url)
            && url.Contains("clients2.google.com/service/update2/crx", StringComparison.OrdinalIgnoreCase);
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
        DownloadUpdated?.Invoke(entry);
    }
}

