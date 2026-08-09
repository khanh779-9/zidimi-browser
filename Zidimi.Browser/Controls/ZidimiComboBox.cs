using System.Windows;
using System.Windows.Controls;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Custom ComboBox (select box) themed for Zidimi.
/// Inherits directly from the raw ComboBox to reuse its built-in behaviors (auto-closing popup, keyboard navigation, etc.).
/// </summary>
public class ZidimiComboBox : ComboBox
{
    static ZidimiComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiComboBox),
            new FrameworkPropertyMetadata(typeof(ZidimiComboBox)));
    }

    public ZidimiComboBox()
    {
        // The default placeholder is only resolved at runtime (after LanguageManager has loaded),
        // to avoid calling LanguageManager from static metadata (XAML is loaded before OnStartup).
        if (GetValue(PlaceholderProperty) == null)
            SetValue(PlaceholderProperty, LanguageManager.Instance["Combo_Placeholder"]);
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(ZidimiComboBox),
        new PropertyMetadata(null));

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty) as string;
        set => SetValue(PlaceholderProperty, value);
    }
}
