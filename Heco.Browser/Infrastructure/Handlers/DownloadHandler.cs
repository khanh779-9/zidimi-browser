using System.Collections.Concurrent;
using CefSharp;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Tracking download — lưu entry vào collection để UI Downloads panel hiển thị
/// (spec 11.2 — IDownloadHandler). Test chỉ log; UI đầy đủ ở phase 2B.
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

        // Nếu AppSettings yêu cầu hỏi nơi lưu → hiển thị SaveFileDialog (trên UI thread).
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
            // Tự lưu vào DownloadPath với tên file đề nghị.
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

