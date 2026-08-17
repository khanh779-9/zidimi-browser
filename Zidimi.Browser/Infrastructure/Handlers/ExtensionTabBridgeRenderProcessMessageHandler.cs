using System.Text.Json;
using CefSharp;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Generic extension-surface bridge for Zidimi's WPF-managed tab strip.
///
/// CEF already uses CefBrowser.Identifier as the extension tabId. This handler only repairs the
/// part CEF cannot infer in an embedded browser: which of those browser ids is the active/current
/// Zidimi tab. All operations after discovery (tabs.sendMessage, scripting.executeScript,
/// tabs.reload, etc.) still call Chromium's native extension APIs and therefore retain normal
/// manifest/host-permission enforcement.
/// </summary>
public sealed class ExtensionTabBridgeRenderProcessMessageHandler : IRenderProcessMessageHandler
{
    private readonly ExtensionTabSnapshot _snapshot;
    private readonly string _bridgeScript;

    public ExtensionTabBridgeRenderProcessMessageHandler(ExtensionTabSnapshot snapshot)
    {
        _snapshot = snapshot ?? ExtensionTabSnapshot.Empty;
        _bridgeScript = BuildBridgeScript(_snapshot);
    }

    public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
    {
        if (!frame.IsMain || string.IsNullOrWhiteSpace(_bridgeScript)) return;

        try
        {
            // Inject before extension frameworks/polyfills capture chrome.tabs methods. The bridge
            // is extension-agnostic and is installed for every Zidimi-hosted extension surface.
            frame.ExecuteJavaScriptAsync(_bridgeScript);
        }
        catch (Exception ex)
        {
            AppLogger.Log("ExtensionRuntime.TabBridge", ex,
                $"Injecting tab bridge. Active={_snapshot.ActiveTabId}; Tabs={_snapshot.TabIds.Count}");
        }
    }

    public void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame) { }

    public void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node) { }

    public void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        JavascriptException exception)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Zidimi Extension Surface JS] {frame.Url}: {exception?.Message}");
    }

    private static string BuildBridgeScript(ExtensionTabSnapshot snapshot)
    {
        var tabIds = snapshot.TabIds.Where(id => id > 0).Distinct().ToArray();
        var idsJson = JsonSerializer.Serialize(tabIds);
        var activeTabId = snapshot.ActiveTabId;

        return $$"""
            (() => {
                const ZIDIMI_ACTIVE_TAB_ID = {{activeTabId}};
                const ZIDIMI_WINDOW_TAB_IDS = {{idsJson}};
                const chromeTabs = globalThis.chrome?.tabs;
                if (!chromeTabs || typeof chromeTabs.query !== 'function' || typeof chromeTabs.get !== 'function') {
                    return;
                }

                // Do not broaden permissions. These are only native CefBrowser.Identifier values;
                // every native API call below still passes through Chromium permission checks.
                globalThis.__zidimiExtensionTabs = Object.freeze({
                    activeTabId: ZIDIMI_ACTIVE_TAB_ID,
                    tabIds: Object.freeze([...ZIDIMI_WINDOW_TAB_IDS])
                });

                const originalGet = chromeTabs.get.bind(chromeTabs);
                const originalQuery = chromeTabs.query.bind(chromeTabs);

                const nativeGet = (tabId) => new Promise((resolve, reject) => {
                    let callbackWasInvoked = false;
                    try {
                        const maybe = originalGet(tabId, (tab) => {
                            callbackWasInvoked = true;
                            const err = globalThis.chrome?.runtime?.lastError;
                            if (err) reject(new Error(err.message || 'tabs.get failed'));
                            else resolve(tab);
                        });
                        if (maybe && typeof maybe.then === 'function') maybe.then(resolve, reject);
                        return;
                    } catch (callbackFormError) {
                        if (callbackWasInvoked) {
                            reject(callbackFormError);
                            return;
                        }
                    }

                    try {
                        const maybe = originalGet(tabId);
                        if (maybe && typeof maybe.then === 'function') maybe.then(resolve, reject);
                        else resolve(maybe);
                    } catch (err) {
                        reject(err);
                    }
                });

                const nativeQuery = (queryInfo) => new Promise((resolve, reject) => {
                    let callbackWasInvoked = false;
                    try {
                        const maybe = originalQuery(queryInfo, (items) => {
                            callbackWasInvoked = true;
                            const err = globalThis.chrome?.runtime?.lastError;
                            if (err) reject(new Error(err.message || 'tabs.query failed'));
                            else resolve(Array.isArray(items) ? items : []);
                        });
                        if (maybe && typeof maybe.then === 'function') maybe.then(resolve, reject);
                        return;
                    } catch (callbackFormError) {
                        if (callbackWasInvoked) {
                            reject(callbackFormError);
                            return;
                        }
                    }

                    try {
                        const maybe = originalQuery(queryInfo);
                        if (maybe && typeof maybe.then === 'function') maybe.then(resolve, reject);
                        else resolve(Array.isArray(maybe) ? maybe : []);
                    } catch (err) {
                        reject(err);
                    }
                });

                const finish = (promise, callback, fallbackValue) => {
                    if (typeof callback === 'function') {
                        promise.then(callback, () => callback(fallbackValue));
                        return undefined;
                    }
                    return promise;
                };

                const allowedTabIds = new Set(ZIDIMI_WINDOW_TAB_IDS);
                const tabIndexById = new Map(
                    ZIDIMI_WINDOW_TAB_IDS.map((id, index) => [id, index]));

                // Chromium owns the real tab object (url/title/status/audible/etc.); Zidimi owns
                // only the WPF strip state. Normalize exactly those host-owned fields so every
                // extension sees the same id/active/index model for foreground and background tabs.
                const normalizeZidimiTab = (tab) => {
                    if (!tab || !allowedTabIds.has(tab.id)) return tab;
                    const index = tabIndexById.get(tab.id);
                    return {
                        ...tab,
                        id: tab.id,
                        active: tab.id === ZIDIMI_ACTIVE_TAB_ID,
                        highlighted: tab.id === ZIDIMI_ACTIVE_TAB_ID,
                        index: Number.isInteger(index) ? index : tab.index
                    };
                };

                const getTab = (id) => id > 0
                    ? nativeGet(id).then(normalizeZidimiTab).catch(() => undefined)
                    : Promise.resolve(undefined);

                const getTabs = (ids) => Promise.all(ids.map(getTab))
                    .then(items => items.filter(Boolean));

                const CURRENT_WINDOW_ID = globalThis.chrome?.windows?.WINDOW_ID_CURRENT ?? -2;
                const isCurrentWindowId = (value) => value === CURRENT_WINDOW_ID || value === -2;

                // Let Chromium apply every query filter it already understands (url/title/status/
                // pinned/audible/etc.). We remove only the WPF-window concepts that Chromium
                // cannot infer for Zidimi, then intersect the native result with real Zidimi ids.
                const makeNativeFilter = (queryInfo) => {
                    const q = { ...(queryInfo || {}) };
                    delete q.active;
                    delete q.currentWindow;
                    delete q.lastFocusedWindow;
                    if (isCurrentWindowId(q.windowId)) delete q.windowId;
                    return q;
                };

                const bridgedQuery = function(queryInfo, callback) {
                    const q = queryInfo || {};
                    const hasWindowId = Object.prototype.hasOwnProperty.call(q, 'windowId');
                    const explicitOtherWindow = hasWindowId && !isCurrentWindowId(q.windowId);

                    // Zidimi currently owns one WPF browser window. Queries for an explicit
                    // different Chromium window are left untouched; every normal/no-window query
                    // is resolved against the complete set of live Zidimi web tabs.
                    if (explicitOtherWindow) {
                        if (typeof callback === 'function') {
                            try { return originalQuery(q, callback); }
                            catch (_) { return finish(nativeQuery(q), callback, []); }
                        }

                        try {
                            const nativeResult = originalQuery(q);
                            if (nativeResult && typeof nativeResult.then === 'function') return nativeResult;
                        } catch (_) { }
                        return nativeQuery(q);
                    }

                    if (ZIDIMI_WINDOW_TAB_IDS.length === 0)
                        return finish(Promise.resolve([]), callback, []);

                    const nativeFilter = makeNativeFilter(q);
                    const applyZidimiState = (items) => {
                        let result = (Array.isArray(items) ? items : [])
                            .filter(tab => allowedTabIds.has(tab?.id))
                            .map(normalizeZidimiTab);

                        // Chromium does not know which WPF tab is selected. Apply only that
                        // missing piece here; inactive tabs remain in the result/runtime.
                        if (q.active === true)
                            result = result.filter(tab => tab?.id === ZIDIMI_ACTIVE_TAB_ID);
                        else if (q.active === false && ZIDIMI_ACTIVE_TAB_ID > 0)
                            result = result.filter(tab => tab?.id !== ZIDIMI_ACTIVE_TAB_ID);

                        // Return in the same visual order as Zidimi's TabStrip, just like a
                        // normal browser window's tabs.query() result.
                        result.sort((a, b) =>
                            (tabIndexById.get(a?.id) ?? Number.MAX_SAFE_INTEGER) -
                            (tabIndexById.get(b?.id) ?? Number.MAX_SAFE_INTEGER));
                        return result;
                    };

                    // For selection/window-only queries do not depend on Chromium having a native
                    // TabStripModel: fetch every real CefBrowser tab directly by its id. When the
                    // extension supplies filters Chromium understands (url/title/status/pinned/...)
                    // keep using native tabs.query and only intersect the result with Zidimi tabs.
                    const baseQuery = Object.keys(nativeFilter).length === 0
                        ? getTabs(ZIDIMI_WINDOW_TAB_IDS)
                        : nativeQuery(nativeFilter);

                    const result = baseQuery
                        .then(applyZidimiState)
                        .catch(() => []);

                    return finish(result, callback, []);
                };

                const bridgedGet = function(tabId, callback) {
                    // A tab from another native Chromium window is not Zidimi-owned; preserve
                    // the runtime's original behavior. For Zidimi tabs normalize active/index.
                    if (!allowedTabIds.has(tabId)) {
                        if (typeof callback === 'function') {
                            try { return originalGet(tabId, callback); }
                            catch (_) { return finish(nativeGet(tabId), callback, undefined); }
                        }

                        try {
                            const nativeResult = originalGet(tabId);
                            if (nativeResult && typeof nativeResult.then === 'function') return nativeResult;
                        } catch (_) { }
                        return nativeGet(tabId);
                    }

                    const result = nativeGet(tabId)
                        .then(normalizeZidimiTab);
                    return finish(result, callback, undefined);
                };

                const replace = (target, name, fn) => {
                    try {
                        target[name] = fn;
                        if (target[name] === fn) return true;
                    } catch (_) { }
                    try {
                        Object.defineProperty(target, name, {
                            configurable: true,
                            enumerable: true,
                            writable: true,
                            value: fn
                        });
                        return target[name] === fn;
                    } catch (_) {
                        return false;
                    }
                };

                const marker = `${ZIDIMI_ACTIVE_TAB_ID}|${ZIDIMI_WINDOW_TAB_IDS.join(',')}`;
                if (chromeTabs.__zidimiTabBridgeMarker !== marker) {
                    replace(chromeTabs, 'get', bridgedGet);
                    replace(chromeTabs, 'query', bridgedQuery);
                    try {
                        Object.defineProperty(chromeTabs, '__zidimiTabBridgeMarker', {
                            configurable: true,
                            value: marker
                        });
                    } catch (_) { }
                }
            })();
            """;
    }
}
