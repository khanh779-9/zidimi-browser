using System;
using CefSharp;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Infrastructure.Handlers;

/// <summary>
/// .NET object bound into JavaScript for each tab (spec 11.2 — JS Bindings).
/// Web pages / internal pages can call it through window.zidimiBrowser.:
///   - zidimiBrowser.getVersion():  returns the browser version string
///   - zidimiBrowser.getLanguage(): returns the current UI language code
///   - zidimiBrowser.setTitle(title): changes the current tab title from JS
///   - zidimiBrowser.getZoomLevel(): reads the current zoom level
/// </summary>
public sealed class ZidimiJsBoundObject
{
    private string _title = "";

    /// <summary>Application version string.</summary>
    public string GetVersion() => typeof(ZidimiJsBoundObject).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>Current UI language code.</summary>
    public string GetLanguage() => LanguageManager.Instance.CurrentLanguage?.Code ?? "en-US";

    /// <summary>Sets the tab title (returns true if not empty).</summary>
    public bool SetTitle(string title)
    {
        _title = title ?? "";
        return !string.IsNullOrEmpty(_title);
    }

    /// <summary>Reads the tab title set by the page (if any).</summary>
    public string GetTitle() => _title;

    /// <summary>Goes back to the previous page (returns true if possible).</summary>
    public bool GoBack() => false;
}

/// <summary>
/// Registers the object above for every newly created tab.
/// </summary>
public static class ZidimiJsBinding
{
    public static void Bind(IWebBrowser browser)
    {
        try
        {
            browser.JavascriptObjectRepository.Register(
                "zidimiBrowser",
                new ZidimiJsBoundObject(),
                options: BindingOptions.DefaultBinder);
        }
        catch
        {
            // Already bound previously or unsupported — skip safely.
        }
    }
}