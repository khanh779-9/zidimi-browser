using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ListBoxItem theo theme Heco: nền trong suốt, hover nổi, selected có
/// nền tím nhạt + vạch accent trái. Dùng thay <c>ListBoxItem</c> gốc.
/// </summary>
public class HecoListBoxItem : ListBoxItem
{
    static HecoListBoxItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoListBoxItem),
            new FrameworkPropertyMetadata(typeof(HecoListBoxItem)));
    }
}

/// <summary>
/// Custom ListBox theo theme Heco: nền trong suốt, không viền, dùng với
/// <see cref="HecoListBoxItem"/>. Dùng cho dropdown gợi ý, danh sách.
/// </summary>
public class HecoListBox : ListBox
{
    static HecoListBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoListBox),
            new FrameworkPropertyMetadata(typeof(HecoListBox)));
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is HecoListBoxItem;

    protected override DependencyObject GetContainerForItemOverride()
        => new HecoListBoxItem();
}
