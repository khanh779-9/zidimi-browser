using System.Windows;
using CefSharp;
using CefSharp.DevTools;
using CefSharp.DevTools.Target;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Keeps Chrome-runtime top-level page targets inside Zidimi's tab strip.
///
/// Extension APIs such as chrome.tabs.create/chrome.windows.create (including post-install welcome
/// pages) may create a top-level Chromium page target that does not pass through a tab's
/// ILifeSpanHandler. This router observes those native targets through CefSharp DevTools, closes
/// the unmanaged top-level target, and re-opens the URL as a normal Zidimi Chromium tab.
///
/// No first-run registry, URL history, extension package cache, or other Zidimi persistence is
/// created. Chromium remains the sole owner of extension install/lifecycle state.
/// </summary>
public sealed class ChromiumTopLevelTargetRouter : IDisposable
{
    private static readonly Lazy<ChromiumTopLevelTargetRouter> LazyInstance =
        new(() => new ChromiumTopLevelTargetRouter());

    public static ChromiumTopLevelTargetRouter Instance => LazyInstance.Value;

    private readonly object _gate = new();
    private readonly HashSet<IChromiumWebBrowserBase> _registeredBrowsers =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _knownZidimiTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _handledTargets = new(StringComparer.Ordinal);
    private readonly List<(string Url, DateTime ExpiresUtc)> _expectedZidimiNavigations = new();

    private DevToolsClient? _monitorClient;
    private IChromiumWebBrowserBase? _monitorBrowser;
    private bool _discoveryEnabled;

    private ChromiumTopLevelTargetRouter() { }

    /// <summary>
    /// Reserves the initial URL of a ChromiumWebBrowser that Zidimi itself is about to create.
    /// Target discovery can see the native target before the WPF control finishes initialization;
    /// this reservation prevents a legitimate tab/action popup from being routed a second time.
    /// </summary>
    public void ExpectZidimiNavigation(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsRoutableTopLevelUrl(url)) return;
        var canonical = Canonicalize(url);

        lock (_gate)
        {
            PruneExpectedNoLock();
            _expectedZidimiNavigations.Add((canonical, DateTime.UtcNow.AddSeconds(8)));
            if (_expectedZidimiNavigations.Count > 32)
                _expectedZidimiNavigations.RemoveRange(0, _expectedZidimiNavigations.Count - 32);
        }
    }

    public async Task RegisterBrowserAsync(IChromiumWebBrowserBase browser)
    {
        if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized) return;

        DevToolsClient? client = null;
        var keepClient = false;
        try
        {
            client = browser.GetDevToolsClient();
            var current = await client.Target.GetTargetInfoAsync().ConfigureAwait(false);
            var currentId = current?.TargetInfo?.TargetId;
            if (!string.IsNullOrWhiteSpace(currentId))
            {
                lock (_gate) _knownZidimiTargets.Add(currentId);
            }

            lock (_gate)
            {
                _registeredBrowsers.Add(browser);
                if (_monitorClient == null || _monitorBrowser == null || _monitorBrowser.IsDisposed)
                {
                    DetachMonitorNoLock();
                    _monitorClient = client;
                    _monitorBrowser = browser;
                    keepClient = true;
                    _monitorClient.Target.TargetCreated += Target_TargetCreated;
                    _monitorClient.Target.TargetInfoChanged += Target_TargetInfoChanged;
                }
            }

            if (!keepClient) return;

            await client.Target.SetDiscoverTargetsAsync(true).ConfigureAwait(false);
            lock (_gate) _discoveryEnabled = true;

            // A service worker may open a welcome page before the first WPF browser finishes
            // initialization. Enumerate existing targets once discovery is enabled so it is not lost.
            var targets = await client.Target.GetTargetsAsync().ConfigureAwait(false);
            if (targets?.TargetInfos != null)
            {
                foreach (var target in targets.TargetInfos)
                    _ = ConsiderTargetAsync(target);
            }

            AppLogger.Log("ExtensionRuntime", "Chromium top-level target router enabled.");
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionRuntime", ex, "Registering Chromium target discovery.");
        }
        finally
        {
            if (!keepClient) client?.Dispose();
        }
    }

    public void UnregisterBrowser(IChromiumWebBrowserBase browser)
    {
        IChromiumWebBrowserBase? replacement = null;
        lock (_gate)
        {
            _registeredBrowsers.Remove(browser);
            if (!ReferenceEquals(_monitorBrowser, browser)) return;

            DetachMonitorNoLock();
            replacement = _registeredBrowsers.FirstOrDefault(x => !x.IsDisposed && x.IsBrowserInitialized);
        }

        if (replacement != null)
            _ = RegisterBrowserAsync(replacement);
    }

    private void Target_TargetCreated(object? sender, TargetCreatedEventArgs e)
        => _ = ConsiderTargetAsync(e.TargetInfo);

    private void Target_TargetInfoChanged(object? sender, TargetInfoChangedEventArgs e)
        => _ = ConsiderTargetAsync(e.TargetInfo);

    private async Task ConsiderTargetAsync(TargetInfo? info)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.TargetId)) return;
        if (!string.Equals(info.Type, "page", StringComparison.OrdinalIgnoreCase)) return;

        var url = (info.Url ?? string.Empty).Trim();
        if (!IsRoutableTopLevelUrl(url)) return;

        DevToolsClient? monitor;
        lock (_gate)
        {
            if (!_discoveryEnabled || _knownZidimiTargets.Contains(info.TargetId) ||
                _handledTargets.Contains(info.TargetId)) return;

            if (TryClaimExpectedNoLock(url, info.TargetId)) return;
            if (!_handledTargets.Add(info.TargetId)) return;
            monitor = _monitorClient;
        }

        // Close immediately: delaying here is what makes a native Chromium window visibly flash.
        try
        {
            if (monitor != null)
                await monitor.Target.CloseTargetAsync(info.TargetId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The target can disappear between discovery and close; routing the URL is still safe.
            AppLogger.Log("ExtensionRuntime", ex, $"Closing unmanaged Chromium target {info.TargetId}.");
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

        _ = dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                App.ViewModel?.NewTab(url);
                AppLogger.Log("ExtensionRuntime", $"Routed Chromium top-level page into Zidimi tab. Url={url}");
            }
            catch (Exception ex)
            {
                AppLogger.Log("ExtensionRuntime", ex, $"Routing Chromium page {url}.");
            }
        }));
    }

    private bool TryClaimExpectedNoLock(string url, string targetId)
    {
        PruneExpectedNoLock();
        var canonical = Canonicalize(url);
        var index = _expectedZidimiNavigations.FindIndex(x =>
            string.Equals(x.Url, canonical, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;

        _expectedZidimiNavigations.RemoveAt(index);
        _knownZidimiTargets.Add(targetId);
        return true;
    }

    private void PruneExpectedNoLock()
    {
        var now = DateTime.UtcNow;
        _expectedZidimiNavigations.RemoveAll(x => x.ExpiresUtc < now);
    }

    private static string Canonicalize(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.Trim();
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static bool IsRoutableTopLevelUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("chrome-extension", StringComparison.OrdinalIgnoreCase);
    }

    private void DetachMonitorNoLock()
    {
        if (_monitorClient != null)
        {
            try
            {
                _monitorClient.Target.TargetCreated -= Target_TargetCreated;
                _monitorClient.Target.TargetInfoChanged -= Target_TargetInfoChanged;
                _monitorClient.Dispose();
            }
            catch { }
        }

        _monitorClient = null;
        _monitorBrowser = null;
        _discoveryEnabled = false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _registeredBrowsers.Clear();
            _knownZidimiTargets.Clear();
            _handledTargets.Clear();
            _expectedZidimiNavigations.Clear();
            DetachMonitorNoLock();
        }
    }
}
