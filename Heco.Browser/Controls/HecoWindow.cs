using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Controls;

/// <summary>
/// Custom Window control styled like Chrome:
///   - Code-only base class; the UI lives in a ControlTemplate (HecoWindowStyle in Controls\HecoWindow.xaml)
///   - The title bar is the tab strip (TabStripContent) plus the caption buttons (Chrome-style)
///   - Only dependency properties are declared; the template renders the title bar and caption buttons itself
///   - When maximized, the RootGrid is inset by a margin (MaximizedPadding) so the border stays visible
///   - Double-click and dragging on the title bar are handled by WindowChrome (CaptionHeight)
///
/// Usage:
///   <ctrl:HecoWindow Style="{StaticResource HecoWindowStyle}">
///       <ctrl:HecoWindow.TabStripContent>
///           <views:TabStrip />
///       </ctrl:HecoWindow.TabStripContent>
///       <Grid> ... main content ... </Grid>
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
    /// When true, uses Windows' native caption buttons (WindowChrome.UseAeroCaptionButtons = true)
    /// and the custom caption buttons in the template are hidden. False (the default) uses custom Chrome-style buttons.
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

    /// <summary>Applies WindowChrome based on UseNativeCaption (native aero buttons or custom).</summary>
    private void ApplyChrome()
    {
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 40,
            // GlassFrameThickness must be non-zero so Windows keeps the DWM shadow and minimize/maximize animation
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

        // Re-apply content in case the DP was set before the template loaded
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

        // Inset the RootGrid when maximized so the border stays visible (the template binds Margin to MaximizedPadding)
        MaximizedPadding = IsMaximized ? new Thickness(8) : new Thickness(0);

        if (_maximizePath != null)
            _maximizePath.Visibility = IsMaximized ? Visibility.Collapsed : Visibility.Visible;
        if (_restorePath != null)
            _restorePath.Visibility = IsMaximized ? Visibility.Visible : Visibility.Collapsed;
        if (_maximizeBtn != null)
            _maximizeBtn.ToolTip = IsMaximized
                ? LanguageManager.Instance["Win_Restore"]
                : LanguageManager.Instance["Win_Maximize"];
    }
}
