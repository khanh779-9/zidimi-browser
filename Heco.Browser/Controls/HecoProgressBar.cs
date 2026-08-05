using System.Windows;
using System.Windows.Controls;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom ProgressBar theo theme Heco: thanh mảnh 2px, fill gradient tím,
/// hỗ trợ IsIndeterminate (vệt chạy). Dùng thay <c>ProgressBar</c> gốc.
/// </summary>
public class HecoProgressBar : System.Windows.Controls.ProgressBar
{
    static HecoProgressBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoProgressBar),
            new FrameworkPropertyMetadata(typeof(HecoProgressBar)));
    }
}
