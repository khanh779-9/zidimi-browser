using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>Color variants for HecoButton.</summary>
public enum HecoButtonVariant
{
    Secondary,
    Primary,
    Ghost,
    Danger,
}

/// <summary>
/// Custom Button themed for Heco. Supports:
///   - Variant: Primary (purple gradient), Secondary (surface), Ghost (transparent), Danger (red).
///   - IconData: an SVG path data string shown to the left of the label.
///   - Customizable corner radius.
/// Use in place of the base <c>Button</c> to keep the theme consistent.
/// </summary>
public class HecoButton : Button
{
    static HecoButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoButton),
            new FrameworkPropertyMetadata(typeof(HecoButton)));
    }

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(HecoButtonVariant), typeof(HecoButton),
        new PropertyMetadata(HecoButtonVariant.Secondary));

    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(string), typeof(HecoButton),
        new PropertyMetadata(null));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(HecoButton),
        new PropertyMetadata(new CornerRadius(6)));

    public HecoButtonVariant Variant
    {
        get => (HecoButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>The icon's SVG path data (see <see cref="IconPaths"/>).</summary>
    public string? IconData
    {
        get => (string?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
