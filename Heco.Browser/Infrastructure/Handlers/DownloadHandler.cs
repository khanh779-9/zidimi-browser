using System.Collections.Concurrent;
using CefSharp;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Tracks downloads — stores entries in a collection so the Downloads panel UI can display them
/// (spec 11.2 — IDownloadHandler). Tests only log; full UI comes in phase 2B.
/// </summary>
public sealed class DownloadHandler : IDownloadHandler
{
    public event Action<DownloadEntry>? DownloadStarted;
    public event Action<DownloadEntry>? DownloadUpdated;

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
        DownloadStarted?.Invoke(entry);

        if (callback.IsDisposed) return true;

        string finalPath = entry.FullPath;

        // If AppSettings asks where to save, show a SaveFileDialog (on the UI thread).
        if (AppSettings.Profile.AskBeforeSave)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = entry.SuggestedFileName,
                    Title = LanguageManager.Instance["Download_ChooseLocation"],
                    InitialDirectory = AppSettings.Profile.DownloadPath,
                };
                var ok = dlg.ShowDialog() == true;
                if (ok)
                {
                    finalPath = dlg.FileName;
                }
                else
                {
                    // Cancel
                    using (callback) callback.Continue("", showDialog: false);
                    return;
                }
            });
        }
        else
        {
            // Save straight into DownloadPath using the suggested file name.
            try
            {
                System.IO.Directory.CreateDirectory(AppSettings.Profile.DownloadPath);
                finalPath = System.IO.Path.Combine(AppSettings.Profile.DownloadPath, entry.SuggestedFileName);
            }
            catch { }
        }

        using (callback)
        {
            callback.Continue(finalPath, showDialog: false);
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
        DownloadUpdated?.Invoke(entry);
    }
}

