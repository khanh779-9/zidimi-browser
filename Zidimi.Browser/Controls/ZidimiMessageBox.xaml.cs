using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Controls;

public enum ZidimiMessageBoxButton
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
}

public enum ZidimiMessageBoxImage
{
    None,
    Information,
    Warning,
    Error,
    Question,
    Success,
}

public enum ZidimiMessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No,
}

public sealed partial class ZidimiMessageBox : Window
{
    public ZidimiMessageBoxResult Result { get; private set; } = ZidimiMessageBoxResult.None;

    public ZidimiMessageBox()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    public static ZidimiMessageBoxResult Show(
        string message,
        string title = "Zidimi Browser",
        ZidimiMessageBoxButton button = ZidimiMessageBoxButton.OK,
        ZidimiMessageBoxImage image = ZidimiMessageBoxImage.None,
        Window? owner = null)
    {
        var box = new ZidimiMessageBox();
        box.TitleText.Text = title ?? "Zidimi Browser";
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

    private void ApplyImage(ZidimiMessageBoxImage image)
    {
        var geometry = image switch
        {
            ZidimiMessageBoxImage.Information  => "M12 2 a10 10 0 1 0 0.01 0 Z M12 16 v-4 M12 8 h.01",
            ZidimiMessageBoxImage.Warning      => "M10.29 3.86 L1.82 18 a2 2 0 0 0 1.71 3 h16.94 a2 2 0 0 0 1.71-3 L13.71 3.86 a2 2 0 0 0-3.42 0 z M12 9 v4 M12 17 h.01",
            ZidimiMessageBoxImage.Error        => "M12 2 a10 10 0 1 0 0.01 0 z M15 9 l-6 6 M9 9 l6 6",
            ZidimiMessageBoxImage.Question     => "M12 2 a10 10 0 1 0 0.01 0 z M9.09 9 a3 3 0 0 1 5.83 1 c0 2-3 3-3 3 M12 17 h.01",
            ZidimiMessageBoxImage.Success      => "M12 2 a10 10 0 1 0 0.01 0 z M8 12 l3 3 l5-6",
            _ => "M12 2 a10 10 0 1 0 0.01 0 z M12 8 v12 M12 16 h.01",
        };

        var stroke = image switch
        {
            ZidimiMessageBoxImage.Information => "InfoBrush",
            ZidimiMessageBoxImage.Warning     => "WarnBrush",
            ZidimiMessageBoxImage.Error        => "DangerBrush",
            ZidimiMessageBoxImage.Question    => "ZidimiPurpleLightBrush",
            ZidimiMessageBoxImage.Success      => "SafeBrush",
            _                                => "ZidimiPurpleLightBrush",
        };

        BodyIcon.Data = Geometry.Parse(geometry);
        BodyIcon.Stroke = (Brush)Application.Current.FindResource(stroke);
        if (image == ZidimiMessageBoxImage.None)
        {
            BodyIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyButtons(ZidimiMessageBoxButton button)
    {
        ButtonBar.Children.Clear();
        void Add(string content, ZidimiMessageBoxResult result, bool isPrimary, bool isDanger, bool isDefault, bool isCancel)
        {
            var btn = new ZidimiButton
            {
                Content = content,
                MinWidth = 96,
                Padding = new Thickness(16, 9, 16, 9),
                Margin = new Thickness(8, 0, 0, 0),
            };
            if (isDanger) btn.Style = (Style)FindResource("ZidimiButtonDanger");
            else if (isPrimary) btn.Style = (Style)FindResource("ZidimiButtonPrimary");
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
            case ZidimiMessageBoxButton.OK:
                Add("OK", ZidimiMessageBoxResult.OK, isPrimary: true, isDanger: false, isDefault: true, isCancel: true);
                break;
            case ZidimiMessageBoxButton.OKCancel:
                Add(LanguageManager.Instance["MsgBox_Cancel"], ZidimiMessageBoxResult.Cancel, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add("OK", ZidimiMessageBoxResult.OK, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
            case ZidimiMessageBoxButton.YesNo:
                Add(LanguageManager.Instance["MsgBox_No"], ZidimiMessageBoxResult.No, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add(LanguageManager.Instance["MsgBox_Yes"], ZidimiMessageBoxResult.Yes, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
            case ZidimiMessageBoxButton.YesNoCancel:
                Add(LanguageManager.Instance["MsgBox_Cancel"], ZidimiMessageBoxResult.Cancel, isPrimary: false, isDanger: false, isDefault: false, isCancel: true);
                Add(LanguageManager.Instance["MsgBox_No"], ZidimiMessageBoxResult.No, isPrimary: false, isDanger: false, isDefault: false, isCancel: false);
                Add(LanguageManager.Instance["MsgBox_Yes"], ZidimiMessageBoxResult.Yes, isPrimary: true, isDanger: false, isDefault: true, isCancel: false);
                break;
        }
    }
}
