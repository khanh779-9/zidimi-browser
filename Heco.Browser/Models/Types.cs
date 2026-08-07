using System.ComponentModel;

namespace Heco.Browser.Models;

/// <summary>
/// Identifies the pages shown in the sidebar.
/// </summary>
public enum PageId
{
    Browser,
    History,
    Bookmarks,
    Preferences,
    Downloads,
}

/// <summary>Tab kind: web (ChromiumWebBrowser) or an internal app tab (Settings/History/...).</summary>
public enum TabKind
{
    Web,
    Settings,
    History,
    Bookmarks,
    Downloads,
}

/// <summary>Theme state.</summary>
public enum Theme { Dark, Light }

/// <summary>Browsing history entry.</summary>
public sealed class HistoryEntry : INotifyPropertyChanged
{
    private string _title = "";
    private string _url = "";

    public long Id { get; set; }
    public DateTime VisitedAt { get; set; } = DateTime.Now;

    public string GroupDateText
    {
        get
        {
            try
            {
                var code = Heco.Browser.Infrastructure.LanguageManager.Instance.CurrentLanguage?.Code ?? "vi-VN";
                var culture = new System.Globalization.CultureInfo(code);
                var text = VisitedAt.ToString("dddd, dd/MM/yyyy", culture);
                if (!string.IsNullOrEmpty(text))
                    return char.ToUpper(text[0]) + text.Substring(1);
                return text;
            }
            catch
            {
                return VisitedAt.ToString("dddd, dd/MM/yyyy");
            }
        }
    }

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }

    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(nameof(Url)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));
}

/// <summary>Bookmark (folder_bookmark).</summary>
public sealed class Bookmark : INotifyPropertyChanged
{
    private string _title = "";
    private string _url = "";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }
    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(nameof(Url)); }
    }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));
}

/// <summary>Download entry for the Downloads panel.</summary>
public sealed class DownloadEntry : INotifyPropertyChanged
{
    private string _guid = System.Guid.NewGuid().ToString();
    private string _url = "";
    private string _suggestedFileName = "";
    private string _fullPath = "";
    private bool _isCancelled;
    private bool _isComplete;
    private long _totalBytes = -1;
    private long _receivedBytes;
    private DateTime _startedAt = DateTime.Now;

    public string Guid
    {
        get => _guid;
        set { _guid = value; OnPropertyChanged(nameof(Guid)); }
    }

    public DateTime StartedAt
    {
        get => _startedAt;
        set { _startedAt = value; OnPropertyChanged(nameof(StartedAt)); }
    }

    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(nameof(Url)); }
    }
    public string SuggestedFileName
    {
        get => _suggestedFileName;
        set { _suggestedFileName = value; OnPropertyChanged(nameof(SuggestedFileName)); }
    }
    public string FullPath
    {
        get => _fullPath;
        set { _fullPath = value; OnPropertyChanged(nameof(FullPath)); }
    }
    public bool IsCancelled
    {
        get => _isCancelled;
        set { _isCancelled = value; OnPropertyChanged(nameof(IsCancelled)); }
    }
    public bool IsComplete
    {
        get => _isComplete;
        set { _isComplete = value; OnPropertyChanged(nameof(IsComplete)); }
    }
    public long TotalBytes
    {
        get => _totalBytes;
        set { _totalBytes = value; OnPropertyChanged(nameof(TotalBytes)); }
    }
    public long ReceivedBytes
    {
        get => _receivedBytes;
        set { _receivedBytes = value; OnPropertyChanged(nameof(ReceivedBytes)); }
    }

public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));
    }

    /// <summary>Autocomplete suggestion for the omnibox (History/Bookmark/Search).</summary>
    public sealed class AutocompleteSuggestion : INotifyPropertyChanged
    {
        private string _title = "";
        private string _subtitle = "";
        private string _iconPath = "";
        private string _typeLabel = "";
        private string _targetUrl = "";

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(nameof(Subtitle)); }
        }
        public string IconPath
        {
            get => _iconPath;
            set { _iconPath = value; OnPropertyChanged(nameof(IconPath)); }
        }
        public string TypeLabel
        {
            get => _typeLabel;
            set { _typeLabel = value; OnPropertyChanged(nameof(TypeLabel)); }
        }
        public string TargetUrl
        {
            get => _targetUrl;
            set { _targetUrl = value; OnPropertyChanged(nameof(TargetUrl)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));
    }

    /// <summary>ViewModel for one browser tab.</summary>
    public sealed class TabViewModel : INotifyPropertyChanged
{
    private string _title = "New Tab";
    private string _address = "";
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isActive;
    private System.Windows.Media.ImageSource? _favicon;
    private bool _isAudioPlaying;
    private bool _isMuted;
    private bool _isPinned;

    public Guid Id { get; } = Guid.NewGuid();
    public TabKind Kind { get; set; } = TabKind.Web;
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(nameof(Title)); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(nameof(Address)); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        set { _canGoBack = value; OnPropertyChanged(nameof(CanGoBack)); }
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        set { _canGoForward = value; OnPropertyChanged(nameof(CanGoForward)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
    }

    /// <summary>The page's favicon, loaded asynchronously (null = fallback).</summary>
    public System.Windows.Media.ImageSource? Favicon
    {
        get => _favicon;
        set { _favicon = value; OnPropertyChanged(nameof(Favicon)); }
    }

    /// <summary>Tab is currently playing audio.</summary>
    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        set { _isAudioPlaying = value; OnPropertyChanged(nameof(IsAudioPlaying)); }
    }

    /// <summary>Tab is muted.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(nameof(IsMuted)); }
    }

    /// <summary>Tab is pinned (icon-only, at the top of the list).</summary>
    public bool IsPinned
    {
        get => _isPinned;
        set { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));
}
