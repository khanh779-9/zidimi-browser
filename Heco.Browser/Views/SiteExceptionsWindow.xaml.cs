using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CefSharp;
using Heco.Browser.Controls;
using Heco.Browser.Models;
using Heco.Browser.Infrastructure;

namespace Heco.Browser.Views;

public partial class SiteExceptionsWindow : Window
{
    private static readonly string[] _keys = new[] 
    { 
        "media_stream_camera", "media_stream_mic", "geolocation", "notifications", 
        "clipboard", "mouselock", "midi_sysex", "automatic_downloads", "window_placement", 
        "protected_media_identifier", "idle_detection", "file_system_write_guard", "local_fonts", 
        "ar", "vr", "sensors" 
    };

    public SiteExceptionsWindow()
    {
        InitializeComponent();
        LoadExceptions();
    }

    private void LoadExceptions()
    {
        ExceptionsPanel.Children.Clear();
        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        bool hasAny = false;
        foreach (var key in _keys)
        {
            var dict = ctx.GetPreference("profile.content_settings.exceptions." + key) as IDictionary<string, object>;
            if (dict == null || dict.Count == 0) continue;

            // Header for this permission
            ExceptionsPanel.Children.Add(new TextBlock 
            { 
                Text = key.Replace("_", " ").ToUpperInvariant(), 
                FontWeight = FontWeights.Bold, 
                Foreground = (Brush)FindResource("Ink200Brush"),
                Margin = new Thickness(0, 10, 0, 5)
            });

            foreach (var kvp in dict)
            {
                string origin = kvp.Key;
                if (kvp.Value is IDictionary<string, object> settingDict && settingDict.TryGetValue("setting", out var settingVal))
                {
                    hasAny = true;
                    int setting = 0;
                    if (settingVal is int i) setting = i;
                    else if (settingVal is long l) setting = (int)l;

                    string state = setting == 1 ? "Allow" : (setting == 2 ? "Block" : "Ask");
                    
                    var row = new Grid { Margin = new Thickness(10, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    row.Children.Add(new TextBlock { Text = origin, Foreground = (Brush)FindResource("Ink100Brush"), VerticalAlignment = VerticalAlignment.Center });
                    
                    var stateText = new TextBlock { Text = state, Foreground = (Brush)FindResource("Ink400Brush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                    Grid.SetColumn(stateText, 1);
                    row.Children.Add(stateText);

                    var removeText = LanguageManager.Instance["ProfileManager_Delete"];
                    if (removeText == "ProfileManager_Delete") removeText = "Xóa";
                    var btnRemove = new HecoButton { Content = removeText, Padding = new Thickness(8,4,8,4) };
                    Grid.SetColumn(btnRemove, 2);
                    btnRemove.Click += (s, e) => 
                    {
                        dict.Remove(kvp.Key);
                        ctx.SetPreference("profile.content_settings.exceptions." + key, dict, out string err);
                        LoadExceptions();
                    };
                    row.Children.Add(btnRemove);

                    ExceptionsPanel.Children.Add(row);
                }
            }
        }

        if (!hasAny)
        {
            var noExcText = LanguageManager.Instance["Pref_NoExceptions"];
            if (noExcText == "Pref_NoExceptions") noExcText = "Không tìm thấy ngoại lệ quyền trang web nào.";
            ExceptionsPanel.Children.Add(new TextBlock 
            { 
                Text = noExcText, 
                Foreground = (Brush)FindResource("Ink400Brush"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 10, 0, 0)
            });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
