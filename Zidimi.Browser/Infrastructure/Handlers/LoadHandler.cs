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
        catch
        {
            // Failed to show the custom error page — fall back to the default.
        }
    }

    private static string BuildErrorPage(CefErrorCode code, string errorText, string failedUrl)
    {
        var l = LanguageManager.Instance;
        var title = l["LoadError_Title"];
        var desc = string.IsNullOrWhiteSpace(errorText) ? l["LoadError_Desc"] : errorText;
        var retry = l["LoadError_Retry"];
        var goHome = l["LoadError_GoHome"];
        var codeText = code.ToString();
        var urlDisplay = HtmlEnc(failedUrl);

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>"
            + HtmlEnc(title)
            + "</title><style>"
            + "html,body{height:100%;margin:0}body{font-family:'Segoe UI',system-ui,Arial,sans-serif;"
            + "display:flex;align-items:center;justify-content:center;background:#f6f7fb;color:#1f2430;padding:24px}"
            + ".card{max-width:440px;text-align:center;background:#fff;border:1px solid #e4e6ef;border-radius:16px;"
            + "padding:40px 36px;box-shadow:0 20px 50px rgba(20,30,60,.08)}"
            + ".icon{width:64px;height:64px;border-radius:50%;background:#fdeceb;display:flex;align-items:center;justify-content:center;margin:0 auto 22px}"
            + ".icon svg{width:34px;height:34px;stroke:#e5484c;fill:none;stroke-width:2}"
            + "h1{font-size:22px;font-weight:650;margin:0 0 8px}p{font-size:14px;line-height:1.6;color:#5a6275;margin:0 0 8px}"
            + ".url{font-size:12.5px;color:#9aa1b4;word-break:break-all;margin-bottom:14px}"
            + ".code{display:inline-block;font-size:11.5px;color:#8a92a6;background:#f2f4fa;border-radius:20px;padding:4px 12px;margin-bottom:22px}"
            + ".actions{display:flex;gap:10px;justify-content:center}"
            + "button{border:0;border-radius:9px;padding:11px 18px;font-size:14px;cursor:pointer}"
            + "button.retry{background:#7c5cfc;color:#fff;font-weight:600}"
            + "button.retry:hover{background:#6c4ce0}"
            + "button.home{background:transparent;color:#4a5165;border:1px solid #dce0eb}"
            + "button.home:hover{background:#f3f5fa}"
            + "</style></head><body><div class=\"card\">"
            + "<div class=\"icon\"><svg viewBox=\"0 0 24 24\"><path d=\"M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z\"/><line x1=\"12\" y1=\"9\" x2=\"12\" y2=\"13\"/><circle cx=\"12\" cy=\"17\" r=\"1\"/></svg></div>"
            + "<h1>" + HtmlEnc(title) + "</h1>"
            + "<p>" + HtmlEnc(desc) + "</p>"
            + "<p class=\"url\">" + urlDisplay + "</p>"
            + "<div class=\"code\">" + HtmlEnc(codeText) + "</div>"
            + "<div class=\"actions\">"
            + "<button class=\"retry\" onclick=\"location.reload()\">" + HtmlEnc(retry) + "</button>"
            + "<button class=\"home\" onclick=\"location.href='about:blank'\">" + HtmlEnc(goHome) + "</button>"
            + "</div></div></body></html>";
    }

    private static string HtmlEnc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
}