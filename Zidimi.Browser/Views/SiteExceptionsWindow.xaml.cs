using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CefSharp;
using CefSharp.Enums;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

public partial class SiteExceptionsWindow : Window
{
    private static readonly PermissionDescriptor[] Permissions =
    [
        new("media_stream_camera", "Perm_Camera", ContentSettingTypes.MediaStreamCamera),
        new("media_stream_mic", "Perm_Microphone", ContentSettingTypes.MediaStreamMic),
        new("geolocation", "Perm_Location", ContentSettingTypes.Geolocation),
        new("notifications", "Perm_Notifications", ContentSettingTypes.Notifications),
        new("clipboard", "Perm_Clipboard", ContentSettingTypes.ClipboardReadWrite),
        new("midi_sysex", "Perm_Midi", ContentSettingTypes.MidiSysex),
        new("automatic_downloads", "Perm_MultipleDownloads", ContentSettingTypes.AutomaticDownloads),
        new("protected_media_identifier", "Perm_ProtectedMedia", ContentSettingTypes.ProtectedMediaIdentifier),
        new("popups", "Perm_Popups", ContentSettingTypes.Popups),
        new("javascript", "Perm_JavaScript", ContentSettingTypes.JavaScript),
    ];

    private readonly IRequestContext _context;

    public SiteExceptionsWindow()
    {
        InitializeComponent();
        _context = App.RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile)
                   ?? Cef.GetGlobalRequestContext();
        Loaded += async (_, _) => await LoadExceptionsAsync();
    }

    private async Task LoadExceptionsAsync()
    {
        ExceptionsPanel.Children.Clear();
        var hasAny = false;

        foreach (var permission in Permissions)
        {
            var raw = await _context.GetPreferenceSafeAsync(
                $"profile.content_settings.exceptions.{permission.PreferenceKey}")
                as IDictionary<string, object>;
            if (raw is null || raw.Count == 0) continue;

            var rows = new List<(string Origin, ContentSettingValues Value)>();
            foreach (var (pattern, value) in raw)
            {
                if (!TryExtractOrigin(pattern, out var origin)) continue;
                if (!TryReadSetting(value, out var setting)) continue;
                rows.Add((origin, setting));
            }

            if (rows.Count == 0) continue;
            hasAny = true;

            ExceptionsPanel.Children.Add(new TextBlock
            {
                Text = LanguageManager.Instance[permission.LabelKey],
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Ink200Brush"),
                Margin = new Thickness(0, 10, 0, 5),
            });

            foreach (var row in rows)
                ExceptionsPanel.Children.Add(CreateExceptionRow(permission, row.Origin, row.Value));
        }

        if (!hasAny)
        {
            ExceptionsPanel.Children.Add(new TextBlock
            {
                Text = LanguageManager.Instance["Pref_NoExceptions"],
                Foreground = (Brush)FindResource("Ink400Brush"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 10, 0, 0),
            });
        }

    }

    private FrameworkElement CreateExceptionRow(
        PermissionDescriptor permission, string origin, ContentSettingValues setting)
    {
        var grid = new Grid { Margin = new Thickness(10, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = origin,
            Foreground = (Brush)FindResource("Ink100Brush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var stateText = new TextBlock
        {
            Text = setting switch
            {
                ContentSettingValues.Allow => LanguageManager.Instance["Browser_Allowed"],
                ContentSettingValues.Block => LanguageManager.Instance["Browser_Blocked"],
                _ => LanguageManager.Instance["Browser_Default"],
            },
            Foreground = (Brush)FindResource("Ink400Brush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        Grid.SetColumn(stateText, 1);
        grid.Children.Add(stateText);

        var removeButton = new ZidimiButton
        {
            Content = LanguageManager.Instance["ProfileManager_Delete"],
            Padding = new Thickness(8, 4, 8, 4),
        };
        Grid.SetColumn(removeButton, 2);
        removeButton.Click += async (_, _) =>
        {
            removeButton.IsEnabled = false;
            try
            {
                await CefProfileDataHelper.SetContentSettingAsync(
                    _context,
                    origin,
                    origin,
                    permission.ContentType,
                    ContentSettingValues.Default);
                await LoadExceptionsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Log("SitePermissions", ex, $"Removing {permission.ContentType} exception for {origin}.");
                removeButton.IsEnabled = true;
            }
        };
        grid.Children.Add(removeButton);

        return grid;
    }

    private static bool TryReadSetting(object value, out ContentSettingValues setting)
    {
        setting = ContentSettingValues.Default;
        if (value is not IDictionary<string, object> values ||
            !values.TryGetValue("setting", out var rawSetting))
            return false;

        var number = rawSetting switch
        {
            int i => i,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            _ => -1,
        };

        if (number < 0) return false;
        setting = (ContentSettingValues)number;
        return true;
    }

    private static bool TryExtractOrigin(string pattern, out string origin)
    {
        origin = string.Empty;
        var firstPattern = pattern.Split(',', 2)[0].Trim();
        if (!Uri.TryCreate(firstPattern, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        origin = uri.GetLeftPart(UriPartial.Authority) + "/";
        return true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record PermissionDescriptor(
        string PreferenceKey,
        string LabelKey,
        ContentSettingTypes ContentType);
}
