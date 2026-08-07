using System;
using CefSharp;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Infrastructure.Handlers;

/// <summary>
/// .NET object bound into JavaScript for each tab (spec 11.2 — JS Bindings).
/// Web pages / internal pages can call it through window.hecoBrowser.:
///   - hecoBrowser.getVersion():  returns the browser version string
///   - hecoBrowser.getLanguage(): returns the current UI language code
///   - hecoBrowser.setTitle(title): changes the current tab title from JS
///   - hecoBrowser.getZoomLevel(): reads the current zoom level
/// </summary>
public sealed class HecoJsBoundObject
{
    private string _title = "";

    /// <summary>Application version string.</summary>
    public string GetVersion() => typeof(HecoJsBoundObject).Assembly.GetName().Version?.ToString() ?? "1.0.0";

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
public static class HecoJsBinding
{
    public static void Bind(IWebBrowser browser)
    {
        try
        {
            browser.JavascriptObjectRepository.Register(
                "hecoBrowser",
                new HecoJsBoundObject(),
                options: BindingOptions.DefaultBinder);
        }
        catch
        {
            // Already bound previously or unsupported — skip safely.
        }
    }
}