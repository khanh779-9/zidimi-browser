using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

/// <summary>
/// Popup card theo theme Heco (thay <c>Popup</c> gốc): card bo góc có shadow + tuỳ chọn backdrop overlay.
///   - Placement / PlacementTarget / HorizontalOffset / VerticalOffset / StaysOpen như Popup.
///   - HasBackdrop = true: phủ nền tối lên toàn app (dùng cho modal nhỏ).
///   - Content tuỳ ý.
/// </summary>
public class HecoPopup : ContentControl
{
    static HecoPopup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoPopup),
            new FrameworkPropertyMetadata(typeof(HecoPopup)));
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(HecoPopup),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(PlacementMode), typeof(HecoPopup),
        new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget), typeof(UIElement), typeof(HecoPopup),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset), typeof(double), typeof(HecoPopup), new PropertyMetadata(0.0));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset), typeof(double), typeof(HecoPopup), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen), typeof(bool), typeof(HecoPopup), new PropertyMetadata(true));

    public static readonly DependencyProperty HasBackdropProperty = DependencyProperty.Register(
        nameof(HasBackdrop), typeof(bool), typeof(HecoPopup), new PropertyMetadata(false));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(HecoPopup), new PropertyMetadata(new CornerRadius(12)));

    private Popup? _popup;

    public HecoPopup()
    {
        ToggleOpenCommand = new RelayCommand(_ => IsOpen = false);
    }

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

    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    /// <summary>True: phủ nền tối lên toàn app khi mở (modal backdrop).</summary>
    public bool HasBackdrop
    {
        get => (bool)GetValue(HasBackdropProperty);
        set => SetValue(HasBackdropProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Command đóng/mở popup (bấm backdrop). Mặc định tự đóng popup.</summary>
    public System.Windows.Input.ICommand? ToggleOpenCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ToggleOpenCommandProperty);
        set => SetValue(ToggleOpenCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleOpenCommandProperty = DependencyProperty.Register(
        nameof(ToggleOpenCommand), typeof(System.Windows.Input.ICommand), typeof(HecoPopup),
        new PropertyMetadata(null));

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (HecoPopup)d;
        if (popup._popup != null) popup._popup.IsOpen = (bool)e.NewValue;
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
