using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CefSharp;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;
using Zidimi.Browser.Views;

namespace Zidimi.Browser.Views;
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

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsContent == null) return;
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            rb.IsChecked = true;
            LoadSettingsSection(tag);
        }
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
            AppSettings.Profile.SearchEngine = searchCombo.SelectedItem is ZidimiComboBoxItem hcbi
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
        var btnLogin = new ZidimiButton { Content = isLoggedIn ? LanguageManager.Instance["Pref_Logout"] : LanguageManager.Instance["Login_SignIn"], Style = (Style)FindResource("ZidimiButtonPrimary"), Padding = new Thickness(16,8,16,8) };
        btnLogin.Click += (s, e) => 
        {
            if (!string.IsNullOrEmpty(AppSettings.Global.LoggedInUser))
            {
                var res = ZidimiMessageBox.Show(LanguageManager.Instance["Pref_ConfirmLogout"], "Zidimi Browser", ZidimiMessageBoxButton.YesNo, ZidimiMessageBoxImage.Question, Window.GetWindow(this));
                if (res == ZidimiMessageBoxResult.Yes)
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
            if (profileCombo.SelectedItem is ZidimiComboBoxItem hcbi)
            {
                var name = hcbi.Content?.ToString() ?? LanguageManager.Instance["Pref_PersonalProfile"];
                if (AppSettings.Global.CurrentProfile != name)
                {
                    AppSettings.Global.CurrentProfile = name;
                    AppSettings.LoadProfile(name);
                    AppSettings.SaveAll();
                    App.ViewModel?.SwitchProfile(name);
                    Infrastructure.ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
                }
            }
        };

        var btnManageProfile = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManageProfile"] ?? "Quản lý hồ sơ", Padding = new Thickness(16,8,16,8) };
        btnManageProfile.Click += (s, e) =>
        {
            var owner = Window.GetWindow(this);
            var ps = new ProfileSelectorWindow { Owner = owner };
            ps.ShowDialog();
            LoadSettingsSection("Profiles"); // Reload UI
        };

        var profilePanel = new StackPanel { Orientation = Orientation.Horizontal };
        profilePanel.Children.Add(profileCombo);
        profilePanel.Children.Add(new Border { Width = 8 });
        profilePanel.Children.Add(btnManageProfile);

        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_CurrentProfile"], LanguageManager.Instance["Pref_ProfileApplyDesc"], profilePanel));

        // Copy data from another profile
        var otherProfiles = AppSettings.Global.Profiles
            .Where(p => p != AppSettings.Global.CurrentProfile).ToArray();

        if (otherProfiles.Length > 0)
        {
            var copyFromCombo = MakeCombo(200, 0, otherProfiles);
            var btnCopyFrom = new ZidimiButton { Content = LanguageManager.Instance["Pref_CopyFromProfile"] ?? "Copy From", Padding = new Thickness(16,8,16,8) };
            btnCopyFrom.Click += async (s, e) =>
            {
                var selectedProfile = (copyFromCombo.SelectedItem as ZidimiComboBoxItem)?.Content?.ToString();
                if (string.IsNullOrEmpty(selectedProfile)) return;

                var sourceCtx = App.RequestContexts.GetProfileContext(selectedProfile);
                var targetCtx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile)
                                ?? Cef.GetGlobalRequestContext();
                if (sourceCtx == null) return;

                var result = ZidimiMessageBox.Show(
                    string.Format(LanguageManager.Instance["Pref_ConfirmCopyData"] ?? "Copy Preferences & Cookies from '{0}' to the current profile?", selectedProfile),
                    "Zidimi Browser", ZidimiMessageBoxButton.YesNo, ZidimiMessageBoxImage.Question, Window.GetWindow(this));

                if (result == ZidimiMessageBoxResult.Yes)
                {
                    await CefProfileDataHelper.CopyAllAsync(sourceCtx, targetCtx);
                    ZidimiMessageBox.Show(
                        LanguageManager.Instance["Pref_CopyComplete"] ?? "Profile data copied successfully!",
                        "Zidimi Browser", ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                }
            };

            var copyPanel = new StackPanel { Orientation = Orientation.Horizontal };
            copyPanel.Children.Add(copyFromCombo);
            copyPanel.Children.Add(new Border { Width = 8 });
            copyPanel.Children.Add(btnCopyFrom);

            panel.Children.Add(CreateSettingRow(
                LanguageManager.Instance["Pref_CopyProfileData"] ?? "Copy Profile Data",
                LanguageManager.Instance["Pref_CopyProfileDesc"] ?? "Copy Preferences & Cookies from another profile",
                copyPanel));
        }

        return panel;
    }

    private UIElement BuildAutofillSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Autofill"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_AutofillDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var btnPasswords = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManagePasswords"], Padding = new Thickness(16,8,16,8) };
        btnPasswords.Click += (s, e) => { var w = new DataManagerWindow("passwords") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PasswordManager"], LanguageManager.Instance["Pref_PasswordDesc"], btnPasswords));

        var btnCards = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManagePayments"], Padding = new Thickness(16,8,16,8) };
        btnCards.Click += (s, e) => { var w = new DataManagerWindow("cards") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PaymentMethods"], LanguageManager.Instance["Pref_PaymentDesc"], btnCards));

        var btnAddress = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManageAddresses"], Padding = new Thickness(16,8,16,8) };
        btnAddress.Click += (s, e) => { var w = new DataManagerWindow("addresses") { Owner = Window.GetWindow(this) }; w.ShowDialog(); };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_AddressAndMore"], LanguageManager.Instance["Pref_AddressDesc"], btnAddress));

        return panel;
    }

    private UIElement BuildDefaultBrowserSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_DefaultBrowser"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_MakeDefault"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var btnDefault = new ZidimiButton { Content = LanguageManager.Instance["Pref_SetDefault"], Style = (Style)FindResource("ZidimiButtonPrimary"), Padding = new Thickness(16,8,16,8) };
        btnDefault.Click += (s, e) => 
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
            }
            catch
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "ms-settings:defaultapps"); }
                catch (Exception ex)
                {
                    ZidimiMessageBox.Show(LanguageManager.Instance["Pref_WinSettingsError"] + ex.Message, LanguageManager.Instance["Pref_Error"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Error, Window.GetWindow(this));
                }
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
            (Key: "classic", Label: LanguageManager.Instance["Pref_ThemeClassic"]),
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

        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        var fontSizes = new[] { LanguageManager.Instance["Pref_SizeSmall"], LanguageManager.Instance["Pref_SizeMedium"], LanguageManager.Instance["Pref_SizeLarge"], LanguageManager.Instance["Pref_SizeExtraLarge"] };
        var currentFontSize = ctx.GetPreferenceSafe("webkit.webprefs.default_font_size");
        int size = 16;
        if (currentFontSize is int i) size = i;
        var idxFont = size switch { <= 12 => 0, 16 => 1, 20 => 2, >= 24 => 3, _ => 1 };
        var fontCombo = MakeCombo(180, idxFont, fontSizes);
        fontCombo.SelectionChanged += (s, e) => 
        { 
            int newSize = fontCombo.SelectedIndex switch { 0 => 12, 1 => 16, 2 => 20, 3 => 24, _ => 16 };
            ctx.SetPreferenceSafe("webkit.webprefs.default_font_size", newSize);
            ctx.SetPreferenceSafe("webkit.webprefs.default_fixed_font_size", newSize - 3);
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_FontSize"], LanguageManager.Instance["Pref_DefaultFontSize"], fontCombo));

        var zooms = new[] { "25%", "50%", "75%", "90%", "100%", "110%", "125%", "150%", "200%" };
        var zoomLevels = new[] { -1.5, -1.0, -0.5, -0.2, 0.0, 0.5, 1.0, 1.5, 2.0 }; // CefSharp ZoomLevels are approx these values
        var currentZoomPref = ctx.GetPreferenceSafe("partition.default_zoom_level");
        double z = 0.0;
        if (currentZoomPref is double d) z = d;
        
        // Find nearest index
        int idxZoom = 4;
        double minDiff = double.MaxValue;
        for (int j = 0; j < zoomLevels.Length; j++)
        {
            double diff = Math.Abs(zoomLevels[j] - z);
            if (diff < minDiff)
            {
                minDiff = diff;
                idxZoom = j;
            }
        }

        var zoomCombo = MakeCombo(140, idxZoom, zooms);
        zoomCombo.SelectionChanged += (s, e) => 
        {
            if (zoomCombo.SelectedIndex >= 0 && zoomCombo.SelectedIndex < zoomLevels.Length)
            {
                double newZoom = zoomLevels[zoomCombo.SelectedIndex];
                ctx.SetPreferenceSafe("partition.default_zoom_level", newZoom);
                
                // Apply immediately to the active web tab (if any).
                var activeTab = App.ViewModel?.ActiveTab;
                if (activeTab != null)
                {
                    var b = App.ViewModel?.GetBrowser(activeTab) as CefSharp.Wpf.HwndHost.ChromiumWebBrowser;
                    b?.SetZoomLevel(newZoom);
                }
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
            AppSettings.Profile.SearchEngine = searchCombo.SelectedItem is ZidimiComboBoxItem hcbi
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

        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        bool block3rd = false;
        if (ctx.GetPreferenceSafe("profile.cookie_controls_mode") is int c) block3rd = c == 1;
        var chkCookie = MakeCheck(LanguageManager.Instance["Pref_BlockThirdPartyCookies"], block3rd);
        chkCookie.Checked += (s, e) => { ctx.SetPreferenceSafe("profile.cookie_controls_mode", 1); };
        chkCookie.Unchecked += (s, e) => { ctx.SetPreferenceSafe("profile.cookie_controls_mode", 0); };
        panel.Children.Add(CreateSettingRow("", "", chkCookie));

        bool doNotTrack = false;
        if (ctx.GetPreferenceSafe("enable_do_not_track") is bool dnt) doNotTrack = dnt;
        var chkDnt = MakeCheck(LanguageManager.Instance["Pref_DoNotTrack"], doNotTrack);
        chkDnt.Checked += (s, e) => { ctx.SetPreferenceSafe("enable_do_not_track", true); };
        chkDnt.Unchecked += (s, e) => { ctx.SetPreferenceSafe("enable_do_not_track", false); };
        panel.Children.Add(CreateSettingRow("", "", chkDnt));

        bool safeBrowsing = true;
        if (ctx.GetPreferenceSafe("safebrowsing.enabled") is bool sb) safeBrowsing = sb;
        var chkSafe = MakeCheck(LanguageManager.Instance["Pref_SafeBrowsing"], safeBrowsing);
        chkSafe.Checked += (s, e) => { ctx.SetPreferenceSafe("safebrowsing.enabled", true); };
        chkSafe.Unchecked += (s, e) => { ctx.SetPreferenceSafe("safebrowsing.enabled", false); };
        panel.Children.Add(CreateSettingRow("", "", chkSafe));

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

        void Row(string permKey, string key, string? cefKey = null)
        {
            var label = LanguageManager.Instance[permKey];
            var descKey = permKey + "_Desc";
            var desc = LanguageManager.Instance[descKey];
            if (desc == descKey) desc = string.Empty;

            var value = (ContentPermission)typeof(SitePermissions).GetProperty(key)!.GetValue(perms)!;
            var combo = MakeCombo(160, (int)value, ask, allow, block);
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                typeof(SitePermissions).GetProperty(key)!.SetValue(perms, (ContentPermission)combo.SelectedIndex);
                AppSettings.SaveAll();

                if (cefKey != null)
                {
                    var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
                    // Cef values: 1 = Allow, 2 = Block, 3 = Ask
                    int cefVal = combo.SelectedIndex == 1 ? 1 : (combo.SelectedIndex == 2 ? 2 : 3);
                    ctx.SetPreferenceSafe("profile.default_content_setting_values." + cefKey, cefVal);
                }
            };
            panel.Children.Add(CreateSettingRow(label, desc, combo));
        }

        Row("Perm_Camera", nameof(SitePermissions.Camera), "media_stream_camera");
        Row("Perm_Microphone", nameof(SitePermissions.Microphone), "media_stream_mic");
        Row("Perm_Location", nameof(SitePermissions.Geolocation), "geolocation");
        Row("Perm_Notifications", nameof(SitePermissions.Notifications), "notifications");
        Row("Perm_Clipboard", nameof(SitePermissions.Clipboard), "clipboard");
        Row("Perm_PointerLock", nameof(SitePermissions.PointerLock), "mouselock");
        Row("Perm_Midi", nameof(SitePermissions.MidiSysex), "midi_sysex");
        Row("Perm_FileSystem", nameof(SitePermissions.FileSystemAccess), "file_system_write_guard");
        Row("Perm_IdleDetection", nameof(SitePermissions.IdleDetection), "idle_detection");
        Row("Perm_LocalFonts", nameof(SitePermissions.LocalFonts), "local_fonts");
        Row("Perm_MultipleDownloads", nameof(SitePermissions.MultipleDownloads), "automatic_downloads");
        Row("Perm_WindowManagement", nameof(SitePermissions.WindowManagement), "window_placement");
        Row("Perm_KeyboardLock", nameof(SitePermissions.KeyboardLock));
        Row("Perm_ProtectedMedia", nameof(SitePermissions.ProtectedMedia), "protected_media_identifier");
        Row("Perm_HandTracking", nameof(SitePermissions.HandTracking));
        Row("Perm_CameraPanTilt", nameof(SitePermissions.CameraPanTiltZoom));
        Row("Perm_CapturedSurface", nameof(SitePermissions.CapturedSurfaceControl));
        Row("Perm_StorageAccess", nameof(SitePermissions.StorageAccess));
        Row("Perm_TopLevelStorage", nameof(SitePermissions.TopLevelStorageAccess));
        Row("Perm_DiskQuota", nameof(SitePermissions.DiskQuota));
        Row("Perm_Vr", nameof(SitePermissions.VrSession), "vr");
        Row("Perm_Ar", nameof(SitePermissions.ArSession), "ar");
        Row("Perm_ProtocolHandler", nameof(SitePermissions.RegisterProtocolHandler));
        Row("Perm_WebAppInstall", nameof(SitePermissions.WebAppInstallation));
        Row("Perm_IdentityProvider", nameof(SitePermissions.IdentityProvider));
        Row("Perm_LocalNetworkAccess", nameof(SitePermissions.LocalNetworkAccess));
        Row("Perm_LocalNetwork", nameof(SitePermissions.LocalNetwork));
        Row("Perm_LoopbackNetwork", nameof(SitePermissions.LoopbackNetwork));

        var blockPopupsLabel = LanguageManager.Instance["Pref_BlockPopups"];
        if (blockPopupsLabel == "Pref_BlockPopups") blockPopupsLabel = LanguageManager.Instance["Pref_BlockPopups"] != "Pref_BlockPopups" ? LanguageManager.Instance["Pref_BlockPopups"] : "Chặn cửa sổ bật lên";
        var chkPopups = MakeCheck(blockPopupsLabel, AppSettings.Profile.SitePermissions.BlockPopups);
        chkPopups.Checked += (s, e) => { 
            AppSettings.Profile.SitePermissions.BlockPopups = true; 
            AppSettings.SaveAll(); 
            var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
            ctx.SetPreferenceSafe("profile.default_content_setting_values.popups", 2);
        };
        chkPopups.Unchecked += (s, e) => { 
            AppSettings.Profile.SitePermissions.BlockPopups = false; 
            AppSettings.SaveAll(); 
            var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
            ctx.SetPreferenceSafe("profile.default_content_setting_values.popups", 1);
        };
        var popupDesc = LanguageManager.Instance["Pref_Popups_Desc"];
        if (popupDesc == "Pref_Popups_Desc") popupDesc = string.Empty;
        var popupTitle = LanguageManager.Instance["Pref_Popups"];
        if (popupTitle == "Pref_Popups") popupTitle = "Pop-up";
        panel.Children.Add(CreateSettingRow(popupTitle, popupDesc, chkPopups));

        var btnExceptionsText = LanguageManager.Instance["Pref_ManageExceptions"];
        if (btnExceptionsText == "Pref_ManageExceptions") btnExceptionsText = "Quản lý ngoại lệ";
        var btnExceptions = MakeButton(btnExceptionsText, 180);
        btnExceptions.Click += (s, e) => {
            var w = new SiteExceptionsWindow { Owner = Window.GetWindow(this) };
            w.ShowDialog();
        };
        var excDesc = LanguageManager.Instance["Pref_Exceptions_Desc"];
        if (excDesc == "Pref_Exceptions_Desc") excDesc = string.Empty;
        var excTitle = LanguageManager.Instance["Pref_Exceptions"];
        if (excTitle == "Pref_Exceptions") excTitle = "Ngoại lệ trang web";
        panel.Children.Add(CreateSettingRow(excTitle, excDesc, btnExceptions));

        return panel;
    }

    private UIElement BuildDownloadsSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Downloads_Title"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ManageDownloads"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        string currentDlPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
        if (ctx.GetPreferenceSafe("download.default_directory") is string p && !string.IsNullOrEmpty(p)) currentDlPath = p;

        var tbDownload = new TextBox { Text = currentDlPath, IsReadOnly = true, FontSize = 13 };

        var btnBrowse = MakeButton(LanguageManager.Instance["Pref_ChooseFolder"], 130);
        btnBrowse.Click += (s, e) => 
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = System.IO.Directory.Exists(currentDlPath)
                    ? currentDlPath
                    : System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                Title = LanguageManager.Instance["Pref_ChooseDownloadFolder"],
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            {
                currentDlPath = dlg.FolderName;
                ctx.SetPreferenceSafe("download.default_directory", currentDlPath);
                tbDownload.Text = currentDlPath;
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
            try { System.Diagnostics.Process.Start("explorer.exe", currentDlPath); }
            catch { }
        };
        panel.Children.Add(CreateSettingRow("", "", btnOpen));

        bool askBeforeSave = true;
        if (ctx.GetPreferenceSafe("download.prompt_for_download") is bool ask) askBeforeSave = ask;
        var chkAsk = MakeCheck(LanguageManager.Instance["Pref_AskWhereToSave"], askBeforeSave);
        chkAsk.Checked += (s, e) => { ctx.SetPreferenceSafe("download.prompt_for_download", true); };
        chkAsk.Unchecked += (s, e) => { ctx.SetPreferenceSafe("download.prompt_for_download", false); };
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
            if (langCombo.SelectedItem is ZidimiComboBoxItem hcbi)
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

        var stableCheck = MakeCheck(LanguageManager.Instance["Pref_StableRendering"], AppSettings.Global.StableRendering);
        stableCheck.Checked += (s, e) => { AppSettings.Global.StableRendering = true; AppSettings.SaveAll(); };
        stableCheck.Unchecked += (s, e) => { AppSettings.Global.StableRendering = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", stableCheck));

        var throttlingCheck = MakeCheck(LanguageManager.Instance["Pref_DisableBackgroundThrottling"], AppSettings.Global.DisableBackgroundThrottling);
        throttlingCheck.Checked += (s, e) => { AppSettings.Global.DisableBackgroundThrottling = true; AppSettings.SaveAll(); };
        throttlingCheck.Unchecked += (s, e) => { AppSettings.Global.DisableBackgroundThrottling = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", throttlingCheck));

        var sandboxCheck = MakeCheck(LanguageManager.Instance["Pref_DisableSandbox"], AppSettings.Global.DisableSandbox);
        sandboxCheck.Checked += (s, e) => { AppSettings.Global.DisableSandbox = true; AppSettings.SaveAll(); };
        sandboxCheck.Unchecked += (s, e) => { AppSettings.Global.DisableSandbox = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", sandboxCheck));

        var cefLogCheck = MakeCheck(LanguageManager.Instance["Pref_CefLog"], AppSettings.Global.CefLogEnabled);
        cefLogCheck.Checked += (s, e) => { AppSettings.Global.CefLogEnabled = true; AppSettings.SaveAll(); };
        cefLogCheck.Unchecked += (s, e) => { AppSettings.Global.CefLogEnabled = false; AppSettings.SaveAll(); };
        panel.Children.Add(CreateSettingRow("", "", cefLogCheck));

        var btnProxy = MakeButton(LanguageManager.Instance["Pref_OpenProxySettings"], 200);
        btnProxy.Click += (s, e) => 
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:network-proxy") { UseShellExecute = true });
            }
            catch
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "ms-settings:network-proxy"); }
                catch (Exception ex)
                {
                    ZidimiMessageBox.Show(LanguageManager.Instance["Pref_ProxyError"] + ex.Message, LanguageManager.Instance["Pref_Error"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Error, Window.GetWindow(this));
                }
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
            (LanguageManager.Instance["Pref_Version"], "Zidimi Browser " + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0")),
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
            ZidimiMessageBox.Show(LanguageManager.Instance["Pref_UpToDate"], LanguageManager.Instance["Pref_Update"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
        };
        btnCheck.Margin = new Thickness(0, 16, 0, 0);
        panel.Children.Add(btnCheck);

        return panel;
    }

    private Border CreateSettingRow(string label, string desc, UIElement control)
    {
        var border = new Border { Style = (Style)FindResource("CardPanel"), Margin = new Thickness(0, 0, 0, 12) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        if (!string.IsNullOrEmpty(label))
            stack.Children.Add(new TextBlock { Text = label, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Ink100Brush"), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(desc))
            stack.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

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
            {
                fe.HorizontalAlignment = HorizontalAlignment.Right;
                fe.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        if (control is Button btnTarget)
        {
            border.Cursor = System.Windows.Input.Cursors.Hand;
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (btnTarget.IsEnabled && !e.Handled)
                {
                    var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(btnTarget);
                    var provider = peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) as System.Windows.Automation.Provider.IInvokeProvider;
                    provider?.Invoke();
                }
            };
        }

        border.Child = grid;
        return border;
    }

    private static ZidimiComboBox MakeCombo(double width, params string[] items)
        => MakeCombo(width, selectedIndex: 0, items);

    private static ZidimiComboBox MakeCombo(double width, int selectedIndex, params string[] items)
    {
        var combo = new ZidimiComboBox { Width = width };
        foreach (var item in items)
            combo.Items.Add(new ZidimiComboBoxItem { Content = item });
        combo.SelectedIndex = selectedIndex;
        return combo;
    }

    private static ZidimiCheckBox MakeCheck(string label, bool isChecked)
        => new() { Content = label, IsChecked = isChecked };

    private static ZidimiButton MakeButton(string content, double width)
    {
        var btn = new ZidimiButton
        {
            Content = content,
            Width = width,
            Style = (Style)Application.Current.Resources["ZidimiButtonPrimary"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        return btn;
    }
}

