using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom Window control theo phong cách Chrome:
///   - Code-only base class, UI nằm trong ControlTemplate (HecoWindowStyle trong Controls\HecoWindow.xaml)
///   - Title bar chính là tab strip (TabStripContent) + caption buttons (Chrome-style)
///   - Chỉ khai báo DependencyProperties; template tự render title bar + caption buttons
///   - Khi maximized, RootGrid được lùi vào một khoảng (MaximizedPadding) để thấy rõ border
///   - Double-click / kéo trên title bar được xử lý bởi WindowChrome (CaptionHeight)
///
/// Cách dùng:
///   <ctrl:HecoWindow Style="{StaticResource HecoWindowStyle}">
///       <ctrl:HecoWindow.TabStripContent>
///           <views:TabStrip />
///       </ctrl:HecoWindow.TabStripContent>
///       <Grid> ... nội dung chính ... </Grid>
///   </ctrl:HecoWindow>
/// </summary>
public class HecoWindow : Window
{
    public static readonly DependencyProperty TabStripContentProperty = DependencyProperty.Register(
        nameof(TabStripContent), typeof(object), typeof(HecoWindow),
        new PropertyMetadata(null, (d, e) =>
        {
            var w = (HecoWindow)d;
            if (w._tabStripSlot != null) w._tabStripSlot.Content = e.NewValue;
        }));

    public static readonly DependencyProperty BrandTextProperty = DependencyProperty.Register(
        nameof(BrandText), typeof(string), typeof(HecoWindow),
        new PropertyMetadata("Heco Browser"));

    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
        nameof(IsMaximized), typeof(bool), typeof(HecoWindow),
        new PropertyMetadata(false));

    public static readonly DependencyProperty MaximizedPaddingProperty = DependencyProperty.Register(
        nameof(MaximizedPadding), typeof(Thickness), typeof(HecoWindow),
        new PropertyMetadata(new Thickness(8)));

    public static readonly DependencyProperty UseNativeCaptionProperty = DependencyProperty.Register(
        nameof(UseNativeCaption), typeof(bool), typeof(HecoWindow),
        new PropertyMetadata(false, (d, _) => ((HecoWindow)d).ApplyChrome()));

    public object? TabStripContent
    {
        get => GetValue(TabStripContentProperty);
        set => SetValue(TabStripContentProperty, value);
    }

    public string BrandText
    {
        get => (string)GetValue(BrandTextProperty);
        set => SetValue(BrandTextProperty, value);
    }

    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    public Thickness MaximizedPadding
    {
        get => (Thickness)GetValue(MaximizedPaddingProperty);
        set => SetValue(MaximizedPaddingProperty, value);
    }

    /// <summary>
    /// True: dùng caption buttons gốc của Windows (WindowChrome.UseAeroCaptionButtons = true),
    /// custom caption buttons trong template sẽ bị ẩn. False (mặc định): custom Chrome-style buttons.
    /// </summary>
    public bool UseNativeCaption
    {
        get => (bool)GetValue(UseNativeCaptionProperty);
        set => SetValue(UseNativeCaptionProperty, value);
    }

    private ContentControl? _tabStripSlot;
    private Button? _minimizeBtn;
    private Button? _maximizeBtn;
    private Button? _closeBtn;
    private Path? _maximizePath;
    private Path? _restorePath;

    public HecoWindow()
    {
        ApplyChrome();
        Style = (Style)Application.Current.FindResource("HecoWindowStyle");
        Background = (Brush)Application.Current.FindResource("AppBackgroundBrush");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        StateChanged += OnStateChanged;
    }

    /// <summary>Áp dụng WindowChrome theo UseNativeCaption (native aero buttons hoặc custom).</summary>
    private void ApplyChrome()
    {
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 40,
            // GlassFrameThickness khác 0 để Windows giữ DWM shadow + animation minimize/maximize
            GlassFrameThickness = new Thickness(0, 0, 0, 1),
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = UseNativeCaption,
        });
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_minimizeBtn != null) _minimizeBtn.Click -= Minimize_Click;
        if (_maximizeBtn != null) _maximizeBtn.Click -= Maximize_Click;
        if (_closeBtn != null) _closeBtn.Click -= Close_Click;

        _tabStripSlot = GetTemplateChild("PART_TabStripSlot") as ContentControl;
        _minimizeBtn = GetTemplateChild("PART_MinimizeBtn") as Button;
        _maximizeBtn = GetTemplateChild("PART_MaximizeBtn") as Button;
        _closeBtn = GetTemplateChild("PART_CloseBtn") as Button;
        _maximizePath = GetTemplateChild("PART_MaximizePath") as Path;
        _restorePath = GetTemplateChild("PART_RestorePath") as Path;

        if (_minimizeBtn != null) _minimizeBtn.Click += Minimize_Click;
        if (_maximizeBtn != null) _maximizeBtn.Click += Maximize_Click;
        if (_closeBtn != null) _closeBtn.Click += Close_Click;

        // Re-apply content nếu DP được set trước khi template load
        if (_tabStripSlot != null) _tabStripSlot.Content = TabStripContent;

        UpdateMaximizeState();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e) => UpdateMaximizeState();

    private void UpdateMaximizeState()
    {
        IsMaximized = WindowState == WindowState.Maximized;

        // RootGrid lùi vào khi maximized để border hiện rõ (template bind Margin vào MaximizedPadding)
        MaximizedPadding = IsMaximized ? new Thickness(8) : new Thickness(0);

        if (_maximizePath != null)
            _maximizePath.Visibility = IsMaximized ? Visibility.Collapsed : Visibility.Visible;
        if (_restorePath != null)
            _restorePath.Visibility = IsMaximized ? Visibility.Visible : Visibility.Collapsed;
        if (_maximizeBtn != null)
            _maximizeBtn.ToolTip = IsMaximized ? "Khôi phục" : "Phóng to";
    }
}
