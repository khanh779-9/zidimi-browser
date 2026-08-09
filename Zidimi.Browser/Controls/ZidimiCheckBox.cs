using System.Windows;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Custom CheckBox themed for Zidimi: a rounded square box with a purple check mark.
/// Keeps the full semantics of the raw CheckBox (IsChecked, Checked/Unchecked/Indeterminate).
/// </summary>
public class ZidimiCheckBox : System.Windows.Controls.CheckBox
{
    static ZidimiCheckBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiCheckBox),
            new FrameworkPropertyMetadata(typeof(ZidimiCheckBox)));
    }
}
