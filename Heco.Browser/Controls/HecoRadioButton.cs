using System.Windows;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom RadioButton themed for Heco: a purple indicator circle plus a label.
/// Keeps the full semantics of the raw RadioButton (GroupName, IsChecked, Checked/Unchecked).
/// </summary>
public class HecoRadioButton : System.Windows.Controls.RadioButton
{
    static HecoRadioButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoRadioButton),
            new FrameworkPropertyMetadata(typeof(HecoRadioButton)));
    }
}
