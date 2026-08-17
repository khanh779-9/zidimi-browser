using CefSharp;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Profile-scoped tab registry shared by every extension.
///
/// Zidimi owns the visible WPF TabStrip, while Chromium owns the actual browser instances.
/// CefBrowser.Identifier is therefore the bridge between both worlds: it is kept as the Zidimi
/// web-tab id and is also the tabId consumed by Chromium extension APIs.
///
/// Every live web browser stays registered whether selected or in the background. Selection only
/// changes ActiveTabId; it never removes/suspends the tab from the extension-visible runtime.
/// Permissions remain Chromium's responsibility.
/// </summary>
public sealed class ExtensionRuntimeCoordinator
{
    private static readonly Lazy<ExtensionRuntimeCoordinator> LazyInstance =
        new(() => new ExtensionRuntimeCoordinator());

    public static ExtensionRuntimeCoordinator Instance => LazyInstance.Value;

    private readonly object _gate = new();
    private readonly Dictionary<int, BrowserEntry> _tabsById = new();
    private readonly Dictionary<IChromiumWebBrowserBase, int> _idsByBrowser =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IRequestContext, int> _activeTabByContext =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IRequestContext, List<int>> _tabOrderByContext =
        new(ReferenceEqualityComparer.Instance);
    // tabs.query()/tabs.get() can be called very frequently by extension popups/service workers.
    // Cache the immutable profile snapshot and invalidate it only when tab lifecycle/order/selection
    // changes instead of rebuilding lists/HashSets for every API call.
    private readonly Dictionary<IRequestContext, ExtensionTabSnapshot> _snapshotCache =
        new(ReferenceEqualityComparer.Instance);
    private int _snapshotReadCount;

    private ExtensionRuntimeCoordinator() { }

    /// <summary>Registers a live Chromium web tab and returns its native extension tabId.</summary>
    public int RegisterWebBrowser(IChromiumWebBrowserBase browser, IRequestContext? requestContext)
    {
        if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized || requestContext == null)
            return 0;

        int tabId;
        try
        {
            tabId = browser.BrowserCore?.Identifier ?? 0;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }

        if (tabId <= 0) return 0;

        lock (_gate)
        {
            PruneNoLock();

            if (_idsByBrowser.TryGetValue(browser, out var oldId) && oldId != tabId &&
                _tabsById.Remove(oldId, out var oldEntry))
            {
                RemoveFromOrderNoLock(oldEntry.RequestContext, oldId);
                if (_activeTabByContext.TryGetValue(oldEntry.RequestContext, out var activeId) && activeId == oldId)
                    _activeTabByContext.Remove(oldEntry.RequestContext);
                InvalidateSnapshotNoLock(oldEntry.RequestContext);
            }

            _idsByBrowser[browser] = tabId;
            _tabsById[tabId] = new BrowserEntry(tabId, browser, requestContext);
            InvalidateSnapshotNoLock(requestContext);
        }

        return tabId;
    }

    public void UnregisterWebBrowser(IChromiumWebBrowserBase? browser)
    {
        if (browser == null) return;

        lock (_gate)
        {
            if (!_idsByBrowser.Remove(browser, out var tabId)) return;

            if (_tabsById.Remove(tabId, out var removed))
            {
                RemoveFromOrderNoLock(removed.RequestContext, tabId);
                if (_activeTabByContext.TryGetValue(removed.RequestContext, out var activeId) && activeId == tabId)
                    _activeTabByContext.Remove(removed.RequestContext);
                InvalidateSnapshotNoLock(removed.RequestContext);
            }
        }
    }

    public void UnregisterTabId(int tabId)
    {
        if (tabId <= 0) return;

        lock (_gate)
        {
            if (!_tabsById.Remove(tabId, out var removed)) return;
            _idsByBrowser.Remove(removed.Browser);
            RemoveFromOrderNoLock(removed.RequestContext, tabId);

            if (_activeTabByContext.TryGetValue(removed.RequestContext, out var activeId) && activeId == tabId)
                _activeTabByContext.Remove(removed.RequestContext);
            InvalidateSnapshotNoLock(removed.RequestContext);
        }
    }

    /// <summary>
    /// Changes only the selected tab for a profile. Background tabs remain registered and keep
    /// their browser/content-script/extension relationships alive just like normal browser tabs.
    /// </summary>
    public void SetActiveTab(IRequestContext? requestContext, int tabId)
    {
        if (requestContext == null) return;

        lock (_gate)
        {
            // Normal tab lifecycle unregisters eagerly. Do not scan every browser on the tab-
            // selection hot path; just validate the requested id. The snapshot path keeps a
            // low-frequency defensive prune for unexpected external disposal.
            if (tabId <= 0 ||
                !_tabsById.TryGetValue(tabId, out var entry) ||
                entry.Browser.IsDisposed ||
                !ReferenceEquals(entry.RequestContext, requestContext))
            {
                if (_activeTabByContext.Remove(requestContext))
                    InvalidateSnapshotNoLock(requestContext);
                return;
            }

            if (!_activeTabByContext.TryGetValue(requestContext, out var current) || current != tabId)
            {
                _activeTabByContext[requestContext] = tabId;
                InvalidateSnapshotNoLock(requestContext);
            }
        }
    }

    /// <summary>Compatibility helper for callers that currently hold the browser control.</summary>
    public void SetActiveWebBrowser(IChromiumWebBrowserBase? browser)
    {
        if (browser == null)
        {
            lock (_gate)
            {
                // Used when Zidimi selects a native page. There is only one WPF browser window
                // today, so clear the selected web tab for every profile context without touching
                // any registered background tab.
                _activeTabByContext.Clear();
                _snapshotCache.Clear();
            }
            return;
        }

        lock (_gate)
        {
            if (!_idsByBrowser.TryGetValue(browser, out var tabId) ||
                !_tabsById.TryGetValue(tabId, out var entry) ||
                entry.Browser.IsDisposed)
                return;

            if (!_activeTabByContext.TryGetValue(entry.RequestContext, out var current) || current != tabId)
            {
                _activeTabByContext[entry.RequestContext] = tabId;
                InvalidateSnapshotNoLock(entry.RequestContext);
            }
        }
    }

    /// <summary>
    /// Synchronizes Chromium tab ids with the visual order in Zidimi's WPF TabStrip. Tabs that are
    /// still initializing are ignored until their real CefBrowser.Identifier becomes available.
    /// </summary>
    public void SetTabOrder(IRequestContext? requestContext, IEnumerable<int> orderedTabIds)
    {
        if (requestContext == null) return;
        ArgumentNullException.ThrowIfNull(orderedTabIds);

        lock (_gate)
        {
            var order = orderedTabIds
                .Where(id => id > 0 &&
                    _tabsById.TryGetValue(id, out var entry) &&
                    !entry.Browser.IsDisposed &&
                    ReferenceEquals(entry.RequestContext, requestContext))
                .Distinct()
                .ToList();

            if (order.Count == 0)
                _tabOrderByContext.Remove(requestContext);
            else
                _tabOrderByContext[requestContext] = order;
            InvalidateSnapshotNoLock(requestContext);
        }
    }

    /// <summary>Returns all live web tabIds for one profile plus the selected tabId.</summary>
    public ExtensionTabSnapshot GetSnapshot(IRequestContext? requestContext)
    {
        if (requestContext == null) return ExtensionTabSnapshot.Empty;

        lock (_gate)
        {
            // Normal tab close goes through UnregisterWebBrowser, so a full stale-browser scan is
            // unnecessary on every extension tabs.query(). Keep a low-frequency defensive prune
            // for unexpected external disposal without turning the hot path back into O(n).
            if ((++_snapshotReadCount & 0x3F) == 0)
                PruneNoLock();
            if (_snapshotCache.TryGetValue(requestContext, out var cached))
                return cached;

            var registered = _tabsById.Values
                .Where(entry => ReferenceEquals(entry.RequestContext, requestContext))
                .Select(entry => entry.TabId)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            var registeredSet = registered.ToHashSet();
            var ordered = _tabOrderByContext.TryGetValue(requestContext, out var knownOrder)
                ? knownOrder.Where(registeredSet.Contains).ToList()
                : new List<int>();

            // A newly initialized browser can be registered a few dispatcher ticks before the
            // TabStrip order sync. Append it temporarily instead of hiding it from extensions.
            var orderedSet = ordered.ToHashSet();
            foreach (var id in registered)
                if (orderedSet.Add(id)) ordered.Add(id);

            var ids = ordered.ToArray();

            var activeId = _activeTabByContext.TryGetValue(requestContext, out var selected) &&
                           registeredSet.Contains(selected)
                ? selected
                : 0;

            var snapshot = new ExtensionTabSnapshot(activeId, ids);
            _snapshotCache[requestContext] = snapshot;
            return snapshot;
        }
    }

    public bool ContainsTab(int tabId, IRequestContext? requestContext = null)
    {
        if (tabId <= 0) return false;
        lock (_gate)
        {
            return _tabsById.TryGetValue(tabId, out var entry) &&
                   !entry.Browser.IsDisposed &&
                   (requestContext == null || ReferenceEquals(entry.RequestContext, requestContext));
        }
    }

    private void RemoveFromOrderNoLock(IRequestContext requestContext, int tabId)
    {
        if (!_tabOrderByContext.TryGetValue(requestContext, out var order)) return;
        if (!order.Remove(tabId)) return;
        if (order.Count == 0)
            _tabOrderByContext.Remove(requestContext);
        InvalidateSnapshotNoLock(requestContext);
    }

    private void PruneNoLock()
    {
        var stale = _tabsById.Values
            .Where(entry => entry.Browser == null || entry.Browser.IsDisposed)
            .Select(entry => entry.TabId)
            .ToArray();

        foreach (var tabId in stale)
        {
            if (!_tabsById.Remove(tabId, out var removed)) continue;
            _idsByBrowser.Remove(removed.Browser);
            RemoveFromOrderNoLock(removed.RequestContext, tabId);

            if (_activeTabByContext.TryGetValue(removed.RequestContext, out var activeId) && activeId == tabId)
                _activeTabByContext.Remove(removed.RequestContext);
            InvalidateSnapshotNoLock(removed.RequestContext);
        }
    }

    private void InvalidateSnapshotNoLock(IRequestContext requestContext)
        => _snapshotCache.Remove(requestContext);

    private sealed record BrowserEntry(
        int TabId,
        IChromiumWebBrowserBase Browser,
        IRequestContext RequestContext);
}

/// <summary>
/// Immutable extension-visible view of one Zidimi profile's live web tabs. Every id is a real
/// native CEF browser Identifier, so messaging/scripting/content-script APIs keep using Chromium's
/// normal pipeline for both foreground and background tabs.
/// </summary>
public sealed record ExtensionTabSnapshot(int ActiveTabId, IReadOnlyList<int> TabIds)
{
    public static ExtensionTabSnapshot Empty { get; } = new(0, Array.Empty<int>());
    public bool HasActiveTab => ActiveTabId > 0;
}
