using System.Windows;
using System.Windows.Controls;

namespace Zidimi.Browser.Controls;

/// <summary>
/// Custom ProgressBar themed for Zidimi: a slim 2px bar with a purple gradient fill,
/// with IsIndeterminate support (an animated sweep). Use it in place of the raw <c>ProgressBar</c>.
/// </summary>
public class ZidimiProgressBar : System.Windows.Controls.ProgressBar
{
    static ZidimiProgressBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ZidimiProgressBar),
            new FrameworkPropertyMetadata(typeof(ZidimiProgressBar)));
    }
}
