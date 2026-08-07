using System;
using System.Windows;
using System.Windows.Media;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Manages the Dark/Light/System themes for Heco Browser.
/// How it works: each theme has its own ResourceDictionary (Themes/Colors.xaml holds the
/// fixed colors/brushes; DarkTheme.xaml &amp; LightTheme.xaml hold the brushes that change by theme).
/// The theme is switched by swapping the dictionary in Application.Current.Resources —
/// the whole UI uses DynamicResource so it refreshes automatically, including hover/press states.
/// </summary>
public static class ThemeManager
{
    public enum AppTheme { Dark, Light, System }

    public static event Action<AppTheme>? ThemeChanged;

    private static AppTheme _current = AppTheme.Dark;
    public static AppTheme Current => _current;

    // 2 dictionaries holding the theme-dependent brushes (same keys, different values).
    private static ResourceDictionary? _darkDict;
    private static ResourceDictionary? _lightDict;

/// <summary>
/// Applies a theme from the stable keys "system" / "dark" / "light".
/// Also supports legacy values that are localized labels (e.g. "System", "Dark", "Light")
/// so old versions can be migrated.
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

    /// <summary>Normalizes the Theme value in AppSettings to a stable key (system/dark/light).</summary>
    public static string NormalizeThemeKey(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName)) return "system";
        var t = themeName.Trim();
        if (t is "system" or "dark" or "light") return t;

        // Legacy: labels in the currently displayed language.
        var lm = LanguageManager.Instance;
        if (t == lm["Pref_ThemeDark"] || t == "Dark" || t == "Tối" || t == "Scuro" || t == "Sombre" || t == "Темная" || t == "深色" || t == "dunkel")
            return "dark";
        if (t == lm["Pref_ThemeLight"] || t == "Light" || t == "Sáng" || t == "Chiaro" || t == "Clair" || t == "Светлая" || t == "浅色" || t == "hell")
            return "light";
        return "system";
    }

    public static void Apply(AppTheme theme)
    {
        EnsureLoaded();
        _current = theme;
        var effective = theme == AppTheme.System
            ? DetectSystemTheme() // Light or Dark
            : (theme == AppTheme.Light ? AppTheme.Light : AppTheme.Dark);

        SwapDictionary(effective == AppTheme.Light ? _lightDict : _darkDict);
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
            if (ReferenceEquals(d, _darkDict) || ReferenceEquals(d, _lightDict)) { active = d; insertAt = i; }
        }

        if (ReferenceEquals(active, target)) return; // correct, no change needed

        res.MergedDictionaries.Remove(active); // active may be null → Remove(null) is harmless
        if (insertAt < 0) insertAt = res.MergedDictionaries.Count;
        res.MergedDictionaries.Insert(insertAt, target);
    }

    /// <summary>Loads and initializes the two theme dictionaries. Called once when the app starts (App.OnStartup).</summary>
    public static void EnsureLoaded()
    {
        if (_darkDict != null && _lightDict != null) return;
        _darkDict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Heco.Browser;component/Themes/DarkTheme.xaml", UriKind.Absolute)
        };
        _lightDict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Heco.Browser;component/Themes/LightTheme.xaml", UriKind.Absolute)
        };
    }

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
        catch { }
        return AppTheme.Dark;
    }
}