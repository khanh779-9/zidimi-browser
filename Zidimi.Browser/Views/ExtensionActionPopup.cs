using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shell;
using CefSharp;
using CefSharp.Wpf.HwndHost;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

/// <summary>
/// Hosts an extension action.default_popup as a small owned tool window anchored to the
/// extension toolbar button.
///
/// A WPF Popup is deliberately NOT used here. CefSharp.Wpf.HwndHost renders through a child
/// HWND and keyboard/focus routing is unreliable when that HWND is placed inside WPF's Popup
/// window. A borderless owned Window still looks/behaves like a browser-action popup (no taskbar,
/// no title bar, closes on deactivation) while giving Chromium a real activatable parent HWND.
///
/// The surface uses the same profile IRequestContext as normal tabs so chrome-extension:// pages
/// keep their extension runtime/storage. Zidimi additionally bridges the logical active Zidimi
/// tab to chrome.tabs.query/get because CEF's embedded browser tabs are not backed by Zidimi's
/// WPF TabStripModel; CefBrowser.Identifier is the extension API tabId.
/// </summary>
public sealed class ExtensionActionPopup : IDisposable
{
    private const double MinPopupWidth = 25;
    private const double MinPopupHeight = 25;
    private const double MaxPopupWidth = 800;
    private const double MaxPopupHeight = 600;
    private const double InitialWidth = 360;
    private const double InitialHeight = 420;
    private const double PopupGap = 6;
    private const double FrameThickness = 1;
    private const double FrameInset = 3;
    private const double FrameExtent = FrameThickness + FrameInset;

    // Prefer body dimensions because documentElement.scrollHeight is commonly at least the
    // current viewport height, which prevents a popup from shrinking to the developer's content.
    // Fall back to the root element for extensions that render outside body or use root overflow.
    private const string MeasureScript = """
        (() => {
            const body = document.body;
            const root = document.documentElement;
            const bodyRect = body?.getBoundingClientRect();
            const rootRect = root?.getBoundingClientRect();

            const bodyWidth = Math.max(
                body?.scrollWidth || 0,
                body?.offsetWidth || 0,
                Math.ceil(bodyRect?.width || 0));
            const bodyHeight = Math.max(
                body?.scrollHeight || 0,
                body?.offsetHeight || 0,
                Math.ceil(bodyRect?.height || 0));

            const rootWidth = Math.max(
                root?.scrollWidth || 0,
                Math.ceil(rootRect?.width || 0));
            const rootHeight = Math.max(
                root?.scrollHeight || 0,
                Math.ceil(rootRect?.height || 0));

            const width = bodyWidth > 0 ? bodyWidth : rootWidth;
            const height = bodyHeight > 0 ? bodyHeight : rootHeight;
            return `${Math.ceil(width)}|${Math.ceil(height)}`;
        })();
        """;

    private readonly Window _window;
    private readonly Border _host;
    private readonly FrameworkElement _placementTarget;
    private readonly ChromiumWebBrowser _browser;
    private readonly DispatcherTimer _measureTimer;
    private readonly DispatcherTimer _deactivateTimer;
    private readonly ExtensionTabSnapshot _tabSnapshot;
    private bool _isMeasuring;
    private int _measureTicks;
    private int _stableMeasureTicks;
    private double _lastMeasuredWidth = -1;
    private double _lastMeasuredHeight = -1;
    private bool _disposed;
    private bool _isClosing;

    public ExtensionActionPopup(
        ExtensionInfo extension,
        string popupUrl,
        IRequestContext requestContext,
        FrameworkElement placementTarget,
        ExtensionTabSnapshot? tabSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentException.ThrowIfNullOrWhiteSpace(popupUrl);
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(placementTarget);

        _placementTarget = placementTarget;
        _tabSnapshot = tabSnapshot ?? ExtensionTabSnapshot.Empty;
        ExtensionId = !string.IsNullOrWhiteSpace(extension.RuntimeId)
            ? extension.RuntimeId
            : extension.Id;

        // This Chromium instance is a Zidimi-owned toolbar surface, not a top-level page.
        // Reserve its initial chrome-extension:// URL before CEF creates the native target so
        // ChromiumTopLevelTargetRouter cannot mistake the action popup for an unmanaged
        // extension window and convert it into a normal Zidimi tab.
        ChromiumTopLevelTargetRouter.Instance.ExpectZidimiNavigation(popupUrl);

        _browser = new ChromiumWebBrowser
        {
            Address = popupUrl,
            RequestContext = requestContext,
            Width = InitialWidth,
            Height = InitialHeight,
            Focusable = true,
            IsHitTestVisible = true,
            // HwndHost is a windowed Chromium browser. Do not force WindowlessFrameRate here;
            // that setting is for OSR/windowless rendering and only adds misleading tuning noise.
            // Nested window.open/target=_blank requests from the extension must never fall
            // through to CEF's default top-level native Chromium window.
            LifeSpanHandler = new LifeSpanHandler(
                url => App.ViewModel.NewTab(url),
                $"Extension:{extension.Name}"),
            FocusHandler = new ExtensionPopupFocusHandler(OnNativeBrowserGotFocus),
            RenderProcessMessageHandler = new ExtensionTabBridgeRenderProcessMessageHandler(_tabSnapshot)
        };

        _host = new Border
        {
            // HwndHost is a native child HWND and cannot be reliably clipped by WPF's rounded
            // geometry. Keep the Chromium surface physically inset from every window edge instead
            // so it can never paint over the border/corners (the classic WPF airspace leak).
            Width = InitialWidth + (FrameExtent * 2),
            Height = InitialHeight + (FrameExtent * 2),
            BorderThickness = new Thickness(FrameThickness),
            Padding = new Thickness(FrameInset),
            CornerRadius = new CornerRadius(10),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            ClipToBounds = true,
            IsHitTestVisible = true,
            Child = _browser
        };
        _host.SetResourceReference(Border.BackgroundProperty, "ZidimiBgElevatedBrush");
        _host.SetResourceReference(Border.BorderBrushProperty, "StrokeLightBrush");

        // WPF Popup + HwndHost is a known bad focus/input combination. Use a borderless owned
        // Window instead: visually it is still an action popup, but its Chromium child gets a
        // normal activatable HWND parent and can receive mouse/keyboard input reliably.
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = true,
            AllowsTransparency = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            SizeToContent = SizeToContent.Manual,

            // App.xaml has an implicit Window style with MinWidth=960/MinHeight=640 for normal
            // Zidimi windows. A raw extension action window inherits that style too unless these
            // values are explicitly overridden, which was why tiny extension popups expanded to an
            // almost full browser-sized 960x640 surface around their actual content.
            MinWidth = MinPopupWidth + (FrameExtent * 2),
            MinHeight = MinPopupHeight + (FrameExtent * 2),
            MaxWidth = MaxPopupWidth + (FrameExtent * 2),
            MaxHeight = MaxPopupHeight + (FrameExtent * 2),
            Width = _host.Width,
            Height = _host.Height,
            Content = _host
        };
        _window.SetResourceReference(Window.BackgroundProperty, "ZidimiBgElevatedBrush");

        // Ask WPF/Windows for a real non-client shadow instead of a DropShadowEffect around an
        // HwndHost. The shadow belongs to the top-level popup HWND, so the Chromium child cannot
        // cover it and no P/Invoke/native helper is required.
        WindowChrome.SetWindowChrome(_window, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            UseAeroCaptionButtons = false
        });

        _measureTimer = new DispatcherTimer(DispatcherPriority.Background, placementTarget.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _measureTimer.Tick += MeasureTimer_Tick;

        // Do not close synchronously on Deactivated. Chromium can briefly transfer native focus
        // between child HWNDs while processing clicks, menus and IME. A short deferred check keeps
        // genuine outside-click dismissal while avoiding the classic "first click closes/freezes"
        // race of HwndHost-based popup surfaces.
        _deactivateTimer = new DispatcherTimer(DispatcherPriority.Input, placementTarget.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _deactivateTimer.Tick += DeactivateTimer_Tick;

        _browser.IsBrowserInitializedChanged += Browser_IsBrowserInitializedChanged;
        _browser.Loaded += Browser_Loaded;
        _browser.FrameLoadEnd += Browser_FrameLoadEnd;
        _window.ContentRendered += Window_ContentRendered;
        _window.Activated += Window_Activated;
        _window.Deactivated += Window_Deactivated;
        _window.Closed += Window_Closed;
        _window.PreviewKeyDown += Window_PreviewKeyDown;
    }

    public bool IsOpen => !_disposed && _window.IsVisible;
    public string ExtensionId { get; }
    public FrameworkElement PlacementTarget => _placementTarget;

    public event EventHandler? Closed;

    public void Show()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ExtensionActionPopup));

        if (_window.IsVisible)
        {
            _window.Activate();
            FocusBrowser();
            return;
        }

        var owner = Window.GetWindow(_placementTarget);
        if (owner != null && !ReferenceEquals(owner, _window))
            _window.Owner = owner;

        PositionWindow();
        _window.Show();
        _window.Activate();

        // Let the owned window finish activation before transferring keyboard focus into the
        // child Chromium HWND. This is the important difference from hosting HwndHost in Popup.
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusBrowser));
        RestartMeasurementBurst();
    }

    public void Close()
    {
        if (_disposed || _isClosing) return;
        _isClosing = true;
        _window.Close();
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (_disposed) return;
        PositionWindow();
        FocusBrowser();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _deactivateTimer.Stop();
        FocusBrowser();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_disposed || _isClosing) return;

        // Chromium/HwndHost owns native child windows. Closing synchronously from Deactivated can
        // race the mouse-down that is supposed to reach the extension. Re-check after the native
        // focus transition has settled; an actual outside click remains deactivated and closes.
        _deactivateTimer.Stop();
        _deactivateTimer.Start();
    }

    private void DeactivateTimer_Tick(object? sender, EventArgs e)
    {
        _deactivateTimer.Stop();
        if (_disposed || _isClosing || _window.IsActive) return;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void OnNativeBrowserGotFocus()
    {
        if (_disposed || _browser.IsDisposed) return;

        // IFocusHandler.OnGotFocus is raised when CEF's native child receives focus (for example
        // from a direct mouse click). Mirror that state into WPF logical focus so the owner window
        // does not continue treating the toolbar button as the focused element.
        _browser.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_disposed || _browser.IsDisposed || !_window.IsVisible) return;
            try
            {
                var scope = FocusManager.GetFocusScope(_browser);
                if (!ReferenceEquals(FocusManager.GetFocusedElement(scope), _browser))
                    FocusManager.SetFocusedElement(scope, _browser);
            }
            catch (Exception ex)
            {
                AppLogger.Log("ExtensionActionPopup.LogicalFocus", ex);
            }
        }));
    }

    private void FocusBrowser()
    {
        if (_disposed || !_window.IsVisible || _browser.IsDisposed) return;

        try
        {
            // Keep WPF logical focus in sync with the native child HWND. CefSharp.Wpf.HwndHost
            // receives native focus itself, but without logical focus WPF keyboard routing can
            // remain on the owner toolbar.
            var scope = FocusManager.GetFocusScope(_browser);
            FocusManager.SetFocusedElement(scope, _browser);
            Keyboard.Focus(_browser);
            _browser.Focus();

            if (_browser.IsBrowserInitialized)
                _browser.GetBrowserHost()?.SetFocus(true);
        }
        catch (ObjectDisposedException)
        {
            // Popup was dismissed while focus was being transferred.
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionActionPopup.Focus", ex);
        }
    }

    private void PositionWindow()
    {
        if (_disposed || !_placementTarget.IsVisible) return;

        try
        {
            // PointToScreen returns physical pixels; WPF Window.Left/Top use DIPs. Convert using
            // the presentation source of the toolbar button so mixed-DPI monitors stay aligned.
            var anchorPixels = _placementTarget.PointToScreen(
                new Point(_placementTarget.ActualWidth, _placementTarget.ActualHeight));
            var topPixels = _placementTarget.PointToScreen(new Point(0, 0));

            var source = PresentationSource.FromVisual(_placementTarget);
            var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var anchorDip = fromDevice.Transform(anchorPixels);
            var topDip = fromDevice.Transform(topPixels);

            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(
                (int)Math.Round(anchorPixels.X),
                (int)Math.Round(anchorPixels.Y)));

            var workTopLeft = fromDevice.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
            var workBottomRight = fromDevice.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

            var width = Math.Max(_window.Width, MinPopupWidth);
            var height = Math.Max(_window.Height, MinPopupHeight);

            var left = anchorDip.X - width;
            var top = anchorDip.Y + PopupGap;

            // Flip above the toolbar if the popup would overflow below the current monitor.
            if (top + height > workBottomRight.Y)
                top = topDip.Y - height - PopupGap;

            left = Math.Clamp(left, workTopLeft.X, Math.Max(workTopLeft.X, workBottomRight.X - width));
            top = Math.Clamp(top, workTopLeft.Y, Math.Max(workTopLeft.Y, workBottomRight.Y - height));

            _window.Left = left;
            _window.Top = top;
        }
        catch (InvalidOperationException)
        {
            // Placement target was detached while the popup was closing.
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionActionPopup.Position", ex);
        }
    }

    private void Browser_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        _browser.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusBrowser));
    }

    private void Browser_FrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
    {
        if (!e.Frame.IsMain || _disposed) return;

        // Extension popups often render asynchronously (service worker state, storage, fonts,
        // framework hydration). Measure for a short burst rather than freezing the first layout.
        _browser.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RestartMeasurementBurst));
    }

    private void RestartMeasurementBurst()
    {
        if (_disposed || !_window.IsVisible) return;
        _measureTicks = 0;
        _stableMeasureTicks = 0;
        _lastMeasuredWidth = -1;
        _lastMeasuredHeight = -1;
        _measureTimer.Stop();
        _measureTimer.Start();
        _ = MeasureAndApplyAsync();
    }

    private async void MeasureTimer_Tick(object? sender, EventArgs e)
    {
        if (_disposed || !_window.IsVisible)
        {
            _measureTimer.Stop();
            return;
        }

        _measureTicks++;
        await MeasureAndApplyAsync();

        // Stop early once the popup is stable. Dynamic extension UIs still get a bounded burst,
        // but a simple popup normally needs only a few EvaluateScriptAsync calls instead of polling
        // for the full lifetime of the window.
        if (_stableMeasureTicks >= 3 || _measureTicks >= 10)
            _measureTimer.Stop();
    }

    private async Task MeasureAndApplyAsync()
    {
        if (_disposed || _isMeasuring || !_window.IsVisible || !_browser.IsBrowserInitialized)
            return;

        _isMeasuring = true;
        try
        {
            using var frame = _browser.GetMainFrame();
            if (frame == null || !frame.IsValid) return;

            var response = await frame.EvaluateScriptAsync(MeasureScript);
            if (!response.Success || response.Result is not string raw) return;

            var parts = raw.Split('|');
            if (parts.Length != 2 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
            {
                return;
            }

            width = Math.Clamp(Math.Ceiling(width), MinPopupWidth, MaxPopupWidth);
            height = Math.Clamp(Math.Ceiling(height), MinPopupHeight, MaxPopupHeight);

            if (Math.Abs(_lastMeasuredWidth - width) < 0.5 &&
                Math.Abs(_lastMeasuredHeight - height) < 0.5)
                _stableMeasureTicks++;
            else
                _stableMeasureTicks = 0;

            _lastMeasuredWidth = width;
            _lastMeasuredHeight = height;

            if (Math.Abs(_browser.Width - width) < 0.5 && Math.Abs(_browser.Height - height) < 0.5)
                return;

            _browser.Width = width;
            _browser.Height = height;
            _host.Width = width + (FrameExtent * 2);
            _host.Height = height + (FrameExtent * 2);
            _window.Width = _host.Width;
            _window.Height = _host.Height;

            // Keep the action popup's right edge attached to the toolbar icon as it resizes.
            PositionWindow();
        }
        catch (ObjectDisposedException)
        {
            // Popup was dismissed while an asynchronous measurement was in flight.
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionActionPopup.Measure", ex);
        }
        finally
        {
            _isMeasuring = false;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _isClosing = true;

        Closed?.Invoke(this, EventArgs.Empty);
        DisposeCore();
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_window.IsVisible && !_isClosing)
        {
            Close();
            return;
        }

        DisposeCore();
    }

    private void DisposeCore()
    {
        if (_disposed) return;
        _disposed = true;

        _measureTimer.Stop();
        _measureTimer.Tick -= MeasureTimer_Tick;
        _deactivateTimer.Stop();
        _deactivateTimer.Tick -= DeactivateTimer_Tick;

        ChromiumTopLevelTargetRouter.Instance.UnregisterBrowser(_browser);

        _browser.IsBrowserInitializedChanged -= Browser_IsBrowserInitializedChanged;
        _browser.Loaded -= Browser_Loaded;
        _browser.FrameLoadEnd -= Browser_FrameLoadEnd;

        _window.ContentRendered -= Window_ContentRendered;
        _window.Activated -= Window_Activated;
        _window.Deactivated -= Window_Deactivated;
        _window.Closed -= Window_Closed;
        _window.PreviewKeyDown -= Window_PreviewKeyDown;

        _window.Content = null;
        _host.Child = null;
        _browser.Dispose();
    }

    private async void Browser_IsBrowserInitializedChanged(object? sender, EventArgs e)
    {
        if (_disposed || !_browser.IsBrowserInitialized || _browser.IsDisposed) return;

        try
        {
            // Mark the popup's actual CDP target as Zidimi-owned. The URL reservation above
            // protects the short race before this callback fires; registration protects later
            // TargetInfoChanged events and redirects performed by the popup itself.
            await ChromiumTopLevelTargetRouter.Instance.RegisterBrowserAsync(_browser);

            AppLogger.Log("ExtensionActionPopup.Runtime",
                $"Popup initialized. Extension={ExtensionId}; ActiveZidimiTabId={_tabSnapshot.ActiveTabId}; " +
                $"VisibleZidimiTabs={_tabSnapshot.TabIds.Count}");

            await _browser.Dispatcher.InvokeAsync(FocusBrowser, DispatcherPriority.Input);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionActionPopup.Runtime", ex,
                $"Registering hosted extension popup target. Extension={ExtensionId}");
        }
    }

    private sealed class ExtensionPopupFocusHandler : CefSharp.Handler.FocusHandler
    {
        private readonly Action _onGotFocus;

        public ExtensionPopupFocusHandler(Action onGotFocus)
        {
            _onGotFocus = onGotFocus ?? throw new ArgumentNullException(nameof(onGotFocus));
        }

        protected override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            _onGotFocus();
        }

        protected override bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
        {
            // Never block CEF focus for the action popup.
            return false;
        }
    }
}
