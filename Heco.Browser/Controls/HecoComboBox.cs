using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ComboBox (select box) theo theme Heco.
/// Kế thừa trực tiếp từ ComboBox gốc để tận dụng các hành vi có sẵn (auto close popup, keyboard nav, v.v.).
/// </summary>
public class HecoComboBox : ComboBox
{
    static HecoComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoComboBox),
            new FrameworkPropertyMetadata(typeof(HecoComboBox)));
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(HecoComboBox),
        new PropertyMetadata("Chọn..."));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }
}
