using System.Windows;
using System.Windows.Controls;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ComboBox (select box) themed for Heco.
/// Inherits directly from the raw ComboBox to reuse its built-in behaviors (auto-closing popup, keyboard navigation, etc.).
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
        // The default placeholder is only resolved at runtime (after LanguageManager has loaded),
        // to avoid calling LanguageManager from static metadata (XAML is loaded before OnStartup).
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
