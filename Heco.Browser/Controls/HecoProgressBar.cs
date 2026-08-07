using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ProgressBar themed for Heco: a slim 2px bar with a purple gradient fill,
/// with IsIndeterminate support (an animated sweep). Use it in place of the raw <c>ProgressBar</c>.
/// </summary>
public class HecoProgressBar : System.Windows.Controls.ProgressBar
{
    static HecoProgressBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoProgressBar),
            new FrameworkPropertyMetadata(typeof(HecoProgressBar)));
    }
}
