using System;
using System.Windows;
using System.Windows.Media;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Manages the Dark/Light/System themes for Zidimi Browser.
/// How it works: Themes/Colors.xaml contains only theme-independent tokens (status colors,
/// corner radii). DarkTheme.xaml / LightTheme.xaml expose the same complete set of
/// theme-sensitive brushes (surfaces, text, accent, hover/pressed, shadows).
/// Switching theme swaps exactly one dictionary in Application.Current.Resources. Theme-aware
/// XAML uses DynamicResource; code-built persistent surfaces listen to ThemeChanged and refresh.
/// </summary>
public static class ThemeManager
{
    public enum AppTheme { Dark, Light, System }

    public static event Action<AppTheme>? ThemeChanged;

    private static AppTheme _current = AppTheme.System;
    public static AppTheme Current => _current;

    /// <summary>The concrete palette currently in use after resolving the System option.</summary>
    public static AppTheme EffectiveCurrent => _current == AppTheme.System ? DetectSystemTheme() : _current;

    // ResourceDictionaries holding theme-dependent brushes.
    private static ResourceDictionary? _darkDict;
    private static ResourceDictionary? _lightDict;

    /// <summary>
    /// Applies Chromium's native browser color-scheme choices: system / dark / light.
    /// Legacy "classic" values are normalized to system.
    /// </summary>
    public static void ApplyFromSettings(string? themeName)
    {
        Apply(ToAppTheme(NormalizeThemeKey(themeName)));
    }

    private static AppTheme ToAppTheme(string key) => key switch
    {
        "dark" => AppTheme.Dark,
        "light" => AppTheme.Light,
        _ => AppTheme.System,
    };

    /// <summary>Normalizes to Chromium BrowserColorScheme keys: system / dark / light.</summary>
    public static string NormalizeThemeKey(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName)) return "system";
        var t = themeName.Trim();
        if (t is "system" or "dark" or "light") return t;
        if (t == "classic") return "system";

        // Localized labels check
        var lm = LanguageManager.Instance;
        if (t == lm["Pref_ThemeClassic"] || t == "Classic" || t == "Cổ điển" || t == "Klassisch" || t == "Classique" || t == "Classico" || t == "Классическая" || t == "经典")
            return "system";
        if (t == lm["Pref_ThemeDark"] || t == "Dark" || t == "Tối" || t == "Scuro" || t == "Sombre" || t == "Темная" || t == "深色" || t == "dunkel")
            return "dark";
        if (t == lm["Pref_ThemeLight"] || t == "Light" || t == "Sáng" || t == "Chiaro" || t == "Clair" || t == "Светлая" || t == "浅色" || t == "hell")
            return "light";
        return "system";
    }

    public static void Apply(AppTheme theme)
    {
        _current = theme;
        var effective = theme == AppTheme.System
            ? DetectSystemTheme()
            : theme;

        var targetDict = GetOrLoad(effective);
        SwapDictionary(targetDict);
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>Swaps the active theme dictionary in the application resources.</summary>
    private static void SwapDictionary(ResourceDictionary? target)
    {
        if (target == null) return;
        var res = Application.Current?.Resources;
        if (res?.MergedDictionaries == null) return;

        int insertAt = -1;
        ResourceDictionary? active = null;
        for (int i = 0; i < res.MergedDictionaries.Count; i++)
        {
            var d = res.MergedDictionaries[i];
            if (ReferenceEquals(d, _darkDict) || ReferenceEquals(d, _lightDict))
            {
                active = d;
                insertAt = i;
            }
        }

        if (ReferenceEquals(active, target)) return; // correct, no change needed

        res.MergedDictionaries.Remove(active); // active may be null → Remove(null) is harmless
        if (insertAt < 0) insertAt = res.MergedDictionaries.Count;
        res.MergedDictionaries.Insert(insertAt, target);
    }

    /// <summary>
    /// Compatibility hook for callers that want the current theme resources ready. Only the
    /// effective theme is loaded; the other dictionaries stay lazy until the user switches.
    /// </summary>
    public static void EnsureLoaded()
        => _ = GetOrLoad(_current == AppTheme.System ? DetectSystemTheme() : _current);

    private static ResourceDictionary GetOrLoad(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => _lightDict ??= LoadDictionary("LightTheme.xaml"),
            _ => _darkDict ??= LoadDictionary("DarkTheme.xaml"),
        };
    }

    private static ResourceDictionary LoadDictionary(string fileName)
        => new()
        {
            Source = new Uri($"pack://application:,,,/Zidimi.Browser;component/Themes/{fileName}", UriKind.Absolute)
        };

    public static AppTheme DetectSystemTheme()
    {
        // Read the Windows registry to determine the system theme.
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v)
                return v == 1 ? AppTheme.Light : AppTheme.Dark;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Theme", ex, "Reading Windows theme preference; falling back to dark.");
        }
        return AppTheme.Dark;
    }
}