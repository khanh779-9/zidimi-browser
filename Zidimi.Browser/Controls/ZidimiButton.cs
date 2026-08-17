using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zidimi.Browser.Controls;

/// <summary>Color variants for ZidimiButton.</summary>
public enum ZidimiButtonVariant
{
    Secondary,
    Primary,
    Ghost,
    Danger,
}

/// <summary>
/// Custom Button themed for Zidimi. Supports:
///   - Variant: Primary (theme accent gradient), Secondary (surface), Ghost (transparent), Danger (red).
///   - IconData: vector geometry shown to the left of the label.
///   - Customizable corner radius.
/// Use in place of the base <c>Button</c> to keep the theme consistent.
/// </summary>
public class ZidimiButton : Button
{
    static ZidimiButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiButton),
            new FrameworkPropertyMetadata(typeof(ZidimiButton)));
    }

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(ZidimiButtonVariant), typeof(ZidimiButton),
        new PropertyMetadata(ZidimiButtonVariant.Secondary));

    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(Geometry), typeof(ZidimiButton),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(ZidimiButton),
        new PropertyMetadata(new CornerRadius(6)));

    public ZidimiButtonVariant Variant
    {
        get => (ZidimiButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>The icon geometry (see <see cref="IconPaths"/>).</summary>
    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
