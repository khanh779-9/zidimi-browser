using System;
using System.Text;
using CefSharp;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// Handles page loading events (spec 11.2 — ILoadHandler).
/// - OnLoadError: when main frame navigation fails, shows a custom error page (Zidimi)
///   instead of Chromium's default error page. Ignores Aborted errors (caused by the user cancelling).
/// - OnLoadingStateChange/OnFrameLoadStart/End: report back through events so the UI can update.
/// </summary>
public sealed class ZidimiLoadHandler : CefSharp.Handler.LoadHandler
{
    /// <summary>Raises an event when a tab's loading state changes (wired to the browser's LoadingStateChanged).</summary>
    public event Action<LoadingStateChangedEventArgs>? LoadingStateChanged;

    protected override void OnLoadingStateChange(IWebBrowser chromiumWebBrowser, LoadingStateChangedEventArgs loadingStateChangedArgs)
    {
        LoadingStateChanged?.Invoke(loadingStateChangedArgs);
    }

    protected override void OnLoadError(IWebBrowser chromiumWebBrowser, LoadErrorEventArgs loadErrorArgs)
    {
        // Only handle the main frame. Ignore ERR_ABORTED (user cancelled / replaced navigation).
        if (loadErrorArgs.Frame.IsMain == false) return;
        if (loadErrorArgs.ErrorCode == CefErrorCode.Aborted) return;

        var failedUrl = loadErrorArgs.FailedUrl ?? "";
        if (string.IsNullOrEmpty(failedUrl)) return;

        var html = BuildErrorPage(loadErrorArgs.ErrorCode, loadErrorArgs.ErrorText, failedUrl);
        try
        {
            chromiumWebBrowser.LoadHtml(html, failedUrl);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Navigation", ex, $"Rendering load-error page for {failedUrl}.");
        }
    }

    private static string BuildErrorPage(CefErrorCode code, string errorText, string failedUrl)
    {
        var l = LanguageManager.Instance;
        var title = l["LoadError_Title"];
        var desc = string.IsNullOrWhiteSpace(errorText) ? l["LoadError_Desc"] : errorText;
        var retry = l["LoadError_Retry"];
        var goHome = l["LoadError_GoHome"];
        var homeUrl = HtmlAttr(Models.AppSettings.Profile.HomePageUrl);
        var codeText = code.ToString();
        var urlDisplay = HtmlEnc(failedUrl);

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>"
            + HtmlEnc(title)
            + "</title><style>"
            + InternalPageTheme.CssVariables
            + "*{box-sizing:border-box}html,body{height:100%;margin:0}body{font-family:'Segoe UI',system-ui,Arial,sans-serif;"
            + "display:flex;align-items:center;justify-content:center;background:var(--bg);color:var(--text);padding:24px}"
            + ".card{max-width:440px;text-align:center;background:var(--surface);border:1px solid var(--border);border-radius:16px;"
            + "padding:40px 36px;box-shadow:0 20px 50px var(--shadow)}"
            + ".icon{width:64px;height:64px;border-radius:50%;background:var(--danger-surface);display:flex;align-items:center;justify-content:center;margin:0 auto 22px}"
            + ".icon svg{width:34px;height:34px;stroke:var(--danger);fill:none;stroke-width:2}"
            + "h1{font-size:22px;font-weight:650;margin:0 0 8px}p{font-size:14px;line-height:1.6;color:var(--text-secondary);margin:0 0 8px}"
            + ".url{font-size:12.5px;color:var(--text-muted);word-break:break-all;margin-bottom:14px}"
            + ".code{display:inline-block;font-size:11.5px;color:var(--text-muted);background:var(--code-surface);border-radius:20px;padding:4px 12px;margin-bottom:22px}"
            + ".actions{display:flex;gap:10px;justify-content:center;flex-wrap:wrap}"
            + "button{border-radius:9px;padding:11px 18px;font-size:14px;cursor:pointer;font-family:inherit}"
            + "button.retry{border:1px solid transparent;background:var(--accent);color:var(--on-accent);font-weight:600}"
            + "button.retry:hover{background:var(--accent-hover)}"
            + "button.home{background:transparent;color:var(--text-secondary);border:1px solid var(--border)}"
            + "button.home:hover{background:var(--surface-hover)}"
            + "</style></head><body><div class=\"card\">"
            + "<div class=\"icon\"><svg viewBox=\"0 0 24 24\"><path d=\"M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z\"/><line x1=\"12\" y1=\"9\" x2=\"12\" y2=\"13\"/><circle cx=\"12\" cy=\"17\" r=\"1\"/></svg></div>"
            + "<h1>" + HtmlEnc(title) + "</h1>"
            + "<p>" + HtmlEnc(desc) + "</p>"
            + "<p class=\"url\">" + urlDisplay + "</p>"
            + "<div class=\"code\">" + HtmlEnc(codeText) + "</div>"
            + "<div class=\"actions\">"
            + "<button class=\"retry\" onclick=\"location.reload()\">" + HtmlEnc(retry) + "</button>"
            + "<button class=\"home\" onclick=\"location.href='" + homeUrl + "'\">" + HtmlEnc(goHome) + "</button>"
            + "</div></div></body></html>";
    }

    private static string HtmlEnc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    private static string HtmlAttr(string s) => HtmlEnc(s).Replace("'", "&#39;", StringComparison.Ordinal);
}