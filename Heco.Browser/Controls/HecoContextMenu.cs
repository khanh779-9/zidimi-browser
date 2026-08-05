using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Heco.Browser.Controls;

/// <summary>
/// Một dòng trong menu theo theme Heco: icon (tuỳ chọn) + nhãn, hover nền, tuỳ chọn màu nguy hiểm.
/// Tự đóng menu cha (HecoContextMenu) khi được bấm.
/// Dùng thay <c>MenuItem</c>/nút dòng của ContextMenu gốc.
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

    /// <summary>Path data SVG của icon bên trái nhãn (xem <see cref="IconPaths"/>).</summary>
    public string? IconData
    {
        get => (string?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    /// <summary>True: nhãn + icon màu đỏ (hành động nguy hiểm như đóng tab / xoá).</summary>
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
/// Menu card theo theme Heco (thay <c>ContextMenu</c> gốc):
///   - Popup card bo góc, có shadow, đóng khi bấm ra ngoài hoặc chọn item.
///   - Items là tập <see cref="HecoMenuItem"/> (hoặc object bất kỳ với ItemTemplate).
///   - Placement mặc định Bottom; hỗ trợ PlacementTarget + HorizontalOffset/VerticalOffset.
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

    /// <summary>False: đóng khi bấm ra ngoài (mặc định).</summary>
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

    /// <summary>Các mục của menu (HecoMenuItem).</summary>
    public System.Collections.ObjectModel.ObservableCollection<object> Items { get; } = new();

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (HecoContextMenu)d;
        if (menu._popup != null) menu._popup.IsOpen = (bool)e.NewValue;
    }

    /// <summary>Mở menu tại vị trí chuột (dùng cho context menu mở trên bất kỳ element nào).</summary>
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
