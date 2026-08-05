using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>Các biến thể màu của HecoButton.</summary>
public enum HecoButtonVariant
{
    Secondary,
    Primary,
    Ghost,
    Danger,
}

/// <summary>
/// Custom Button theo theme Heco. Hỗ trợ:
///   - Variant: Primary (gradient tím), Secondary (surface), Ghost (trong suốt), Danger (đỏ).
///   - IconData: chuỗi path data SVG hiển thị bên trái nhãn.
///   - CornerRadius tuỳ chỉnh.
/// Dùng thay <c>Button</c> gốc để giữ đồng bộ theme.
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

    /// <summary>Path data SVG của icon (xem <see cref="IconPaths"/>).</summary>
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
