using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

public partial class TabStrip : UserControl
{
    private readonly MainViewModel _vm;
    private ScrollViewer? _scroller;

    public TabStrip()
    {
        InitializeComponent();
        _scroller = TabScroller;
        _vm = App.ViewModel;
        DataContext = _vm;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.Tabs.CollectionChanged += Tabs_CollectionChanged;
        UpdateScrollButtons();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.Tabs.CollectionChanged -= Tabs_CollectionChanged;
    }

    private void Tabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // wait for the layout to finish, then scroll to the new tab
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _scroller?.ScrollToRightEnd();
                UpdateScrollButtons();
            }));
        }
    }

    private void TabScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroller is null)
            return;
        _scroller.ScrollToHorizontalOffset(_scroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void TabScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateScrollButtons();
    }

    private void UpdateScrollButtons()
    {
        if (_scroller is null || ScrollLeftBtn is null || ScrollRightBtn is null)
            return;
        ScrollLeftBtn.Visibility = _scroller.HorizontalOffset > 0 ? Visibility.Visible : Visibility.Collapsed;
        ScrollRightBtn.Visibility =
            _scroller.HorizontalOffset + _scroller.ViewportWidth < _scroller.ExtentWidth
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        _scroller?.ScrollToHorizontalOffset(_scroller.HorizontalOffset - 240);
    }

    private void ScrollRight_Click(object sender, RoutedEventArgs e)
    {
        _scroller?.ScrollToHorizontalOffset(_scroller.HorizontalOffset + 240);
    }

    private TabViewModel? _dragTab;
    private Point _dragStart;

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabViewModel tab)
        {
            _vm.ActiveTab = tab;
            _dragTab = tab;
            _dragStart = e.GetPosition(this);
            if (e.ClickCount == 2)
            {
                _vm.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        }
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab == null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) < 8 && Math.Abs(pos.Y - _dragStart.Y) < 8) return;
        if (sender is DependencyObject dep)
        {
            DragDrop.DoDragDrop(dep, new DataObject(typeof(TabViewModel), _dragTab), DragDropEffects.Move);
        }
    }

    private void Tab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragTab = null;
    }

    private void Tab_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void Tab_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TabViewModel))) return;
        if (e.Data.GetData(typeof(TabViewModel)) is not TabViewModel dragged) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not TabViewModel target) return;
        if (ReferenceEquals(dragged, target)) return;

        _vm.MoveTab(dragged, target);
        _dragTab = null;
        e.Handled = true;
    }

    private void Tab_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabViewModel tab)
        {
            _vm.ActiveTab = tab;
            ShowTabContextMenu(fe, tab);
            e.Handled = true;
        }
    }

    private void ShowTabContextMenu(FrameworkElement anchor, TabViewModel tab)
    {
        var menu = new ZidimiContextMenu();

        var pin = new ZidimiMenuItem
        {
            Content = tab.IsPinned ? LanguageManager.Instance["Tab_UnpinTab"] : LanguageManager.Instance["Tab_PinTab"],
            IconData = tab.IsPinned ? IconPaths.Close : IconPaths.Star,
        };
        pin.Click += (_, _) => _vm.TogglePinTab(tab);
        menu.Items.Add(pin);

        var mute = new ZidimiMenuItem
        {
            Content = tab.IsMuted ? LanguageManager.Instance["Tab_UnmuteTab"] : LanguageManager.Instance["Tab_MuteTab"],
            IsEnabled = tab.IsAudioPlaying || tab.IsMuted,
        };
        mute.Click += (_, _) => ToggleMute(tab);
        menu.Items.Add(mute);

        var reload = new ZidimiMenuItem { Content = LanguageManager.Instance["Tab_ReloadTab"], IconData = IconPaths.Reload };
        reload.Click += (_, _) => _vm.ReloadTab(tab);
        menu.Items.Add(reload);

        var dup = new ZidimiMenuItem { Content = LanguageManager.Instance["Tab_DuplicateTab"] };
        dup.Click += (_, _) => _vm.DuplicateTab(tab);
        menu.Items.Add(dup);

        var close = new ZidimiMenuItem { Content = LanguageManager.Instance["Tab_CloseTab"], IsDanger = true, IconData = IconPaths.Close };
        close.Click += (_, _) => _vm.CloseTabCommand.Execute(tab);
        menu.Items.Add(close);

        menu.PlacementTarget = anchor;
        menu.HorizontalOffset = 0;
        menu.VerticalOffset = 0;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Audio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabViewModel tab)
            ToggleMute(tab);
    }

    private void ToggleMute(TabViewModel tab)
    {
        if (App.ViewModel.GetBrowser(tab) is var browser && browser != null)
        {
            var host = browser.GetBrowserHost();
            if (host != null)
            {
                var muted = !host.IsAudioMuted;
                host.SetAudioMuted(muted);
                tab.IsMuted = muted;
            }
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabViewModel tab)
            _vm.CloseTabCommand.Execute(tab);
    }

    private void AllTabs_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ZidimiContextMenu();

        foreach (var tab in _vm.Tabs)
        {
            var item = new ZidimiMenuItem
            {
                Content = string.IsNullOrWhiteSpace(tab.Title) ? tab.Address : tab.Title,
                IconData = tab.Favicon == null ? IconPaths.Home : null,
                FontWeight = ReferenceEquals(tab, _vm.ActiveTab) ? FontWeights.SemiBold : FontWeights.Normal,
            };
            item.Click += (_, _) => _vm.ActiveTab = tab;
            menu.Items.Add(item);
        }

        var newTab = new ZidimiMenuItem { Content = LanguageManager.Instance["Tab_NewTab"], IconData = IconPaths.Plus };
        newTab.Click += (_, _) => _vm.NewTabCommand.Execute(null);
        menu.Items.Add(newTab);

        menu.PlacementTarget = sender as Button ?? AllTabsBtn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    // ===== Tab search =====

    public void OpenTabSearch()
    {
        RefreshTabSearch();
        TabSearchPopup.PlacementTarget = TabSearchBtn;
        TabSearchPopup.IsOpen = true;
        TabSearchBox.Text = string.Empty;
        TabSearchBox.Focus();
        Keyboard.Focus(TabSearchBox);
    }

    private void TabSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        OpenTabSearch();
    }

    private void TabSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshTabSearch();
    }

    private void RefreshTabSearch()
    {
        if (TabSearchList == null) return;
        var q = (TabSearchBox?.Text ?? "").Trim().ToLower();
        var matches = _vm.Tabs
            .Where(t => string.IsNullOrEmpty(q)
                || (t.Title?.ToLowerInvariant().Contains(q) ?? false)
                || (t.Address?.ToLowerInvariant().Contains(q) ?? false))
            .ToList();

        TabSearchList.Items.Clear();
        foreach (var tab in matches)
        {
            var item = new System.Windows.Controls.ListBoxItem
            {
                Content = string.IsNullOrWhiteSpace(tab.Title) ? tab.Address : tab.Title,
                Tag = tab,
            };
            TabSearchList.Items.Add(item);
        }
    }

    private void TabSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            TabSearchPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (TabSearchList.SelectedItem is System.Windows.Controls.ListBoxItem sel && sel.Tag is TabViewModel selected)
            {
                _vm.ActiveTab = selected;
                TabSearchPopup.IsOpen = false;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down && TabSearchList.Items.Count > 0)
        {
            TabSearchList.SelectedIndex = Math.Min(TabSearchList.Items.Count - 1, TabSearchList.SelectedIndex + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            TabSearchList.SelectedIndex = Math.Max(0, TabSearchList.SelectedIndex - 1);
            e.Handled = true;
        }
    }

    private void TabSearchList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender == null) return;
        var sel = TabSearchList.SelectedItem as System.Windows.Controls.ListBoxItem;
        if (sel?.Tag is TabViewModel tab)
            _vm.ActiveTab = tab;
    }
}
