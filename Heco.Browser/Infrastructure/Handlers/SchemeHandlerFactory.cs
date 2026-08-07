using System;
using System.IO;
using System.Text;
using CefSharp;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// Custom scheme "heco://" (spec 11.2 — ISchemeHandlerFactory).
/// Serves lightweight internal pages, isolated from the internet:
///   - heco://welcome : internal welcome / about page
///   - heco://newtab  : new tab page (backed by JSON if present, simple)
/// Any other heco:// URL returns a 404 with a compact HTML page.
/// </summary>
public sealed class HecoSchemeHandlerFactory : ISchemeHandlerFactory
{
    public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
    {
        var url = request.Url ?? "";
        var path = new Uri(url).AbsolutePath ?? "";

        var html = path switch
        {
            "/welcome" => WelcomePage(),
            "/newtab" => NewTabPage(),
            _ => NotFoundPage(path),
        };

        var bytes = Encoding.UTF8.GetBytes(html);
        var stream = new MemoryStream(bytes);
        return new ResourceHandler("text/html", stream, autoDisposeStream: true, charset: "utf-8");
    }

    private static string WelcomePage()
    {
        var l = LanguageManager.Instance;
        return MinimalPage(
            l["Scheme_WelcomeTitle"],
            l["Scheme_WelcomeBody"]);
    }

    private static string NewTabPage()
    {
        var l = LanguageManager.Instance;
        return MinimalPage(
            l["Scheme_NewTabTitle"],
            l["Scheme_NewTabBody"]);
    }

    private static string NotFoundPage(string path)
    {
        var l = LanguageManager.Instance;
        return MinimalPage(
            l["Scheme_NotFoundTitle"],
            string.Format(l["Scheme_NotFoundBody"], path));
    }

    private static string MinimalPage(string title, string body)
    {
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>"
            + System.Net.WebUtility.HtmlEncode(title)
            + "</title><style>"
            + "html,body{height:100%;margin:0}body{font-family:'Segoe UI',system-ui,sans-serif;display:flex;align-items:center;"
            + "justify-content:center;background:#f6f7fb;color:#1f2430;padding:24px}"
            + ".card{max-width:420px;text-align:center;background:#fff;border:1px solid #e4e6ef;border-radius:16px;padding:40px 34px;"
            + "box-shadow:0 20px 50px rgba(20,30,60,.08)}"
            + ".logo{width:56px;height:56px;border-radius:14px;background:#7c5cfc;display:flex;align-items:center;justify-content:center;margin:0 auto 18px}"
            + ".logo span{color:#fff;font-size:28px;font-weight:700}"
            + "h1{font-size:22px;font-weight:650;margin:0 0 10px}p{font-size:14px;line-height:1.6;color:#5a6275;margin:0}"
            + "</style></head><body><div class=\"card\"><div class=\"logo\"><span>H</span></div>"
            + "<h1>" + System.Net.WebUtility.HtmlEncode(title) + "</h1><p>" + System.Net.WebUtility.HtmlEncode(body) + "</p></div></body></html>";
    }
}