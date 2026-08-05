using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

/// <summary>Biến thể màu của toast.</summary>
public enum HecoToastVariant
{
    Info,
    Success,
    Warn,
    Danger,
}

/// <summary>
/// Một thông báo toast (card nhỏ, tự biến mất sau khoảng thời gian).
/// Được host quản lý bởi <see cref="HecoToastHost"/>.
/// </summary>
public sealed class HecoToast : Control
{
    static HecoToast()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoToast),
            new FrameworkPropertyMetadata(typeof(HecoToast)));
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(HecoToast), new PropertyMetadata(null));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(HecoToast), new PropertyMetadata(null));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(HecoToastVariant), typeof(HecoToast),
        new PropertyMetadata(HecoToastVariant.Info));

    public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(
        nameof(CloseCommand), typeof(System.Windows.Input.ICommand), typeof(HecoToast),
        new PropertyMetadata(null));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public HecoToastVariant Variant
    {
        get => (HecoToastVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Command để tự đóng toast (host gán).</summary>
    public System.Windows.Input.ICommand? CloseCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    internal void Close()
    {
        var host = ToastHostOf(this);
        host?.Dismiss(this);
    }

    internal static HecoToastHost? ToastHostOf(DependencyObject obj)
    {
        while (obj != null)
        {
            if (obj is HecoToastHost host) return host;
            obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
}

/// <summary>
/// Host quản lý các toast trong cửa sổ. Đặt trong Grid của MainWindow (thường bottom-right).
/// Dùng: <c>ToastHost.Show("Tiêu đề", "Nội dung", HecoToastVariant.Success);</c>
/// Toast tự biến mất sau <see cref="DefaultDuration"/> (3 giây), có nút đóng.
/// </summary>
public class HecoToastHost : Control
{
    static HecoToastHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoToastHost),
            new FrameworkPropertyMetadata(typeof(HecoToastHost)));
    }

    /// <summary>Thời gian hiển thị mặc định của mỗi toast (giây).</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(3);

    private StackPanel? _panel;
    private readonly System.Collections.Generic.List<HecoToast> _active = new();

    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(
        nameof(MaxVisible), typeof(int), typeof(HecoToastHost), new PropertyMetadata(4));

    /// <summary>Số toast hiển thị đồng thời tối đa (toast cũ nhất bị loại trước).</summary>
    public int MaxVisible
    {
        get => (int)GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_Panel") as StackPanel;
        _panel?.Children.Clear();
        _active.Clear();
    }

    /// <summary>Hiện một toast.</summary>
    public void Show(string? title, string message, HecoToastVariant variant = HecoToastVariant.Info, TimeSpan? duration = null)
    {
        var toast = new HecoToast
        {
            Title = title,
            Message = message,
            Variant = variant,
        };

        if (Application.Current?.Dispatcher != null &&
            Application.Current.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => ShowCore(toast, duration));
            return;
        }
        ShowCore(toast, duration);
    }

    private void ShowCore(HecoToast toast, TimeSpan? duration)
    {
        if (_panel is null) return;

        toast.CloseCommand = new RelayCommand(_ => toast.Close());
        _panel.Children.Add(toast);
        _active.Add(toast);

        var timer = new DispatcherTimer { Interval = duration ?? DefaultDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Dismiss(toast);
        };
        timer.Start();

        while (_active.Count > MaxVisible)
            Dismiss(_active[0]);
    }

    internal void Dismiss(HecoToast toast)
    {
        if (Application.Current?.Dispatcher != null &&
            Application.Current.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => DismissCore(toast));
            return;
        }
        DismissCore(toast);
    }

    private void DismissCore(HecoToast toast)
    {
        if (_panel is null) return;
        if (!_active.Contains(toast)) return;
        _active.Remove(toast);
        _panel.Children.Remove(toast);
    }
}
