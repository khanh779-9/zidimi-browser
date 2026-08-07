using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Heco.Browser.Controls;

/// <summary>
/// A commonly used helper class.
/// </summary>
public static class IconPaths
{
    // Path data for the SVG icons used in the custom buttons.
    public const string Back = "M15,18 L9,12 L15,6 L16.5,7.5 L12,12 L16.5,16.5 Z";
    public const string Forward = "M9,18 L15,12 L9,6 L7.5,7.5 L12,12 L7.5,16.5 Z";
    public const string Reload = "M17.65,6.35 C16.2,4.9 14.21,4 12,4 c-4.42,0 -7.99,3.58 -7.99,8 s3.57,8 7.99,8 c3.73,0 6.84,-2.55 7.73,-6 h-2.08 c-0.82,2.33 -3.04,4 -5.65,4 -3.31,0 -6,-2.69 -6,-6 s2.69,-6 6,-6 c1.66,0 3.14,0.69 4.22,1.78 L13,11 h7 V4 z";
    public const string Home = "M10,20 V12 H6 V20 H4 a1,1 0 0 1 -1,-1 V11 L12,3 L21,11 V19 a1,1 0 0 1 -1,1 H18 V12 H14 V20 Z";
    public const string Plus = "M11,5 H13 V11 H19 V13 H13 V19 H11 V13 H5 V11 H11 Z";
    public const string Close = "M2,2 L10,10 M10,2 L2,10";
    public const string Star = "M12,17.27 L18.18,21 L16.54,13.97 L22,9.24 L14.71,8.42 L12,2 L9.29,8.42 L2,9.24 L7.46,13.97 L5.82,21 Z";
    public const string Minimize = "M2,7 L12,7";
    public const string Maximize = "M2,2 L10,2 L10,10 L2,10 Z";
    public const string Restore = "M8,2 L2,2 L2,8 M4,4 L10,4 L10,10 L4,10 Z";
    public const string Search = "M15.5,14 h-.79 l-.28,-.27 a6.5,6.5 0 1 0 -.7,.7 l.27,.28 v.79 l5,4.99 L20.49,19 z";
    public const string Trash = "M6,19 c0,1.1 .9,2 2,2 h8 c1.1,0 2,-.9 2,-2 V7 H6 z M19,4 H16 l-1,-1 H9 L8,4 H5 V6 H19 z";
    public const string Menu = "M3,6 H21 V8 H3 Z M3,11 H21 V13 H3 Z M3,16 H21 V18 H3 Z";

    public static Path MakeIcon(string data, double size = 16, Brush? stroke = null, double thickness = 1.5)
    {
        var p = new Path
        {
            Data = Geometry.Parse(data),
            Width = size, Height = size, Stretch = Stretch.Uniform,
            Stroke = stroke ?? Brushes.Transparent,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return p;
    }

    public static Path MakeFilled(string data, double size = 16, Brush? fill = null)
    {
        var p = new Path
        {
            Data = Geometry.Parse(data),
            Width = size, Height = size, Stretch = Stretch.Uniform,
            Fill = fill ?? Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return p;
    }
}
