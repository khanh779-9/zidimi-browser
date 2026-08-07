using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CefSharp;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;
using Heco.Browser.Views;

namespace Heco.Browser.Views;
public partial class PreferencesView : UserControl
{
    private string _currentSection = "General";

    public PreferencesView()
    {
        InitializeComponent();
        // Rebuild the section being viewed when the theme changes so code-built labels pick up the new brushes.
        ThemeManager.ThemeChanged += OnThemeChanged;
        Unloaded += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;
        if (SettingsContent.Content == null)
            LoadSettingsSection("General");
    }

    private void OnThemeChanged(ThemeManager.AppTheme theme)
    {
        if (Dispatcher.CheckAccess())
            LoadSettingsSection(_currentSection);
        else
            Dispatcher.BeginInvoke(() => LoadSettingsSection(_currentSection));
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (SettingsContent == null) return;
        if (sender is RadioButton rb && rb.Tag is string tag)
            LoadSettingsSection(tag);
    }

    private void LoadSettingsSection(string section)
    {
        _currentSection = section;
        SettingsContent.Content = section switch
        {
            "General" => BuildGeneralSection(),
            "Profiles" => BuildProfilesSection(),
            "Autofill" => BuildAutofillSection(),
            "DefaultBrowser" => BuildDefaultBrowserSection(),
            "Appearance" => BuildAppearanceSection(),
            "Search" => BuildSearchSection(),
            "Privacy" => BuildPrivacySection(),
            "SitePermissions" => BuildSitePermissionsSection(),
            "Downloads" => BuildDownloadsSection(),
            "Languages" => BuildLanguagesSection(),
            "System" => BuildSystemSection(),
            "About" => BuildAboutSection(),
            _ => BuildGeneralSection()
        };
    }

    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = (sender as TextBox)?.Text?.Trim().ToLower() ?? "";
        if (NavPanel == null) return;

        foreach (var child in NavPanel.Children)
        {
            if (child is RadioButton rb && rb.Tag is string tag)
            {
                var label = FindNavLabel(rb)?.ToLower() ?? "";
                rb.Visibility = string.IsNullOrEmpty(query) || label.Contains(query) || tag.ToLower().Contains(query)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private static string? FindNavLabel(FrameworkElement element)
    {
        // NavItem content is a Grid { Path col0, TextBlock col1 } — find the deepest TextBlock.
        foreach (var descendant in EnumerateVisuals(element))
        {
            if (descendant is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
                return tb.Text;
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<DependencyObject> EnumerateVisuals(DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in EnumerateVisuals(child)) yield return d;
        }
    }

    private UIElement BuildGeneralSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_General"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_GeneralDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var tbHome = new TextBox { Width = 320, Text = AppSettings.Profile.HomePageUrl, FontSize = 13 };
        tbHome.TextChanged += (s, e) => { AppSettings.Profile.HomePageUrl = tbHome.Text; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_StartupPage"], LanguageManager.Instance["Pref_HomeUrl"], tbHome));

        var engines = SearchEngines.All;
        var idxEngine = SearchEngines.IndexOf(AppSettings.Profile.SearchEngine);
        var searchCombo = MakeCombo(200, idxEngine, engines);
        searchCombo.SelectionChanged += (s, e) =>
        {
            AppSettings.Profile.SearchEngine = searchCombo.SelectedItem is HecoComboBoxItem hcbi
                ? SearchEngines.Normalize(hcbi.Content?.ToString())
                : SearchEngines.Default;
            AppSettings.SaveAll();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultEngine"], LanguageManager.Instance["Pref_SelectEngine"], searchCombo));

        var startupCombo = MakeCombo(280, AppSettings.Profile.StartupBehavior, LanguageManager.Instance["Pref_StartupNewPage"], LanguageManager.Instance["Pref_StartupContinue"], LanguageManager.Instance["Pref_StartupSpecific"]);
        startupCombo.SelectionChanged += (s, e) => { AppSettings.Profile.StartupBehavior = startupCombo.SelectedIndex; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_OnStartup"], LanguageManager.Instance["Pref_StartupAction"], startupCombo));

        var tbPages = new TextBox
        {
            Width = 380,
            MinHeight = 80,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 13,
            Text = string.Join("\n", AppSettings.Profile.StartupPages),
        };
        tbPages.TextChanged += (s, e) =>
        {
            AppSettings.Profile.StartupPages = tbPages.Text
                .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            AppSettings.SaveAll();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_SpecificPages"], LanguageManager.Instance["Pref_OnePerLine"], tbPages));

        return panel;
    }

    private UIElement BuildProfilesSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Profile"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ProfileDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var isLoggedIn = !string.IsNullOrEmpty(AppSettings.Global.LoggedInUser);
        var btnLogin = new HecoButton { Content = isLoggedIn ? LanguageManager.Instance["Pref_Logout"] : LanguageManager.Instance["Login_SignIn"], Style = (Style)FindResource("HecoButtonPrimary"), Padding = new Thickness(16,8,16,8) };
        btnLogin.Click += (s, e) => 
        {
            if (!string.IsNullOrEmpty(AppSettings.Global.LoggedInUser))
            {
                var res = HecoMessageBox.Show(LanguageManager.Instance["Pref_ConfirmLogout"], "Heco Browser", HecoMessageBoxButton.YesNo, HecoMessageBoxImage.Question, Window.GetWindow(this));
                if (res == HecoMessageBoxResult.Yes)
                {
                    AppSettings.Global.LoggedInUser = null;
                    AppSettings.SaveAll();
                    LoadSettingsSection("Profiles"); // Reload UI
                }
            }
            else
            {
                var window = new LoginWindow { Owner = Window.GetWindow(this) };
                if (window.ShowDialog() == true)
                {
                    LoadSettingsSection("Profiles"); // Reload UI
                }
            }
        };
        var syncTitle = isLoggedIn ? $"{LanguageManager.Instance["Pref_SyncTitle"]} ({AppSettings.Global.LoggedInUser})" : LanguageManager.Instance["Pref_SyncTitle"];
        panel.Children.Add(CreateSettingRow(syncTitle, LanguageManager.Instance["Pref_SyncDataBeta"], btnLogin));

        var profiles = AppSettings.Global.Profiles.ToArray();
        var idxProfile = Array.IndexOf(profiles, AppSettings.Global.CurrentProfile);
        var profileCombo = MakeCombo(200, Math.Max(0, idxProfile), profiles);
        profileCombo.SelectionChanged += (s, e) =>
        {
            if (profileCombo.SelectedItem is HecoComboBoxItem hcbi)
            {
                var name = hcbi.Content?.ToString() ?? LanguageManager.Instance["Pref_PersonalProfile"];
                if (AppSettings.Global.CurrentProfile != name)
                {
                    AppSettings.Global.CurrentProfile = name;
                    AppSettings.SaveAll();
                    App.ViewModel?.SwitchProfile(name);
                }
            }
        };

        var btnAddProfile = new HecoButton { Content = LanguageManager.Instance["Pref_AddProfile"], Padding = new Thickness(16,8,16,8) };
        btnAddProfile.Click += (s, e) => 
        {
            var newProfile = string.Format(LanguageManager.Instance["Pref_ProfileCount"], AppSettings.Global.Profiles.Count + 1);
            AppSettings.Global.Profiles.Add(newProfile);
            AppSettings.Global.CurrentProfile = newProfile;
            AppSettings.SaveAll();
            Infrastructure.UserDataPaths.EnsureProfileDir(newProfile);
            Infrastructure.UserDataPaths.RegisterProfile(newProfile);
            App.ViewModel?.SwitchProfile(newProfile);
            LoadSettingsSection("Profiles"); // Reload UI
        };

        var profilePanel = new StackPanel { Orientation = Orientation.Horizontal };
        profilePanel.Children.Add(profileCombo);
        profilePanel.Children.Add(new Border { Width = 8 });
        profilePanel.Children.Add(btnAddProfile);

        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_CurrentProfile"], LanguageManager.Instance["Pref_ProfileApplyDesc"], profilePanel));

        return panel;
    }

    private UIElement BuildAutofillSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Autofill"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_AutofillDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var btnPasswords = new HecoButton { Content = LanguageManager.Instance["Pref_ManagePasswords"], Padding = new Thickness(16,8,16,8) };
        btnPasswords.Click += (s, e) => { var w = new DataManagerWindow("passwords") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PasswordManager"], LanguageManager.Instance["Pref_PasswordDesc"], btnPasswords));

        var btnCards = new HecoButton { Content = LanguageManager.Instance["Pref_ManagePayments"], Padding = new Thickness(16,8,16,8) };
        btnCards.Click += (s, e) => { var w = new DataManagerWindow("cards") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PaymentMethods"], LanguageManager.Instance["Pref_PaymentDesc"], btnCards));

        var btnAddress = new HecoButton { Content = LanguageManager.Instance["Pref_ManageAddresses"], Padding = new Thickness(16,8,16,8) };
        btnAddress.Click += (s, e) => { var w = new DataManagerWindow("addresses") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_AddressAndMore"], LanguageManager.Instance["Pref_AddressDesc"], btnAddress));

        return panel;
    }

    private UIElement BuildDefaultBrowserSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_DefaultBrowser"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_MakeDefault"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var btnDefault = new HecoButton { Content = LanguageManager.Instance["Pref_SetDefault"], Style = (Style)FindResource("HecoButtonPrimary"), Padding = new Thickness(16,8,16,8) };
        btnDefault.Click += (s, e) => 
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                HecoMessageBox.Show(LanguageManager.Instance["Pref_WinSettingsError"] + ex.Message, LanguageManager.Instance["Pref_Error"], HecoMessageBoxButton.OK, HecoMessageBoxImage.Error, Window.GetWindow(this));
            }
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultBrowser"], LanguageManager.Instance["Pref_NotDefault"], btnDefault));

        return panel;
    }

    private UIElement BuildAppearanceSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Appearance"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_CustomizeAppearance"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var themeOptions = new[]
        {
            (Key: "light", Label: LanguageManager.Instance["Pref_ThemeLight"]),
            (Key: "dark", Label: LanguageManager.Instance["Pref_ThemeDark"]),
            (Key: "system", Label: LanguageManager.Instance["Pref_SystemTitle"]),
        };
        var currentTheme = Infrastructure.ThemeManager.NormalizeThemeKey(AppSettings.Profile.Theme);
        var idxTheme = Array.FindIndex(themeOptions, o => o.Key == currentTheme);
        var themeCombo = MakeCombo(180, Math.Max(0, idxTheme), themeOptions.Select(o => o.Label).ToArray());
        themeCombo.SelectionChanged += (s, e) => 
        {
            if (themeCombo.SelectedIndex >= 0 && themeCombo.SelectedIndex < themeOptions.Length)
            {
                AppSettings.Profile.Theme = themeOptions[themeCombo.SelectedIndex].Key;
                AppSettings.SaveAll();
                Infrastructure.ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
            }
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_Theme"], LanguageManager.Instance["Pref_SelectTheme"], themeCombo));

        var fontSizes = new[] { LanguageManager.Instance["Pref_SizeSmall"], LanguageManager.Instance["Pref_SizeMedium"], LanguageManager.Instance["Pref_SizeLarge"], LanguageManager.Instance["Pref_SizeExtraLarge"] };
        var idxFont = AppSettings.Profile.FontSize switch { 12 => 0, 14 => 1, 16 => 2, 18 => 3, _ => 1 };
        var fontCombo = MakeCombo(180, idxFont, fontSizes);
        fontCombo.SelectionChanged += (s, e) => 
        { 
            AppSettings.Profile.FontSize = fontCombo.SelectedIndex switch { 0 => 12, 1 => 14, 2 => 16, 3 => 18, _ => 14 };
            AppSettings.SaveAll();
            // Apply to the UI in real time
            if (Application.Current?.MainWindow is MainWindow mw)
                mw.FontSize = AppSettings.Profile.FontSize;
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_FontSize"], LanguageManager.Instance["Pref_DefaultFontSize"], fontCombo));

        var zooms = new[] { "25%", "50%", "75%", "90%", "100%", "110%", "125%", "150%", "200%" };
        var zoomLevels = new[] { -1.5, -1.0, -0.5, -0.2, 0.0, 0.5, 1.0, 1.5, 2.0 }; // CefSharp ZoomLevels are approx these values
        var idxZoom = Array.IndexOf(zoomLevels, AppSettings.Profile.ZoomLevel);
        var zoomCombo = MakeCombo(140, Math.Max(0, idxZoom), zooms);
        zoomCombo.SelectionChanged += (s, e) => 
        {
            if (zoomCombo.SelectedIndex >= 0 && zoomCombo.SelectedIndex < zoomLevels.Length)
                AppSettings.Profile.ZoomLevel = zoomLevels[zoomCombo.SelectedIndex];
            AppSettings.SaveAll();
            // Apply immediately to the active web tab (if any).
            var activeTab = App.ViewModel?.ActiveTab;
            if (activeTab != null)
            {
                var b = App.ViewModel?.GetBrowser(activeTab) as CefSharp.Wpf.ChromiumWebBrowser;
                b?.SetZoomLevel(AppSettings.Profile.ZoomLevel);
            }
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_ZoomPage"], LanguageManager.Instance["Pref_DefaultZoom"], zoomCombo));

        return panel;
    }

    private UIElement BuildSearchSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Search"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SearchSettings"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var engines = SearchEngines.All;
        var idxEngine = SearchEngines.IndexOf(AppSettings.Profile.SearchEngine);
        var searchCombo = MakeCombo(200, idxEngine, engines);
        searchCombo.SelectionChanged += (s, e) =>
        {
            AppSettings.Profile.SearchEngine = searchCombo.SelectedItem is HecoComboBoxItem hcbi
                ? SearchEngines.Normalize(hcbi.Content?.ToString())
                : SearchEngines.Default;
            AppSettings.SaveAll();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultEngine"], LanguageManager.Instance["Pref_SearchEngineTitle"], searchCombo));

        var suggestCheck = MakeCheck(LanguageManager.Instance["Pref_ShowSearchSuggestions"], AppSettings.Profile.SearchSuggestEnabled);
        suggestCheck.Checked += (s, e) => { AppSettings.Profile.SearchSuggestEnabled = true; AppSettings.SaveAll(); };
        suggestCheck.Unchecked += (s, e) => { AppSettings.Profile.SearchSuggestEnabled = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", suggestCheck));

        return panel;
    }

    private UIElement BuildPrivacySection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Privacy"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ProtectData"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var chkCookie = MakeCheck(LanguageManager.Instance["Pref_BlockThirdPartyCookies"], AppSettings.Profile.BlockThirdPartyCookies);
        chkCookie.Checked += (s, e) => { AppSettings.Profile.BlockThirdPartyCookies = true; AppSettings.SaveAll(); };
        chkCookie.Unchecked += (s, e) => { AppSettings.Profile.BlockThirdPartyCookies = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkCookie));

        var chkDnt = MakeCheck(LanguageManager.Instance["Pref_DoNotTrack"], AppSettings.Profile.SendDoNotTrack);
        chkDnt.Checked += (s, e) => { AppSettings.Profile.SendDoNotTrack = true; AppSettings.SaveAll(); };
        chkDnt.Unchecked += (s, e) => { AppSettings.Profile.SendDoNotTrack = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkDnt));

        var chkSafe = MakeCheck(LanguageManager.Instance["Pref_SafeBrowsing"], AppSettings.Profile.SafeBrowsing);
        chkSafe.Checked += (s, e) => { AppSettings.Profile.SafeBrowsing = true; AppSettings.SaveAll(); };
        chkSafe.Unchecked += (s, e) => { AppSettings.Profile.SafeBrowsing = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkSafe));

        var chkWarn = MakeCheck(LanguageManager.Instance["Pref_WarnDangerousSites"], AppSettings.Profile.WarnDangerousSites);
        chkWarn.Checked += (s, e) => { AppSettings.Profile.WarnDangerousSites = true; AppSettings.SaveAll(); };
        chkWarn.Unchecked += (s, e) => { AppSettings.Profile.WarnDangerousSites = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkWarn));

        var btnClear = MakeButton(LanguageManager.Instance["Pref_ClearBrowsingDataBtn"], 200);
        btnClear.Click += (s, e) => 
        {
            var window = new ClearDataWindow { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        };
        panel.Children.Add(CreateSettingRow("", "", btnClear));

        return panel;
    }

    private UIElement BuildSitePermissionsSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SitePermissions"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SitePermissionsDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var perms = AppSettings.Profile.SitePermissions;
        var ask = LanguageManager.Instance["Perm_Ask"];
        var allow = LanguageManager.Instance["Perm_Allow"];
        var block = LanguageManager.Instance["Perm_Block"];

        void Row(string label, string key)
        {
            var value = (ContentPermission)typeof(SitePermissions).GetProperty(key)!.GetValue(perms)!;
            var combo = MakeCombo(160, (int)value, ask, allow, block);
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                typeof(SitePermissions).GetProperty(key)!.SetValue(perms, (ContentPermission)combo.SelectedIndex);
                AppSettings.SaveAll();
            };
            panel.Children.Add(CreateSettingRow(label, "", combo));
        }

        Row(LanguageManager.Instance["Perm_Camera"], nameof(SitePermissions.Camera));
        Row(LanguageManager.Instance["Perm_Microphone"], nameof(SitePermissions.Microphone));
        Row(LanguageManager.Instance["Perm_Location"], nameof(SitePermissions.Geolocation));
        Row(LanguageManager.Instance["Perm_Notifications"], nameof(SitePermissions.Notifications));
        Row(LanguageManager.Instance["Perm_Clipboard"], nameof(SitePermissions.Clipboard));
        Row(LanguageManager.Instance["Perm_PointerLock"], nameof(SitePermissions.PointerLock));
        Row(LanguageManager.Instance["Perm_Midi"], nameof(SitePermissions.MidiSysex));
        Row(LanguageManager.Instance["Perm_FileSystem"], nameof(SitePermissions.FileSystemAccess));
        Row(LanguageManager.Instance["Perm_IdleDetection"], nameof(SitePermissions.IdleDetection));
        Row(LanguageManager.Instance["Perm_LocalFonts"], nameof(SitePermissions.LocalFonts));
        Row(LanguageManager.Instance["Perm_MultipleDownloads"], nameof(SitePermissions.MultipleDownloads));
        Row(LanguageManager.Instance["Perm_WindowManagement"], nameof(SitePermissions.WindowManagement));
        Row(LanguageManager.Instance["Perm_KeyboardLock"], nameof(SitePermissions.KeyboardLock));
        Row(LanguageManager.Instance["Perm_ProtectedMedia"], nameof(SitePermissions.ProtectedMedia));
        Row(LanguageManager.Instance["Perm_HandTracking"], nameof(SitePermissions.HandTracking));

        var chkPopups = MakeCheck(LanguageManager.Instance["Pref_BlockPopups"], AppSettings.Profile.SitePermissions.BlockPopups);
        chkPopups.Checked += (s, e) => { AppSettings.Profile.SitePermissions.BlockPopups = true; AppSettings.SaveAll(); };
        chkPopups.Unchecked += (s, e) => { AppSettings.Profile.SitePermissions.BlockPopups = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_Popups"], "", chkPopups));

        return panel;
    }

    private UIElement BuildDownloadsSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Downloads"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ManageDownloads"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var tbDownload = new TextBox { Text = AppSettings.Profile.DownloadPath, IsReadOnly = true, FontSize = 13 };

        var btnBrowse = MakeButton(LanguageManager.Instance["Pref_ChooseFolder"], 130);
        btnBrowse.Click += (s, e) => 
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = System.IO.Directory.Exists(AppSettings.Profile.DownloadPath)
                    ? AppSettings.Profile.DownloadPath
                    : System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                Title = LanguageManager.Instance["Pref_ChooseDownloadFolder"],
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            {
                AppSettings.Profile.DownloadPath = dlg.FolderName;
                AppSettings.SaveAll();
                tbDownload.Text = dlg.FolderName;
            }
        };

        var dlPanel = new Grid();
        dlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        dlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        Grid.SetColumn(tbDownload, 0);
        Grid.SetColumn(btnBrowse, 2);

        dlPanel.Children.Add(tbDownload);
        dlPanel.Children.Add(btnBrowse);

        var row = CreateSettingRow(LanguageManager.Instance["Pref_DefaultDownloadFolder"], "", dlPanel);
        dlPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        dlPanel.Margin = new Thickness(16, 0, 0, 0);
        panel.Children.Add(row);

        var btnOpen = MakeButton(LanguageManager.Instance["Pref_OpenFolder"], 140);
        btnOpen.Click += (s, e) => 
        {
            try { System.Diagnostics.Process.Start("explorer.exe", AppSettings.Profile.DownloadPath); }
            catch { }
        };
        panel.Children.Add(CreateSettingRow("", "", btnOpen));

        var chkAsk = MakeCheck(LanguageManager.Instance["Pref_AskWhereToSave"], AppSettings.Profile.AskBeforeSave);
        chkAsk.Checked += (s, e) => { AppSettings.Profile.AskBeforeSave = true; AppSettings.SaveAll(); };
        chkAsk.Unchecked += (s, e) => { AppSettings.Profile.AskBeforeSave = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkAsk));

        var chkBar = MakeCheck(LanguageManager.Instance["Pref_ShowDownloadBar"], AppSettings.Profile.ShowDownloadBar);
        chkBar.Checked += (s, e) => { AppSettings.Profile.ShowDownloadBar = true; AppSettings.SaveAll(); };
        chkBar.Unchecked += (s, e) => { AppSettings.Profile.ShowDownloadBar = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", chkBar));

        return panel;
    }

    private UIElement BuildLanguagesSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Languages"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_LangTitle"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var langs = LanguageManager.Instance.AvailableLanguages.Select(l => l.Name).ToArray();
        var currentLangName = LanguageManager.Instance.CurrentLanguage?.Name ?? "English";
        var idxLang = Array.IndexOf(langs, currentLangName);
        var langCombo = MakeCombo(200, Math.Max(0, idxLang), langs);
        langCombo.SelectionChanged += (s, e) =>
        {
            if (langCombo.SelectedItem is HecoComboBoxItem hcbi)
            {
                var selectedName = hcbi.Content?.ToString();
                var selectedLang = LanguageManager.Instance.AvailableLanguages.FirstOrDefault(l => l.Name == selectedName);
                if (selectedLang != null && LanguageManager.Instance.CurrentLanguage != selectedLang)
                {
                    LanguageManager.Instance.CurrentLanguage = selectedLang;
                    AppSettings.Global.DisplayLanguage = selectedLang.Code;
                    AppSettings.SaveAll();
                    
                    // Refresh the UI (because the texts in the content section are built hardcoded in C#)
                    LoadSettingsSection("Languages");
                }
            }
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DisplayLang"], LanguageManager.Instance["Pref_SelectUILang"], langCombo));

        return panel;
    }

    private UIElement BuildSystemSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_System"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SystemSettings"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var gpuCheck = MakeCheck(LanguageManager.Instance["Pref_HardwareAccel"], AppSettings.Global.EnableGpu);
        gpuCheck.Checked += (s, e) => { AppSettings.Global.EnableGpu = true; AppSettings.SaveAll(); };
        gpuCheck.Unchecked += (s, e) => { AppSettings.Global.EnableGpu = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", gpuCheck));

        var enhanceCheck = MakeCheck(LanguageManager.Instance["Pref_EnhanceVideos"], AppSettings.Global.EnhanceVideos);
        enhanceCheck.Checked += (s, e) => { AppSettings.Global.EnhanceVideos = true; AppSettings.SaveAll(); };
        enhanceCheck.Unchecked += (s, e) => { AppSettings.Global.EnhanceVideos = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", enhanceCheck));

        var bgCheck = MakeCheck(LanguageManager.Instance["Pref_RunInBackground"], AppSettings.Global.RunInBackground);
        bgCheck.Checked += (s, e) => { AppSettings.Global.RunInBackground = true; AppSettings.SaveAll(); };
        bgCheck.Unchecked += (s, e) => { AppSettings.Global.RunInBackground = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", bgCheck));

        var proxyCheck = MakeCheck(LanguageManager.Instance["Pref_UseSystemProxy"], AppSettings.Global.UseSystemProxy);
        proxyCheck.Checked += (s, e) => { AppSettings.Global.UseSystemProxy = true; AppSettings.SaveAll(); };
        proxyCheck.Unchecked += (s, e) => { AppSettings.Global.UseSystemProxy = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", proxyCheck));

        var btnProxy = MakeButton(LanguageManager.Instance["Pref_OpenProxySettings"], 200);
        btnProxy.Click += (s, e) => 
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:network-proxy") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                HecoMessageBox.Show(LanguageManager.Instance["Pref_ProxyError"] + ex.Message, LanguageManager.Instance["Pref_Error"], HecoMessageBoxButton.OK, HecoMessageBoxImage.Error, Window.GetWindow(this));
            }
        };
        panel.Children.Add(CreateSettingRow("", "", btnProxy));

        return panel;
    }

    private UIElement BuildAboutSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_About"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_VersionInfo"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var info = new[]
        {
            (LanguageManager.Instance["Pref_Version"], "Heco Browser " + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0")),
            (LanguageManager.Instance["Pref_EngineLabel"], "Chromium (CefSharp 150)"),
            (LanguageManager.Instance["Pref_Runtime"], ".NET 8 (WPF, x86)"),
            (LanguageManager.Instance["Pref_SourceCode"], LanguageManager.Instance["Pref_AboutSourceCode"]),
            (LanguageManager.Instance["Pref_License"], LanguageManager.Instance["Pref_AboutLicense"]),
        };
        foreach (var (label, value) in info)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = (Brush)FindResource("Ink400Brush") });
            grid.Children.Add(new TextBlock { Text = value, FontSize = 13, Foreground = (Brush)FindResource("Ink200Brush") });
            Grid.SetColumn(grid.Children[1], 1);
            panel.Children.Add(grid);
        }

        var btnCheck = MakeButton(LanguageManager.Instance["Pref_CheckUpdate"], 160);
        btnCheck.Click += async (s, e) => 
        {
            var originalContent = btnCheck.Content;
            btnCheck.Content = LanguageManager.Instance["Pref_CheckingUpdate"];
            btnCheck.IsEnabled = false;
            await System.Threading.Tasks.Task.Delay(2000); // Simulate a network check
            btnCheck.Content = originalContent;
            btnCheck.IsEnabled = true;
            HecoMessageBox.Show(LanguageManager.Instance["Pref_UpToDate"], LanguageManager.Instance["Pref_Update"], HecoMessageBoxButton.OK, HecoMessageBoxImage.Information, Window.GetWindow(this));
        };
        btnCheck.Margin = new Thickness(0, 16, 0, 0);
        panel.Children.Add(btnCheck);

        return panel;
    }

    private Border CreateSettingRow(string label, string desc, UIElement control)
    {
        var border = new Border { Style = (Style)FindResource("CardPanel"), Margin = new Thickness(0, 0, 0, 12) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(label))
            stack.Children.Add(new TextBlock { Text = label, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Ink100Brush") });
        if (!string.IsNullOrEmpty(desc))
            stack.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 2, 0, 0) });

        grid.Children.Add(stack);
        grid.Children.Add(control);
        
        if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(desc))
        {
            Grid.SetColumnSpan(control, 2);
            if (control is FrameworkElement fe)
                fe.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            Grid.SetColumn(control, 1);
            if (control is FrameworkElement fe)
                fe.HorizontalAlignment = HorizontalAlignment.Right;
        }

        border.Child = grid;
        return border;
    }

    private static HecoComboBox MakeCombo(double width, params string[] items)
        => MakeCombo(width, selectedIndex: 0, items);

    private static HecoComboBox MakeCombo(double width, int selectedIndex, params string[] items)
    {
        var combo = new HecoComboBox { Width = width };
        foreach (var item in items)
            combo.Items.Add(new HecoComboBoxItem { Content = item });
        combo.SelectedIndex = selectedIndex;
        return combo;
    }

    private static HecoCheckBox MakeCheck(string label, bool isChecked)
        => new() { Content = label, IsChecked = isChecked };

    private static HecoButton MakeButton(string content, double width)
    {
        var btn = new HecoButton
        {
            Content = content,
            Width = width,
            Style = (Style)Application.Current.Resources["HecoButtonPrimary"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        return btn;
    }
}

