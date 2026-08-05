using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Views;

public partial class PreferencesView : UserControl
{
    public PreferencesView()
    {
        InitializeComponent();
        if (SettingsContent.Content == null)
            LoadSettingsSection("General");
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (SettingsContent == null) return;
        if (sender is RadioButton rb && rb.Tag is string tag)
            LoadSettingsSection(tag);
    }

    private void LoadSettingsSection(string section)
    {
        SettingsContent.Content = section switch
        {
            "General" => BuildGeneralSection(),
            "Profiles" => BuildProfilesSection(),
            "Autofill" => BuildAutofillSection(),
            "DefaultBrowser" => BuildDefaultBrowserSection(),
            "Appearance" => BuildAppearanceSection(),
            "Search" => BuildSearchSection(),
            "Privacy" => BuildPrivacySection(),
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
                var label = (rb.Content as string)?.ToLower() ?? "";
                rb.Visibility = string.IsNullOrEmpty(query) || label.Contains(query) || tag.ToLower().Contains(query)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private UIElement BuildGeneralSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_General"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_GeneralDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var tbHome = new TextBox { Width = 320, Text = AppSettings.Current.HomePageUrl, FontSize = 13 };
        tbHome.TextChanged += (s, e) => { AppSettings.Current.HomePageUrl = tbHome.Text; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_StartupPage"], LanguageManager.Instance["Pref_HomeUrl"], tbHome));

        var engines = new[] { "DuckDuckGo", "Google", "Bing", "Brave Search" };
        var idxEngine = Array.IndexOf(engines, AppSettings.Current.SearchEngine);
        var searchCombo = MakeCombo(200, Math.Max(0, idxEngine), engines);
        searchCombo.SelectionChanged += (s, e) => 
        { 
            if (searchCombo.SelectedItem is ComboBoxItem cbi)
                AppSettings.Current.SearchEngine = cbi.Content?.ToString() ?? "Google";
            AppSettings.Current.Save(); 
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultEngine"], LanguageManager.Instance["Pref_SelectEngine"], searchCombo));

        var startupCombo = MakeCombo(280, AppSettings.Current.StartupBehavior, LanguageManager.Instance["Pref_StartupNewPage"], LanguageManager.Instance["Pref_StartupContinue"], LanguageManager.Instance["Pref_StartupSpecific"]);
        startupCombo.SelectionChanged += (s, e) => { AppSettings.Current.StartupBehavior = startupCombo.SelectedIndex; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_OnStartup"], LanguageManager.Instance["Pref_StartupAction"], startupCombo));

        return panel;
    }

    private UIElement BuildProfilesSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Profile"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ProfileDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var isLoggedIn = !string.IsNullOrEmpty(AppSettings.Current.LoggedInUser);
        var btnLogin = new HecoButton { Content = isLoggedIn ? "Đăng xuất" : LanguageManager.Instance["Login_SignIn"], Style = (Style)FindResource("HecoButtonPrimary"), Padding = new Thickness(16,8,16,8) };
        btnLogin.Click += (s, e) => 
        {
            if (!string.IsNullOrEmpty(AppSettings.Current.LoggedInUser))
            {
                var res = MessageBox.Show(LanguageManager.Instance["Pref_ConfirmLogout"], "Heco Browser", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    AppSettings.Current.LoggedInUser = null;
                    AppSettings.Current.Save();
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
        var syncTitle = isLoggedIn ? $"Đồng bộ dữ liệu ({AppSettings.Current.LoggedInUser})" : "Đồng bộ dữ liệu";
        panel.Children.Add(CreateSettingRow(syncTitle, LanguageManager.Instance["Pref_SyncDesc"], btnLogin));

        var profiles = AppSettings.Current.Profiles.ToArray();
        var idxProfile = Array.IndexOf(profiles, AppSettings.Current.CurrentProfile);
        var profileCombo = MakeCombo(200, Math.Max(0, idxProfile), profiles);
        profileCombo.SelectionChanged += (s, e) =>
        {
            if (profileCombo.SelectedItem is ComboBoxItem cbi)
            {
                AppSettings.Current.CurrentProfile = cbi.Content?.ToString() ?? LanguageManager.Instance["Pref_PersonalProfile"];
                AppSettings.Current.Save();
            }
        };

        var btnAddProfile = new HecoButton { Content = LanguageManager.Instance["Pref_AddProfile"], Padding = new Thickness(16,8,16,8) };
        btnAddProfile.Click += (s, e) => 
        {
            var newProfile = string.Format(LanguageManager.Instance["Pref_ProfileCount"], AppSettings.Current.Profiles.Count + 1);
            AppSettings.Current.Profiles.Add(newProfile);
            AppSettings.Current.CurrentProfile = newProfile;
            AppSettings.Current.Save();
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
                MessageBox.Show(LanguageManager.Instance["Pref_WinSettingsError"] + ex.Message, LanguageManager.Instance["Pref_Error"]);
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

        var themes = new[] { LanguageManager.Instance["Pref_ThemeLight"], LanguageManager.Instance["Pref_ThemeDark"], LanguageManager.Instance["Pref_System"] };
        var idxTheme = Array.IndexOf(themes, AppSettings.Current.Theme);
        var themeCombo = MakeCombo(180, Math.Max(0, idxTheme), themes);
        themeCombo.SelectionChanged += (s, e) => 
        { 
            if (themeCombo.SelectedItem is ComboBoxItem cbi)
                AppSettings.Current.Theme = cbi.Content?.ToString() ?? LanguageManager.Instance["Pref_SystemTitle"];
            AppSettings.Current.Save(); 
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_Theme"], LanguageManager.Instance["Pref_SelectTheme"], themeCombo));

        var fontSizes = new[] { LanguageManager.Instance["Pref_SizeSmall"], LanguageManager.Instance["Pref_SizeMedium"], LanguageManager.Instance["Pref_SizeLarge"], LanguageManager.Instance["Pref_SizeExtraLarge"] };
        var idxFont = AppSettings.Current.FontSize switch { 12 => 0, 14 => 1, 16 => 2, 18 => 3, _ => 1 };
        var fontCombo = MakeCombo(120, idxFont, fontSizes);
        fontCombo.SelectionChanged += (s, e) => 
        { 
            AppSettings.Current.FontSize = fontCombo.SelectedIndex switch { 0 => 12, 1 => 14, 2 => 16, 3 => 18, _ => 14 };
            AppSettings.Current.Save(); 
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_FontSize"], LanguageManager.Instance["Pref_DefaultFontSize"], fontCombo));

        var zooms = new[] { "25%", "50%", "75%", "90%", "100%", "110%", "125%", "150%", "200%" };
        var zoomLevels = new[] { -1.5, -1.0, -0.5, -0.2, 0.0, 0.5, 1.0, 1.5, 2.0 }; // CefSharp ZoomLevels are approx these values
        var idxZoom = Array.IndexOf(zoomLevels, AppSettings.Current.ZoomLevel);
        var zoomCombo = MakeCombo(120, Math.Max(0, idxZoom), zooms);
        zoomCombo.SelectionChanged += (s, e) => 
        {
            if (zoomCombo.SelectedIndex >= 0 && zoomCombo.SelectedIndex < zoomLevels.Length)
                AppSettings.Current.ZoomLevel = zoomLevels[zoomCombo.SelectedIndex];
            AppSettings.Current.Save(); 
        };
        panel.Children.Add(CreateSettingRow("Zoom trang", LanguageManager.Instance["Pref_DefaultZoom"], zoomCombo));

        return panel;
    }

    private UIElement BuildSearchSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Search"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SearchSettings"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var engines = new[] { "DuckDuckGo", "Google", "Bing", "Brave Search" };
        var idxEngine = Array.IndexOf(engines, AppSettings.Current.SearchEngine);
        var searchCombo = MakeCombo(200, Math.Max(0, idxEngine), engines);
        searchCombo.SelectionChanged += (s, e) => 
        {
            if (searchCombo.SelectedItem is ComboBoxItem cbi)
                AppSettings.Current.SearchEngine = cbi.Content?.ToString() ?? "Google";
            AppSettings.Current.Save();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultEngine"], LanguageManager.Instance["Pref_SearchEngineTitle"], searchCombo));

        var suggestCheck = MakeCheck(LanguageManager.Instance["Pref_ShowSearchSuggestions"], AppSettings.Current.SearchSuggestEnabled);
        suggestCheck.Checked += (s, e) => { AppSettings.Current.SearchSuggestEnabled = true; AppSettings.Current.Save(); };
        suggestCheck.Unchecked += (s, e) => { AppSettings.Current.SearchSuggestEnabled = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", suggestCheck));

        return panel;
    }

    private UIElement BuildPrivacySection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Privacy"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ProtectData"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var chkCookie = MakeCheck(LanguageManager.Instance["Pref_BlockThirdPartyCookies"], AppSettings.Current.BlockThirdPartyCookies);
        chkCookie.Checked += (s, e) => { AppSettings.Current.BlockThirdPartyCookies = true; AppSettings.Current.Save(); };
        chkCookie.Unchecked += (s, e) => { AppSettings.Current.BlockThirdPartyCookies = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", chkCookie));

        var chkDnt = MakeCheck(LanguageManager.Instance["Pref_DoNotTrack"], AppSettings.Current.SendDoNotTrack);
        chkDnt.Checked += (s, e) => { AppSettings.Current.SendDoNotTrack = true; AppSettings.Current.Save(); };
        chkDnt.Unchecked += (s, e) => { AppSettings.Current.SendDoNotTrack = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", chkDnt));

        var chkSafe = MakeCheck(LanguageManager.Instance["Pref_SafeBrowsing"], AppSettings.Current.SafeBrowsing);
        chkSafe.Checked += (s, e) => { AppSettings.Current.SafeBrowsing = true; AppSettings.Current.Save(); };
        chkSafe.Unchecked += (s, e) => { AppSettings.Current.SafeBrowsing = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", chkSafe));

        var chkWarn = MakeCheck(LanguageManager.Instance["Pref_WarnDangerousSites"], AppSettings.Current.WarnDangerousSites);
        chkWarn.Checked += (s, e) => { AppSettings.Current.WarnDangerousSites = true; AppSettings.Current.Save(); };
        chkWarn.Unchecked += (s, e) => { AppSettings.Current.WarnDangerousSites = false; AppSettings.Current.Save(); };
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

    private UIElement BuildDownloadsSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Downloads"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ManageDownloads"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var tbDownload = new TextBox { Width = 320, Text = AppSettings.Current.DownloadPath, IsReadOnly = true, FontSize = 13 };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DefaultDownloadFolder"], AppSettings.Current.DownloadPath, tbDownload));

        var btnOpen = MakeButton(LanguageManager.Instance["Pref_OpenFolder"], 140);
        btnOpen.Click += (s, e) => 
        {
            try { System.Diagnostics.Process.Start("explorer.exe", AppSettings.Current.DownloadPath); }
            catch { }
        };
        panel.Children.Add(CreateSettingRow("", "", btnOpen));

        var chkAsk = MakeCheck(LanguageManager.Instance["Pref_AskWhereToSave"], AppSettings.Current.AskBeforeSave);
        chkAsk.Checked += (s, e) => { AppSettings.Current.AskBeforeSave = true; AppSettings.Current.Save(); };
        chkAsk.Unchecked += (s, e) => { AppSettings.Current.AskBeforeSave = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", chkAsk));

        var chkBar = MakeCheck(LanguageManager.Instance["Pref_ShowDownloadBar"], AppSettings.Current.ShowDownloadBar);
        chkBar.Checked += (s, e) => { AppSettings.Current.ShowDownloadBar = true; AppSettings.Current.Save(); };
        chkBar.Unchecked += (s, e) => { AppSettings.Current.ShowDownloadBar = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", chkBar));

        return panel;
    }

    private UIElement BuildLanguagesSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Languages"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_LangTitle"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var langs = new[] { LanguageManager.Instance["Pref_LangVietnamese"], "English", "日本語", "中文 (简体)" };
        var idxLang = Array.IndexOf(langs, AppSettings.Current.DisplayLanguage);
        var langCombo = MakeCombo(200, Math.Max(0, idxLang), langs);
        langCombo.SelectionChanged += (s, e) =>
        {
            if (langCombo.SelectedItem is ComboBoxItem cbi)
                AppSettings.Current.DisplayLanguage = cbi.Content?.ToString() ?? LanguageManager.Instance["Pref_LangVietnamese"];
            AppSettings.Current.Save();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_DisplayLang"], LanguageManager.Instance["Pref_SelectUILang"], langCombo));

        var translateCheck = MakeCheck(LanguageManager.Instance["Pref_AutoTranslate"], AppSettings.Current.AutoTranslate);
        translateCheck.Checked += (s, e) => { AppSettings.Current.AutoTranslate = true; AppSettings.Current.Save(); };
        translateCheck.Unchecked += (s, e) => { AppSettings.Current.AutoTranslate = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", translateCheck));

        return panel;
    }

    private UIElement BuildSystemSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_System"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_SystemSettings"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var gpuCheck = MakeCheck(LanguageManager.Instance["Pref_HardwareAccel"], AppSettings.Current.EnableGpu);
        gpuCheck.Checked += (s, e) => { AppSettings.Current.EnableGpu = true; AppSettings.Current.Save(); };
        gpuCheck.Unchecked += (s, e) => { AppSettings.Current.EnableGpu = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", gpuCheck));

        var bgCheck = MakeCheck(LanguageManager.Instance["Pref_RunInBackground"], AppSettings.Current.RunInBackground);
        bgCheck.Checked += (s, e) => { AppSettings.Current.RunInBackground = true; AppSettings.Current.Save(); };
        bgCheck.Unchecked += (s, e) => { AppSettings.Current.RunInBackground = false; AppSettings.Current.Save(); };
        panel.Children.Add(CreateSettingRow("", "", bgCheck));

        var proxyCheck = MakeCheck(LanguageManager.Instance["Pref_UseSystemProxy"], AppSettings.Current.UseSystemProxy);
        proxyCheck.Checked += (s, e) => { AppSettings.Current.UseSystemProxy = true; AppSettings.Current.Save(); };
        proxyCheck.Unchecked += (s, e) => { AppSettings.Current.UseSystemProxy = false; AppSettings.Current.Save(); };
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
                MessageBox.Show(LanguageManager.Instance["Pref_ProxyError"] + ex.Message, LanguageManager.Instance["Pref_Error"]);
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
            (LanguageManager.Instance["Pref_Version"], "Heco Browser 1.0.0"),
            ("Engine", "Chromium (CefSharp 150)"),
            (".NET Runtime", "8.0 (WPF, x64)"),
            (LanguageManager.Instance["Pref_SourceCode"], "CefSharp / Chromium Embedded Framework"),
            (LanguageManager.Instance["Pref_License"], "BSD-3 (CEF/CefSharp)"),
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
            await System.Threading.Tasks.Task.Delay(2000); // Giả lập kiểm tra mạng
            btnCheck.Content = originalContent;
            btnCheck.IsEnabled = true;
            MessageBox.Show(LanguageManager.Instance["Pref_UpToDate"], LanguageManager.Instance["Pref_Update"], MessageBoxButton.OK, MessageBoxImage.Information);
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
