using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Views;

public partial class DownloadsView : UserControl
{
    private readonly MainViewModel _vm;

    public DownloadsView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;
    }

    private void Entry_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Mở file khi click (chỉ khi đã tải xong)
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