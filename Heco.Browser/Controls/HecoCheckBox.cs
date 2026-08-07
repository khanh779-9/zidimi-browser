using System.Windows;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom CheckBox themed for Heco: a rounded square box with a purple check mark.
/// Keeps the full semantics of the raw CheckBox (IsChecked, Checked/Unchecked/Indeterminate).
/// </summary>
public class HecoCheckBox : System.Windows.Controls.CheckBox
{
    static HecoCheckBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoCheckBox),
            new FrameworkPropertyMetadata(typeof(HecoCheckBox)));
    }
}
