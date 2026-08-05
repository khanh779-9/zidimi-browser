using System.Windows;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom RadioButton theo theme Heco: vòng tròn indicator tím + nhãn.
/// Giữ đầy đủ ngữ nghĩa RadioButton gốc (GroupName, IsChecked, Checked/Unchecked).
/// </summary>
public class HecoRadioButton : System.Windows.Controls.RadioButton
{
    static HecoRadioButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoRadioButton),
            new FrameworkPropertyMetadata(typeof(HecoRadioButton)));
    }
}
