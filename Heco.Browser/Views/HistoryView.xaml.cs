using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;
using ICollectionView = System.ComponentModel.ICollectionView;

namespace Heco.Browser.Views;

public partial class HistoryView : UserControl
{
    private readonly MainViewModel _vm;
    private ICollectionView? _view;

    public HistoryView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;

        _view = CollectionViewSource.GetDefaultView(_vm.History);
        if (_view != null)
        {
            _view.Filter = FilterHistory;
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SearchFilter)) _view.Refresh();
            };
        }
    }

    private bool FilterHistory(object o)
    {
        if (o is not HistoryEntry h) return false;
        if (string.IsNullOrEmpty(_vm.SearchFilter)) return true;
        var q = _vm.SearchFilter;
        return (h.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) == true)
            || (h.Url?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void Entry_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is HistoryEntry h)
            OpenUrl(h.Url);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is HistoryEntry h) OpenUrl(h.Url);
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        _vm.NewTab(url);
    }
}
