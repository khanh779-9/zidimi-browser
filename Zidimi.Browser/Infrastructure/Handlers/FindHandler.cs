using CefSharp;
using CefSharp.Structs;

namespace Zidimi.Browser.Infrastructure.Handlers;

public class FindHandler : IFindHandler
{
    /// <summary>
    /// Called when CEF reports an in-page search result (match count, current position).
    /// </summary>
    public event Action<int, int, bool>? FindResult;

    public void OnFindResult(IWebBrowser chromiumWebBrowser, IBrowser browser, int identifier,
        int count, Rect selectionRect, int activeMatchOrdinal, bool finalUpdate)
    {
        // count == 0 -> no matches found.
        FindResult?.Invoke(count, activeMatchOrdinal, finalUpdate);
    }
}