using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zidimi.Browser.Infrastructure.Localization;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Infrastructure;

public sealed class LanguageInfo
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

/// <summary>
/// WPF localization facade backed by Chromium DataPack locale files in locales/*.pak.
/// Zidimi no longer ships or reads language/*.lng and does not create a parallel locale directory.
/// </summary>
public sealed class LanguageManager : INotifyPropertyChanged
{
    private static readonly Lazy<LanguageManager> LazyInstance = new(() => new LanguageManager());
    public static LanguageManager Instance => LazyInstance.Value;

    private readonly Dictionary<string, string> _currentStrings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fallbackStrings = new(StringComparer.OrdinalIgnoreCase);
    private LanguageInfo? _currentLanguage;

    public List<LanguageInfo> AvailableLanguages { get; } = new();

    public LanguageInfo? CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (value == null || string.Equals(_currentLanguage?.Code, value.Code, StringComparison.OrdinalIgnoreCase)) return;
            _currentLanguage = value;
            LoadDictionary(value.Code, _currentStrings);
            AppSettings.Global.DisplayLanguage = value.Code;

            // intl.selected_languages / intl.accept_languages are real Chromium profile prefs.
            // The locale pack itself is selected before Cef.Initialize, so changing language at
            // runtime updates Zidimi immediately and Chromium web-language prefs through CEF.
            AppSettings.SaveGlobal();
            AppSettings.SaveProfile();
            OnPropertyChanged();
            OnPropertyChanged("Item[]");
        }
    }

    public string this[string key]
    {
        get
        {
            if (_currentStrings.TryGetValue(key, out var value)) return value;
            if (_fallbackStrings.TryGetValue(key, out value)) return value;
            return key;
        }
    }

    private LanguageManager() => Initialize();

    public void Initialize()
    {
        AvailableLanguages.Clear();
        _currentStrings.Clear();
        _fallbackStrings.Clear();

        // Must happen before Cef.Initialize. ChromiumLocalePackManager augments the stock CEF
        // locales/*.pak files in-place with Zidimi's WPF resource IDs, preserving Chromium's pack.
        ChromiumLocalePackManager.EnsureMerged();
        AvailableLanguages.AddRange(ChromiumLocalePackManager.GetLanguages());

        LoadDictionary("en-US", _fallbackStrings);

        var requestedCode = NormalizeUiCode(AppSettings.Global.DisplayLanguage);
        var selected = AvailableLanguages.FirstOrDefault(l =>
                           l.Code.Equals(requestedCode, StringComparison.OrdinalIgnoreCase))
                       ?? AvailableLanguages.FirstOrDefault(l =>
                           l.Code.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                       ?? AvailableLanguages.FirstOrDefault();

        if (selected != null)
        {
            _currentLanguage = selected;
            LoadDictionary(selected.Code, _currentStrings);
            AppSettings.Global.DisplayLanguage = selected.Code;
        }
    }

    /// <summary>Applies a Chromium-owned language preference without writing it back again.</summary>
    public void ApplyFromSettings(string? code)
    {
        if (AvailableLanguages.Count == 0) return;
        var normalized = NormalizeUiCode(code);
        var selected = AvailableLanguages.FirstOrDefault(l =>
                           l.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                       ?? AvailableLanguages.FirstOrDefault(l =>
                           l.Code.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                       ?? AvailableLanguages.First();
        if (string.Equals(_currentLanguage?.Code, selected.Code, StringComparison.OrdinalIgnoreCase)) return;
        _currentLanguage = selected;
        LoadDictionary(selected.Code, _currentStrings);
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged("Item[]");
    }

    public static string NormalizeUiCode(string? code)
    {
        var value = (code ?? string.Empty).Trim().Replace('_', '-');
        if (value.StartsWith("vi", StringComparison.OrdinalIgnoreCase)) return "vi-VN";
        if (value.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return "fr-FR";
        if (value.StartsWith("de", StringComparison.OrdinalIgnoreCase)) return "de-DE";
        if (value.StartsWith("it", StringComparison.OrdinalIgnoreCase)) return "it-IT";
        if (value.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) return "ru-RU";
        if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        return "en-US";
    }

    private static void LoadDictionary(string code, Dictionary<string, string> target)
    {
        target.Clear();
        foreach (var (key, value) in ChromiumLocalePackManager.LoadLanguage(NormalizeUiCode(code)))
            target[key] = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
