using System.Windows;
using System.Windows.Controls;
using Heco.Browser.Infrastructure;

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

    public HecoComboBox()
    {
        // Default placeholder chỉ lấy ở runtime (sau khi LanguageManager đã nạp),
        // tránh gọi LanguageManager trong static metadata (nạp XAML trước OnStartup).
        if (GetValue(PlaceholderProperty) == null)
            SetValue(PlaceholderProperty, LanguageManager.Instance["Combo_Placeholder"]);
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(HecoComboBox),
        new PropertyMetadata(null));

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty) as string;
        set => SetValue(PlaceholderProperty, value);
    }
}
