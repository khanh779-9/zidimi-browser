using System.Windows;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom CheckBox theo theme Heco: hộp vuông bo góc + dấu tích tím.
/// Giữ đầy đủ ngữ nghĩa CheckBox gốc (IsChecked, Checked/Unchecked/Indeterminate).
/// </summary>
public class HecoCheckBox : System.Windows.Controls.CheckBox
{
    static HecoCheckBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoCheckBox),
            new FrameworkPropertyMetadata(typeof(HecoCheckBox)));
    }
}
