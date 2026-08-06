using System;
using System.Windows;
using System.Windows.Media;

namespace Heco.Browser.Infrastructure;

/// <summary>
/// Quản lý theme Sáng/Tối/Hệ thống cho Heco Browser.
/// Cách hoạt động: mỗi theme có một ResourceDictionary riêng (Themes/Colors.xaml chứa
/// màu/brush cố định; DarkTheme.xaml & LightTheme.xaml chứa brush đổi theo theme).
/// Theme được đổi bằng cách swap dictionary trong Application.Current.Resources —
/// toàn UI dùng DynamicResource nên tự refresh, kể cả trạng thái hover/press.
/// </summary>
public static class ThemeManager
{
    public enum AppTheme { Dark, Light, System }

    public static event Action<AppTheme>? ThemeChanged;

    private static AppTheme _current = AppTheme.Dark;
    public static AppTheme Current => _current;

    // 2 dictionary chứa brush đổi theo theme (khai báo cùng key, khác giá trị).
    private static ResourceDictionary? _darkDict;
    private static ResourceDictionary? _lightDict;

    /// <summary>Áp dụng theme theo chuỗi "Tối" / "Sáng" / "Hệ thống" (giống AppSettings.Theme).</summary>
    public static void ApplyFromSettings(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName) || themeName == LanguageManager.Instance["Pref_System"])
            Apply(AppTheme.System);
        else if (themeName == LanguageManager.Instance["Pref_ThemeDark"])
            Apply(AppTheme.Dark);
        else if (themeName == LanguageManager.Instance["Pref_ThemeLight"])
            Apply(AppTheme.Light);
        else
            Apply(AppTheme.System);
    }

    public static void Apply(AppTheme theme)
    {
        EnsureLoaded();
        _current = theme;
        var effective = theme == AppTheme.System
            ? DetectSystemTheme() // Light hoặc Dark
            : (theme == AppTheme.Light ? AppTheme.Light : AppTheme.Dark);

        SwapDictionary(effective == AppTheme.Light ? _lightDict : _darkDict);
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>Swap dictionary theme đang active trong Application resources.</summary>
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

        if (ReferenceEquals(active, target)) return; // đã đúng theme, không cần đổi

        res.MergedDictionaries.Remove(active); // active có thể null → Remove(null) bỏ qua
        if (insertAt < 0) insertAt = res.MergedDictionaries.Count;
        res.MergedDictionaries.Insert(insertAt, target);
    }

    /// <summary>Tải và khởi tạo 2 dictionary theme. Gọi 1 lần khi app bắt đầu (App.OnStartup).</summary>
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
        // Đọc registry Windows để biết theme của hệ thống.
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