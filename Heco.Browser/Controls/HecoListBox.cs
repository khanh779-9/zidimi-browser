using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ListBoxItem themed for Heco: transparent background, a raised hover state, and a selected
/// state with a light purple background plus a left accent bar. Use it in place of the raw <c>ListBoxItem</c>.
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
/// Custom ListBox themed for Heco: transparent background, no border, meant to be used with
/// <see cref="HecoListBoxItem"/>. Good for suggestion dropdowns and lists.
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
