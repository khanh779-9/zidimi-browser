using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Views;

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
            // đợi layout xong rồi cuộn về tab mới
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
        var menu = new HecoContextMenu();

        var pin = new HecoMenuItem
        {
            Content = tab.IsPinned ? LanguageManager.Instance["Tab_UnpinTab"] : LanguageManager.Instance["Tab_PinTab"],
            IconData = tab.IsPinned ? IconPaths.Close : IconPaths.Star,
        };
        pin.Click += (_, _) => _vm.TogglePinTab(tab);
        menu.Items.Add(pin);

        var mute = new HecoMenuItem
        {
            Content = tab.IsMuted ? LanguageManager.Instance["Tab_UnmuteTab"] : LanguageManager.Instance["Tab_MuteTab"],
            IsEnabled = tab.IsAudioPlaying || tab.IsMuted,
        };
        mute.Click += (_, _) => ToggleMute(tab);
        menu.Items.Add(mute);

        var reload = new HecoMenuItem { Content = LanguageManager.Instance["Tab_ReloadTab"], IconData = IconPaths.Reload };
        reload.Click += (_, _) => _vm.ReloadTab(tab);
        menu.Items.Add(reload);

        var dup = new HecoMenuItem { Content = LanguageManager.Instance["Tab_DuplicateTab"] };
        dup.Click += (_, _) => _vm.DuplicateTab(tab);
        menu.Items.Add(dup);

        var close = new HecoMenuItem { Content = LanguageManager.Instance["Tab_CloseTab"], IsDanger = true, IconData = IconPaths.Close };
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
        var menu = new HecoContextMenu();

        foreach (var tab in _vm.Tabs)
        {
            var item = new HecoMenuItem
            {
                Content = string.IsNullOrWhiteSpace(tab.Title) ? tab.Address : tab.Title,
                IconData = tab.Favicon == null ? IconPaths.Home : null,
                FontWeight = ReferenceEquals(tab, _vm.ActiveTab) ? FontWeights.SemiBold : FontWeights.Normal,
            };
            item.Click += (_, _) => _vm.ActiveTab = tab;
            menu.Items.Add(item);
        }

        var newTab = new HecoMenuItem { Content = LanguageManager.Instance["Tab_NewTab"], IconData = IconPaths.Plus };
        newTab.Click += (_, _) => _vm.NewTabCommand.Execute(null);
        menu.Items.Add(newTab);

        menu.PlacementTarget = sender as Button ?? AllTabsBtn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
