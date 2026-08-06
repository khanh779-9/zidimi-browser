using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

public enum HecoMessageBoxButton
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
}

public enum HecoMessageBoxImage
{
    None,
    Information,
    Warning,
    Error,
    Question,
    Success,
}

public enum HecoMessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No,
}

public sealed partial class HecoMessageBox : Window
{
    public HecoMessageBoxResult Result { get; private set; } = HecoMessageBoxResult.None;

    public HecoMessageBox()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    public static HecoMessageBoxResult Show(
        string message,
        string title = "Heco Browser",
        HecoMessageBoxButton button = HecoMessageBoxButton.OK,
        HecoMessageBoxImage image = HecoMessageBoxImage.None,
        Window? owner = null)
    {
        var box = new HecoMessageBox();
        box.TitleText.Text = title ?? "Heco Browser";
        box.MessageText.Text = message ?? "";
        box.ApplyImage(image);
        box.ApplyButtons(button);

        if (owner != null && owner.IsLoaded)
        {
            box.Owner = owner;
        }
        else
        {
            box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var main = Application.Current?.MainWindow;
            if (main != null && main.IsLoaded) box.Owner = main;
        }

        box.ShowDialog();
        return box.Result;
    }

    private void ApplyImage(HecoMessageBoxImage image)
    {
        var geometry = image switch
        {
            HecoMessageBoxImage.Information  => "M12 2 a10 10 0 1 0 0.01 0 Z M12 16 v-4 M12 8 h.01",
            HecoMessageBoxImage.Warning      => "M10.29 3.86 L1.82 18 a2 2 0 0 0 1.71 3 h16.94 a2 2 0 0 0 1.71-3 L13.71 3.86 a2 2 0 0 0-3.42 0 z M12 9 v4 M12 17 h.01",
            HecoMessageBoxImage.Error        => "M12 2 a10 10 0 1 0 0.01 0 z M15 9 l-6 6 M9 9 l6 6",
            HecoMessageBoxImage.Question     => "M12 2 a10 10 0 1 0 0.01 0 z M9.09 9 a3 3 0 0 1 5.83 1 c0 2-3 3-3 3 M12 17 h.01",
            HecoMessageBoxImage.Success      => "M12 2 a10 10 0 1 0 0.01 0 z M8 12 l3 3 l5-6",
            _ => "M12 2 a10 10 0 1 0 0.01 0 z M12 8 v12 M12 16 h.01",
        };

        var stroke = image switch
        {
            HecoMessageBoxImage.Information => "InfoBrush",
            HecoMessageBoxImage.Warning     => "WarnBrush",
            HecoMessageBoxImage.Error        => "DangerBrush",
            HecoMessageBoxImage.Question    => "HecoPurpleLightBrush",
            HecoMessageBoxImage.Success      => "SafeBrush",
            _                                => "HecoPurpleLightBrush",
        };

        BodyIcon.Data = Geometry.Parse(geometry);
        BodyIcon.Stroke = (Brush)Application.Current.FindResource(stroke);
        if (image == HecoMessageBoxImage.None)
        {
            BodyIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyButtons(HecoMessageBoxButton button)
    {
        ButtonBar.Children.Clear();
        void Add(string content, HecoMessageBoxResult result, bool isPrimary, bool isDanger, bool isDefault, bool isCancel)
        {
            var btn = new HecoButton
            {
                Content = content,
                MinWidth = 96,
                Padding = new Thickness(16, 9, 16, 9),
                Margin = new Thickness(8, 0, 0, 0),
            };
            if (isDanger) btn.Style = (Style)FindResource("HecoButtonDanger");
            else if (isPrimary) btn.Style = (Style)FindResource("HecoButtonPrimary");
            btn.Click += (s, e) => { Result = result; DialogResult = true; Close(); };
            if (isCancel)
            {
                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Escape) { Result = result; DialogResult = false; Close(); }
                };
            }
            ButtonBar.Children.Add(btn);
            if (isDefault)
            {
                Loaded += (s, e) => btn.Focus();
            }
        }

        switch (button)
        {
            case HecoMessageBoxButton.OK:
                Add("OK", HecoMessageBoxResult.OK, isPrimary: true, isDanger: false, isDefault: true, isCancel: true);
                break;
            case HecoMessageBoxButton.OKCancel:
                Add(LanguageManager.Instance["MsgBox_Cancel"], HecoMessageBoxResult.Cancel, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add("OK", HecoMessageBoxResult.OK, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
            case HecoMessageBoxButton.YesNo:
                Add(LanguageManager.Instance["MsgBox_No"], HecoMessageBoxResult.No, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add(LanguageManager.Instance["MsgBox_Yes"], HecoMessageBoxResult.Yes, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
            case HecoMessageBoxButton.YesNoCancel:
                Add(LanguageManager.Instance["MsgBox_Cancel"], HecoMessageBoxResult.Cancel, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add(LanguageManager.Instance["MsgBox_No"], HecoMessageBoxResult.No, isPrimary: false, isDanger: false, isDefault: false, isCancel: false);
                Add(LanguageManager.Instance["MsgBox_Yes"], HecoMessageBoxResult.Yes, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
        }
    }
}
