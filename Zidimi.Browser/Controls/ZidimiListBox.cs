using System.Windows;
using System.Windows.Controls;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Custom ListBoxItem themed for Zidimi: transparent background, a raised hover state, and a selected
/// state with a light purple background plus a left accent bar. Use it in place of the raw <c>ListBoxItem</c>.
/// </summary>
public class ZidimiListBoxItem : ListBoxItem
{
    static ZidimiListBoxItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiListBoxItem),
            new FrameworkPropertyMetadata(typeof(ZidimiListBoxItem)));
    }
}

/// <summary>
/// Custom ListBox themed for Zidimi: transparent background, no border, meant to be used with
/// <see cref="ZidimiListBoxItem"/>. Good for suggestion dropdowns and lists.
/// </summary>
public class ZidimiListBox : ListBox
{
    static ZidimiListBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiListBox),
            new FrameworkPropertyMetadata(typeof(ZidimiListBox)));
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
        => item is ZidimiListBoxItem;

    protected override DependencyObject GetContainerForItemOverride()
        => new ZidimiListBoxItem();
}
