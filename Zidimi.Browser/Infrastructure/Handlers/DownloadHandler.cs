using System.IO;
using System.Collections.Concurrent;
using CefSharp;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Tracks downloads and reports stable GUID-based updates to the Downloads service/UI.
/// </summary>
public sealed class DownloadHandler : IDownloadHandler
{
    private readonly ConcurrentDictionary<int, string> _downloadGuids = new();

    public event Action<DownloadEntry>? DownloadStarted;
    public event Action<DownloadEntry>? DownloadUpdated;

    public bool CanDownload(IWebBrowser browserControl, IBrowser browser, string url, string requestMethod)
    {
        // Do not intercept Chrome Web Store traffic. In Chrome Runtime mode Chromium owns extension
        // installation; ordinary downloads continue through this handler.
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

        bool askBeforeSave = true;
        string defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var ctx = browserControl?.GetBrowserHost()?.RequestContext;
        if (ctx != null)
        {
            if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DownloadPromptForDownload) is bool ask) askBeforeSave = ask;
            if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DownloadDefaultDirectory) is string dir && !string.IsNullOrEmpty(dir))
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
            Directory.CreateDirectory(defaultDir);
            entry.FullPath = Path.Combine(defaultDir, entry.SuggestedFileName);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Downloads", ex, $"Preparing download directory '{defaultDir}'.");
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

