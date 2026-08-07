using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Heco.Browser.Controls;

/// <summary>
/// A single row in the menu themed for Heco: an optional icon plus a label, hover background,
/// and an optional danger style. Closes the parent menu (HecoContextMenu) when clicked.
/// Use it in place of the raw <c>MenuItem</c> or row button in an ordinary ContextMenu.
/// </summary>
public class HecoMenuItem : Button
{
    static HecoMenuItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoMenuItem),
            new FrameworkPropertyMetadata(typeof(HecoMenuItem)));
    }

    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(string), typeof(HecoMenuItem), new PropertyMetadata(null));

    public static readonly DependencyProperty IsDangerProperty = DependencyProperty.Register(
        nameof(IsDanger), typeof(bool), typeof(HecoMenuItem), new PropertyMetadata(false));

    /// <summary>The SVG path data of the icon to the left of the label (see <see cref="IconPaths"/>).</summary>
    public string? IconData
    {
        get => (string?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    /// <summary>When true, the label and icon are shown in red (for dangerous actions like closing a tab or deleting).</summary>
    public bool IsDanger
    {
        get => (bool)GetValue(IsDangerProperty);
        set => SetValue(IsDangerProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        CloseParentMenu();
    }

    private void CloseParentMenu()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is HecoContextMenu menu)
            {
                menu.IsOpen = false;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }
}

/// <summary>
/// Menu card themed for Heco (in place of the raw <c>ContextMenu</c>):
///   - A rounded popup card with a shadow that closes when you click outside or pick an item.
///   - Items are a set of <see cref="HecoMenuItem"/> (or any object with an ItemTemplate).
///   - Placement defaults to Bottom; PlacementTarget plus HorizontalOffset/VerticalOffset are supported.
/// </summary>
public class HecoContextMenu : Control
{
    static HecoContextMenu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoContextMenu),
            new FrameworkPropertyMetadata(typeof(HecoContextMenu)));
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(HecoContextMenu),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(PlacementMode), typeof(HecoContextMenu),
        new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget), typeof(UIElement), typeof(HecoContextMenu),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset), typeof(double), typeof(HecoContextMenu), new PropertyMetadata(0.0));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset), typeof(double), typeof(HecoContextMenu), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen), typeof(bool), typeof(HecoContextMenu), new PropertyMetadata(false));

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(HecoContextMenu), new PropertyMetadata(null));

    private Popup? _popup;

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public PlacementMode Placement
    {
        get => (PlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public UIElement? PlacementTarget
    {
        get => (UIElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>When false (the default), closes when you click outside the menu.</summary>
    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The items in the menu (HecoMenuItem).</summary>
    public System.Collections.ObjectModel.ObservableCollection<object> Items { get; } = new();

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (HecoContextMenu)d;
        if (menu._popup != null) menu._popup.IsOpen = (bool)e.NewValue;
    }

    /// <summary>Opens the menu at the mouse position (used for context menus opened on any element).</summary>
    public void ShowAt(UIElement target, System.Windows.Point point)
    {
        PlacementTarget = target;
        HorizontalOffset = point.X;
        VerticalOffset = point.Y + 2;
        Placement = PlacementMode.Relative;
        IsOpen = true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            _popup.Opened += (_, _) => IsOpen = true;
            _popup.Closed += (_, _) => IsOpen = false;
            _popup.IsOpen = IsOpen;
        }
    }
}
