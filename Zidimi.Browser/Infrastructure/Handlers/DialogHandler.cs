using CefSharp;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;

namespace Zidimi.Browser.Infrastructure.Handlers;

public class DialogHandler : IDialogHandler
{
    public bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode,
        string title, string defaultFilePath,
        IReadOnlyCollection<string> acceptFilters,
        IReadOnlyCollection<string> separatorChars,
        IReadOnlyCollection<string> acceptExtensions,
        IFileDialogCallback callback)
    {
        // CEF file dialogs must be handled with Windows dialogs. CEF calls this callback from
        // a background thread, so we must switch to the UI thread before opening a dialog.
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            bool handled = false;
            var selected = new List<string>();

            switch (mode)
            {
                case CefFileDialogMode.Open:
                case CefFileDialogMode.OpenMultiple:
                {
                    var dlg = new OpenFileDialog
                    {
                        Title = string.IsNullOrEmpty(title) ? "Open" : title,
                        Multiselect = mode == CefFileDialogMode.OpenMultiple,
                        Filter = BuildFilter(acceptFilters, acceptExtensions)
                    };
                    if (dlg.ShowDialog(Application.Current.MainWindow) == true)
                    {
                        selected.AddRange(dlg.FileNames);
                        handled = true;
                    }
                    break;
                }
                case CefFileDialogMode.OpenFolder:
                {
                    var dlg = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = string.IsNullOrEmpty(title) ? "Select Folder" : title
                    };
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selected.Add(dlg.SelectedPath);
                        handled = true;
                    }
                    break;
                }
                case CefFileDialogMode.Save:
                {
                    var dlg = new SaveFileDialog
                    {
                        Title = string.IsNullOrEmpty(title) ? "Save" : title,
                        FileName = defaultFilePath,
                        Filter = BuildFilter(acceptFilters, acceptExtensions)
                    };
                    if (dlg.ShowDialog(Application.Current.MainWindow) == true)
                    {
                        selected.Add(dlg.FileName);
                        handled = true;
                    }
                    break;
                }
            }

            if (!handled || selected.Count == 0)
            {
                if (!callback.IsDisposed)
                    callback.Cancel();
            }
            else
            {
                if (!callback.IsDisposed)
                    callback.Continue(selected);
            }
        });

        // Return true -> we handle the dialog asynchronously.
        return true;
    }

    private static string BuildFilter(IReadOnlyCollection<string> acceptFilters,
        IReadOnlyCollection<string> acceptExtensions)
    {
        // CEF provides acceptFilters or acceptExtensions. If neither is available -> "*.*".
        if (acceptFilters != null && acceptFilters.Count > 0)
        {
            return string.Join("|", acceptFilters);
        }

        if (acceptExtensions != null && acceptExtensions.Count > 0)
        {
            var exts = new List<string>();
            foreach (var e in acceptExtensions)
            {
                var ext = e.TrimStart('.').ToLowerInvariant();
                exts.Add(string.IsNullOrEmpty(ext) ? "*" : ext);
            }
            return "Files (*" + string.Join(", *", System.Linq.Enumerable.Select(exts, x => x == "*" ? "*" : "." + x)) + ")|*" +
                   string.Join(";", System.Linq.Enumerable.Select(exts, x => x == "*" ? "*" : "*." + x)) +
                   "|All Files (*.*)|*.*";
        }

        return "All Files (*.*)|*.*";
    }
}