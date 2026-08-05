using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Views;

public partial class BookmarksView : UserControl
{
    private readonly MainViewModel _vm;

    public BookmarksView()
    {
        InitializeComponent();
        _vm = App.ViewModel;
        DataContext = _vm;

        SearchBox.TextChanged += OnSearchChanged;
        UpdateEmptyState(_vm.Bookmarks.Count);
        ((INotifyCollectionChanged)_vm.Bookmarks).CollectionChanged += OnBookmarksChanged;
    }

    private void OnBookmarksChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => UpdateEmptyState(_vm.Bookmarks.Count);

    private void UpdateEmptyState(int count)
    {
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Grid.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(_vm.Bookmarks);
        if (view == null) return;
        view.Filter = o =>
        {
            if (o is not Bookmark b) return false;
            if (string.IsNullOrEmpty(SearchBox.Text)) return true;
            var q = SearchBox.Text;
            return (b.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) == true)
                || (b.Url?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
        };
    }

    private void Bookmark_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Bookmark b)
        {
            _vm.NewTab(b.Url);
        }
    }
}
