using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

/// <summary>Color variant of a toast.</summary>
public enum HecoToastVariant
{
    Info,
    Success,
    Warn,
    Danger,
}

/// <summary>
/// A toast notification (a small card that disappears by itself after a period of time).
/// It is managed by a host <see cref="HecoToastHost"/>.
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

    /// <summary>Command used to close the toast (assigned by the host).</summary>
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
/// Host that manages the toasts within a window. Place it in the MainWindow's grid (usually bottom-right).
/// Usage: <c>ToastHost.Show("Title", "Content", HecoToastVariant.Success);</c>
/// A toast disappears on its own after <see cref="DefaultDuration"/> (3 seconds) and has a close button.
/// </summary>
public class HecoToastHost : Control
{
    static HecoToastHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HecoToastHost),
            new FrameworkPropertyMetadata(typeof(HecoToastHost)));
    }

    /// <summary>Default display time for each toast (in seconds).</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(3);

    private StackPanel? _panel;
    private readonly System.Collections.Generic.List<HecoToast> _active = new();

    public static readonly DependencyProperty MaxVisibleProperty = DependencyProperty.Register(
        nameof(MaxVisible), typeof(int), typeof(HecoToastHost), new PropertyMetadata(4));

    /// <summary>Maximum number of toasts shown at the same time (the oldest toast is dismissed first).</summary>
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

    /// <summary>Shows a toast.</summary>
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
