using System.Globalization;
using System.Windows.Media;
using Zidimi.Browser.Infrastructure;

namespace Zidimi.Browser.Models;

/// <summary>Tab kind: Chromium web content or one of Zidimi's internal app pages.</summary>
public enum TabKind
{
    Web,
    Settings,
    History,
    Bookmarks,
    Downloads,
    Extensions,
}

/// <summary>WPF projection of a Chromium browsing-history entry.</summary>
public sealed class HistoryEntry : ViewModelBase
{
    private string _title = string.Empty;
    private string _url = string.Empty;
    private DateTime _visitedAt = DateTime.Now;

    public long Id { get; set; }

    public DateTime VisitedAt
    {
        get => _visitedAt;
        set
        {
            if (!Set(ref _visitedAt, value)) return;
            OnPropertyChanged(nameof(GroupDateText));
        }
    }

    public string GroupDateText
    {
        get
        {
            try
            {
                var code = LanguageManager.Instance.CurrentLanguage?.Code ?? "vi-VN";
                var text = VisitedAt.ToString("dddd, dd/MM/yyyy", CultureInfo.GetCultureInfo(code));
                return string.IsNullOrEmpty(text)
                    ? text
                    : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
            }
            catch (CultureNotFoundException)
            {
                return VisitedAt.ToString("dddd, dd/MM/yyyy", CultureInfo.CurrentCulture);
            }
        }
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? string.Empty);
    }

    public string Url
    {
        get => _url;
        set => Set(ref _url, value ?? string.Empty);
    }
}

/// <summary>WPF projection of a Chromium bookmark.</summary>
public sealed class Bookmark : ViewModelBase
{
    private string _title = string.Empty;
    private string _url = string.Empty;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? string.Empty);
    }

    public string Url
    {
        get => _url;
        set => Set(ref _url, value ?? string.Empty);
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>WPF projection of Chromium/CEF download state.</summary>
public sealed class DownloadEntry : ViewModelBase
{
    private string _guid = System.Guid.NewGuid().ToString();
    private string _url = string.Empty;
    private string _suggestedFileName = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isCancelled;
    private bool _isComplete;
    private long _totalBytes = -1;
    private long _receivedBytes;
    private DateTime _startedAt = DateTime.Now;

    public string Guid
    {
        get => _guid;
        set => Set(ref _guid, value ?? string.Empty);
    }

    public DateTime StartedAt
    {
        get => _startedAt;
        set => Set(ref _startedAt, value);
    }

    public string Url
    {
        get => _url;
        set => Set(ref _url, value ?? string.Empty);
    }

    public string SuggestedFileName
    {
        get => _suggestedFileName;
        set => Set(ref _suggestedFileName, value ?? string.Empty);
    }

    public string FullPath
    {
        get => _fullPath;
        set => Set(ref _fullPath, value ?? string.Empty);
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        set => Set(ref _isCancelled, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set => Set(ref _isComplete, value);
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set => Set(ref _totalBytes, value);
    }

    public long ReceivedBytes
    {
        get => _receivedBytes;
        set => Set(ref _receivedBytes, value);
    }
}

/// <summary>Autocomplete suggestion for the omnibox.</summary>
public sealed class AutocompleteSuggestion : ViewModelBase
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _iconPath = string.Empty;
    private string _typeLabel = string.Empty;
    private string _targetUrl = string.Empty;

    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? string.Empty);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => Set(ref _subtitle, value ?? string.Empty);
    }

    public string IconPath
    {
        get => _iconPath;
        set => Set(ref _iconPath, value ?? string.Empty);
    }

    public string TypeLabel
    {
        get => _typeLabel;
        set => Set(ref _typeLabel, value ?? string.Empty);
    }

    public string TargetUrl
    {
        get => _targetUrl;
        set => Set(ref _targetUrl, value ?? string.Empty);
    }
}

/// <summary>View model for one browser tab.</summary>
public sealed class TabViewModel : ViewModelBase
{
    private string _title = "New Tab";
    private string _address = string.Empty;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isActive;
    private ImageSource? _favicon;
    private bool _isAudioPlaying;
    private bool _isMuted;
    private bool _isPinned;
    private TabKind _kind = TabKind.Web;
    private int _tabId;

    // Shell identity is available before Chromium finishes creating the native browser.
    // Web tabs then receive their real CEF/extension tab id through TabId.
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Native Chromium tab id for a web tab. CEF exposes the browser Identifier as the same id
    /// consumed by extension APIs. A value of 0 means the browser is still being created or this
    /// is a Zidimi-native tab with no Chromium browser.
    /// </summary>
    public int TabId
    {
        get => _tabId;
        internal set
        {
            if (!Set(ref _tabId, value)) return;
            OnPropertyChanged(nameof(HasNativeTabId));
        }
    }

    public bool HasNativeTabId => _tabId > 0;

    public TabKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? string.Empty);
    }

    public string Address
    {
        get => _address;
        set => Set(ref _address, value ?? string.Empty);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => Set(ref _isLoading, value);
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        set => Set(ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        set => Set(ref _canGoForward, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    /// <summary>The page's favicon, or null to show the fallback icon.</summary>
    public ImageSource? Favicon
    {
        get => _favicon;
        set => Set(ref _favicon, value);
    }

    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        set => Set(ref _isAudioPlaying, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => Set(ref _isMuted, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => Set(ref _isPinned, value);
    }

    // Shell navigation only bridges transitions between Chromium pages and Zidimi-native pages.
    // Keep a browser-like bounded list so one long-lived tab cannot grow memory without limit.
    private const int MaxShellNavigationEntries = 256;
    private readonly List<string> _navigationHistory = new();
    private int _navigationIndex = -1;

    public bool HasNavigationHistory => _navigationIndex >= 0;

    /// <summary>
    /// Records a top-level location in Zidimi's tab history. Keeping this history at
    /// the shell level lets Back/Forward work across Chromium pages and native
    /// zidimi:// pages without registering a fake network scheme in CEF.
    /// </summary>
    public void RecordNavigation(string? address)
    {
        var value = address?.Trim() ?? string.Empty;
        if (value.Length == 0) return;

        if (_navigationIndex >= 0 &&
            string.Equals(_navigationHistory[_navigationIndex], value, StringComparison.OrdinalIgnoreCase))
        {
            UpdateNavigationAvailability();
            return;
        }

        if (_navigationIndex + 1 < _navigationHistory.Count)
            _navigationHistory.RemoveRange(_navigationIndex + 1, _navigationHistory.Count - _navigationIndex - 1);

        _navigationHistory.Add(value);
        _navigationIndex = _navigationHistory.Count - 1;

        if (_navigationHistory.Count > MaxShellNavigationEntries)
        {
            var removeCount = _navigationHistory.Count - MaxShellNavigationEntries;
            _navigationHistory.RemoveRange(0, removeCount);
            _navigationIndex = Math.Max(0, _navigationIndex - removeCount);
        }

        UpdateNavigationAvailability();
    }

    public void ResetNavigation(string? address)
    {
        _navigationHistory.Clear();
        _navigationIndex = -1;
        RecordNavigation(address);
    }

    public string? MoveBack()
    {
        if (_navigationIndex <= 0) return null;
        _navigationIndex--;
        UpdateNavigationAvailability();
        return _navigationHistory[_navigationIndex];
    }

    public string? MoveForward()
    {
        if (_navigationIndex < 0 || _navigationIndex >= _navigationHistory.Count - 1) return null;
        _navigationIndex++;
        UpdateNavigationAvailability();
        return _navigationHistory[_navigationIndex];
    }

    private void UpdateNavigationAvailability()
    {
        CanGoBack = _navigationIndex > 0;
        CanGoForward = _navigationIndex >= 0 && _navigationIndex < _navigationHistory.Count - 1;
    }
}
