using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

public partial class DownloadsView : UserControl
{
    private readonly MainViewModel _vm;
    private ICollectionView? _view;

    public DownloadsView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;

        _view = CollectionViewSource.GetDefaultView(_vm.Downloads);
        if (_view != null)
        {
            _view.Filter = FilterDownload;
            _vm.Downloads.CollectionChanged += (s, e) =>
            {
                UpdateEmptyState(_vm.Downloads.Count);
                _view?.Refresh();
            };
        }
        SearchBox.TextChanged += (s, e) => _view?.Refresh();
        UpdateEmptyState(_vm.Downloads.Count);
    }

    private bool FilterDownload(object o)
    {
        if (o is not DownloadEntry d) return false;
        var q = SearchBox?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) return true;
        return (d.SuggestedFileName?.Contains(q, System.StringComparison.OrdinalIgnoreCase) == true)
            || (d.Url?.Contains(q, System.StringComparison.OrdinalIgnoreCase) == true);
    }

    private void UpdateEmptyState(int count)
    {
        if (EmptyState == null || ListScroll == null) return;
        var empty = count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Entry_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DownloadEntry d)
        {
            if (d.IsComplete && !string.IsNullOrEmpty(d.FullPath) && System.IO.File.Exists(d.FullPath))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(d.FullPath) { UseShellExecute = true }); }
                catch { }
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is DownloadEntry d)
        {
            if (!string.IsNullOrEmpty(d.FullPath) && System.IO.File.Exists(d.FullPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{d.FullPath}\"");
            }
        }
    }
}