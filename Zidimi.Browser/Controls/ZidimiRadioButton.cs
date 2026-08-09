using System.Windows;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Custom RadioButton themed for Zidimi: a purple indicator circle plus a label.
/// Keeps the full semantics of the raw RadioButton (GroupName, IsChecked, Checked/Unchecked).
/// </summary>
public class ZidimiRadioButton : System.Windows.Controls.RadioButton
{
    static ZidimiRadioButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiRadioButton),
            new FrameworkPropertyMetadata(typeof(ZidimiRadioButton)));
    }
}
