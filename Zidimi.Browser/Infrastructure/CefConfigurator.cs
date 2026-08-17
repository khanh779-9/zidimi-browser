using System;
using System.Collections.Generic;
using System.IO;
using CefSharp;
using CefSharp.Wpf;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Builds the global CEF <see cref="CefSettings"/> from user settings.
/// Every Chromium tuning decision lives here, organized by concern, so the
/// initialization code in App.xaml.cs stays readable and switches stay consistent.
/// </summary>
public static class CefConfigurator
{
    public static CefSettings BuildSettings()
    {
        // Real Chromium extension support (MV2/MV3 action popups, background/service workers,
        // content scripts) requires Chrome runtime and a windowed host. BrowserView uses
        // CefSharp.Wpf.HwndHost, which is explicitly supported by CefSharp for this mode.
        CefSharpSettings.RuntimeStyle = CefRuntimeStyle.Chrome;
        // Zidimi owns the shutdown order in App.OnExit; avoid the HwndHost static shutdown hook
        // racing our RequestContext/profile cleanup.
        CefSharpSettings.ShutdownOnExit = false;
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cachePath = UserDataPaths.RootCacheDir;

        var settings = new CefSettings
        {
            // Chromium installation data lives in RootCachePath. The global CEF RequestContext
            // is the Default disk-backed profile; non-default profiles use child RequestContexts.
            // This prevents two contexts from competing for the same Default Preferences/Cookies files.
            RootCachePath = cachePath,
            CachePath = UserDataPaths.ProfileDir(UserDataPaths.DefaultProfileId),
            // CefSharp 150 uses the Chrome bootstrap where user-preference persistence is
            // always owned by Chromium for a disk-backed CachePath. There is intentionally
            // no manual Preferences/Local State file writer in Zidimi.
            LogSeverity = LogSeverity.Disable,
            // Even disabled CEF builds may touch a log file. Pin that file to Chromium's native
            // User Data name so no debug.log appears beside the executable.
            LogFile = UserDataPaths.ChromeDebugLogFile,
            // Capture stack frames so RenderProcessMessageHandler.OnUncaughtException
            // (logged in AppLogger) receives a useful stack instead of nothing.
            UncaughtExceptionStackSize = 10,
        };


        // ------------------------------------------------------------------
        // Locale & language
        // ------------------------------------------------------------------
        // Chromium resource packs don't always use the full .NET/UI culture name.
        // Example: Zidimi UI uses "vi-VN", while CEF ships locales\vi.pak.
        // Keep the full culture for HTTP Accept-Language, but resolve settings.Locale
        // against the actual *.pak files deployed by the CefSharp runtime package.
        // CEF must choose a resource pack before profile preferences can be read. Zidimi augments
        // these same Chromium locales/*.pak DataPacks before Cef.Initialize; there is no .lng tree.
        var bootstrapLanguage = NormalizeLocale(System.Globalization.CultureInfo.CurrentUICulture.Name);
        settings.Locale = ResolveCefResourceLocale(bootstrapLanguage);
        settings.AcceptLanguageList = BuildAcceptLanguageList(bootstrapLanguage);

        // GPU policy is not exposed as a managed mutable global preference in CefSharp 150.
        // Leave Chromium defaults untouched instead of faking persistence with command-line switches.

        // ------------------------------------------------------------------
        // Proxy
        // ------------------------------------------------------------------
        // Intentionally no proxy command-line switch here. CefSharp documents that proxy switches
        // make the RequestContext proxy preference read-only. Zidimi applies the standard Chromium
        // `proxy` preference (system/direct) to each profile context so changes can take effect
        // immediately and persist in Chromium-owned Preferences.

        // Chromium's sandbox, GPU crash policy, renderer scheduling, process limits and normal
        // runtime defaults are intentionally left untouched. Zidimi only supplies switches that
        // are required to bridge shell functionality that CefSharp exposes explicitly.

        // ------------------------------------------------------------------
        // Extensions (Chrome runtime)
        // ------------------------------------------------------------------
        // Chromium owns extension installation and persistence. No --load-extension list and no
        // Zidimi package directory are built at startup. This flag only enables Chromium's in-process
        // DevTools Extensions.triggerAction used by Zidimi's custom toolbar for actions without a popup.
        args["enable-unsafe-extension-debugging"] = "1";

        // Chrome Runtime owns its feature lists. Defensive removal here prevents
        // a future setting from accidentally re-introducing the native startup crash.
        args.Remove("enable-features");
        args.Remove("disable-features");

        foreach (var kv in args)
        {
            settings.CefCommandLineArgs[kv.Key] = kv.Value;
        }

        // Subprocesses exit as soon as the browser process dies, so a crash
        // never leaves orphaned renderer/GPU processes behind.
        CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

        return settings;
    }

    /// <summary>"vi-VN" → "vi-VN,vi"; "en-US" → "en-US,en".</summary>
    private static string BuildAcceptLanguageList(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return "en-US,en";
        var dash = locale.IndexOf('-');
        if (dash > 0)
        {
            var lang = locale.Substring(0, dash);
            return $"{locale},{lang}";
        }
        return locale;
    }

    /// <summary>
    /// Resolves a UI culture (for example "vi-VN") to the CEF resource-pack locale
    /// that is actually present beside the application (for example "vi").
    /// CefSharp's dependency checker requires locales\{settings.Locale}.pak to exist.
    /// </summary>
    private static string ResolveCefResourceLocale(string requestedLocale)
    {
        var normalized = NormalizeLocale(requestedLocale).Replace('_', '-');
        var localesDirectory = Path.Combine(AppContext.BaseDirectory, "locales");

        // Prefer the exact Chromium pack when one exists (en-US, en-GB, pt-BR,
        // zh-CN, zh-TW, ...). This also keeps the code future-proof if CEF adds
        // additional region-specific packs.
        if (HasLocalePack(localesDirectory, normalized))
            return normalized;

        // Most Chromium locale packs use the base language only (vi, fr, de, it,
        // ru, ...), even when Zidimi's WPF language file uses a regional culture.
        var separatorIndex = normalized.IndexOf('-');
        if (separatorIndex > 0)
        {
            var baseLanguage = normalized[..separatorIndex];
            if (HasLocalePack(localesDirectory, baseLanguage))
                return baseLanguage;
        }

        // A healthy CefSharp runtime always contains en-US.pak. Falling back here
        // is safer than passing an unsupported culture to DependencyChecker and
        // failing startup merely because the WPF language name is more specific.
        if (HasLocalePack(localesDirectory, "en-US"))
            return "en-US";

        // If the output hasn't been populated yet (e.g. design-time/static analysis),
        // retain a sensible Chromium-style locale. At real startup the dependency
        // checker will still report a genuinely incomplete CefSharp deployment.
        if (separatorIndex > 0)
            return normalized[..separatorIndex];

        return normalized;
    }

    private static bool HasLocalePack(string localesDirectory, string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return false;

        return File.Exists(Path.Combine(localesDirectory, $"{locale}.pak"));
    }

    private static string NormalizeLocale(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? "en-US" : code.Trim();
    }
}