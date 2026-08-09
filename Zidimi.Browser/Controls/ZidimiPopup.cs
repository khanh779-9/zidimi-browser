using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Popup card themed for Zidimi (in place of the raw <c>Popup</c>): a rounded card with a shadow
/// plus an optional backdrop overlay.
///   - Placement / PlacementTarget / HorizontalOffset / VerticalOffset / StaysOpen work like Popup.
///   - HasBackdrop = true covers the whole app with a dark overlay (for small modals).
///   - The Content is arbitrary.
/// </summary>
public class ZidimiPopup : ContentControl
{
    static ZidimiPopup()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiPopup),
            new FrameworkPropertyMetadata(typeof(ZidimiPopup)));
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(ZidimiPopup),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(PlacementMode), typeof(ZidimiPopup),
        new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget), typeof(UIElement), typeof(ZidimiPopup),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
        nameof(HorizontalOffset), typeof(double), typeof(ZidimiPopup), new PropertyMetadata(0.0));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset), typeof(double), typeof(ZidimiPopup), new PropertyMetadata(0.0));

    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        nameof(StaysOpen), typeof(bool), typeof(ZidimiPopup), new PropertyMetadata(true));

    public static readonly DependencyProperty HasBackdropProperty = DependencyProperty.Register(
        nameof(HasBackdrop), typeof(bool), typeof(ZidimiPopup), new PropertyMetadata(false));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(ZidimiPopup), new PropertyMetadata(new CornerRadius(12)));

    private Popup? _popup;

    public ZidimiPopup()
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

    /// <summary>When true, covers the whole app with a dark overlay when opened (modal backdrop).</summary>
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

    /// <summary>Command used to open/close the popup (e.g. clicking the backdrop). By default it closes the popup.</summary>
    public System.Windows.Input.ICommand? ToggleOpenCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(ToggleOpenCommandProperty);
        set => SetValue(ToggleOpenCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleOpenCommandProperty = DependencyProperty.Register(
        nameof(ToggleOpenCommand), typeof(System.Windows.Input.ICommand), typeof(ZidimiPopup),
        new PropertyMetadata(null));

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (ZidimiPopup)d;
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
