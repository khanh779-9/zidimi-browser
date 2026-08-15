using System;
using System.Collections.Generic;
using System.IO;
using CefSharp;
using CefSharp.Wpf;
using Zidimi.Browser.Infrastructure.Handlers;
using Zidimi.Browser.Models;

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
        var g = AppSettings.Global;
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cachePath = UserDataPaths.SharedCacheDir;
        Directory.CreateDirectory(cachePath);

        var settings = new CefSettings
        {
            CachePath = cachePath,
            LogSeverity = g.CefLogEnabled ? LogSeverity.Info : LogSeverity.Error,
            // Capture stack frames so RenderProcessMessageHandler.OnUncaughtException
            // (logged in AppLogger) receives a useful stack instead of nothing.
            UncaughtExceptionStackSize = 10,
        };

        if (g.CefLogEnabled)
        {
            settings.LogFile = Path.Combine(UserDataPaths.Root, "cef-debug.log");
        }

        // ------------------------------------------------------------------
        // Locale & language
        // ------------------------------------------------------------------
        var langCode = NormalizeLocale(g.DisplayLanguage);
        if (!string.IsNullOrWhiteSpace(langCode))
        {
            settings.Locale = langCode;
            settings.AcceptLanguageList = BuildAcceptLanguageList(langCode);
        }

        // ------------------------------------------------------------------
        // User-Agent
        // ------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(g.UserAgentOverride))
        {
            settings.UserAgent = g.UserAgentOverride;
        }

        // ------------------------------------------------------------------
        // Custom scheme "zidimi://" (spec 11.2 — ISchemeHandlerFactory)
        // ------------------------------------------------------------------
        settings.RegisterScheme(new CefCustomScheme
        {
            SchemeName = "zidimi",
            IsStandard = true,
            IsSecure = true,
            IsCorsEnabled = true,
            IsLocal = true,
            SchemeHandlerFactory = new ZidimiSchemeHandlerFactory(),
        });

        // ------------------------------------------------------------------
        // GPU & rendering
        // ------------------------------------------------------------------
        if (!g.EnableGpu)
        {
            args["disable-gpu"] = "1";
            args["disable-gpu-compositing"] = "1";
        }

        // ------------------------------------------------------------------
        // Video enhancement
        // ------------------------------------------------------------------
        if (g.EnhanceVideos)
        {
            AppendCommaValue(args, "enable-features", "HardwareSecureDecryption,Vulkan");
        }

        // ------------------------------------------------------------------
        // Proxy
        // ------------------------------------------------------------------
        if (!g.UseSystemProxy)
        {
            args["no-proxy-server"] = "1";
        }

        // ------------------------------------------------------------------
        // Stability mitigations
        // ------------------------------------------------------------------
        // Chromium won't tear down the whole browser when the GPU process
        // crashes repeatedly. Also mirror CefSharp's own Windows defaults when
        // re-declaring disable-features, so nothing important gets re-enabled.
        if (g.StableRendering)
        {
            args["disable-gpu-process-crash-limit"] = "1";
            AppendCommaValue(args, "disable-features",
                "BackForwardCache,CalculateNativeWinOcclusion,WinUseBrowserSpellChecker");
        }

        // We do our own crash logging (AppLogger) — Chromium's crash reporter
        // would only spawn an idle process and swallow dumps we never read.
        args["disable-breakpad"] = "1";
        // Embedded CEF has no component updater to talk to.
        args["disable-component-update"] = "1";

        // ------------------------------------------------------------------
        // Background throttling
        // ------------------------------------------------------------------
        if (g.DisableBackgroundThrottling)
        {
            args["disable-backgrounding-occluded-windows"] = "1";
            args["disable-renderer-backgrounding"] = "1";
            args["disable-renderer-throttling"] = "1";
            args["disable-background-timer-throttling"] = "1";
        }

        // ------------------------------------------------------------------
        // Sandbox
        // ------------------------------------------------------------------
        if (g.DisableSandbox)
        {
            args["no-sandbox"] = "1";
        }

        // ------------------------------------------------------------------
        // Renderer processes & JS heap
        // ------------------------------------------------------------------
        if (g.RendererProcessLimit > 0)
        {
            args["renderer-process-limit"] = g.RendererProcessLimit.ToString();
        }

        if (g.MaxJsHeapSizeMb > 0)
        {
            settings.JavascriptFlags = $"--max-old-space-size={g.MaxJsHeapSizeMb}";
        }

        // ------------------------------------------------------------------
        // DevTools debugging
        // ------------------------------------------------------------------
        if (g.RemoteDebuggingPort is >= 1024 and <= 65535)
        {
            settings.RemoteDebuggingPort = g.RemoteDebuggingPort;
        }

        foreach (var kv in args)
        {
            settings.CefCommandLineArgs[kv.Key] = kv.Value;
        }

        // Subprocesses exit as soon as the browser process dies, so a crash
        // never leaves orphaned renderer/GPU processes behind.
        CefSharpSettings.SubprocessExitIfParentProcessClosed = true;

        return settings;
    }

    /// <summary>Append a value to a comma-separated switch, keeping any existing entries.</summary>
    private static void AppendCommaValue(IDictionary<string, string> args, string key, string value)
    {
        if (args.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            args[key] = existing + "," + value;
        else
            args[key] = value;
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

    private static string NormalizeLocale(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? "en-US" : code;
    }
}