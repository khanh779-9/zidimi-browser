namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Signals when CEF's global RequestContext is initialized. Preference access is intentionally kept
/// out of this lifecycle callback; AppSettings reads Chromium's already-loaded values through the
/// managed CefSharp global IRequestContext after this signal.
/// </summary>
public sealed class CefBootstrapBrowserProcessHandler : CefSharp.Handler.BrowserProcessHandler
{
    private readonly TaskCompletionSource<bool> _contextReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ContextReady => _contextReady.Task;

    protected override void OnContextInitialized()
    {
        base.OnContextInitialized();
        _contextReady.TrySetResult(true);
    }
}
