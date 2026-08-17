using System.IO;
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
    private bool _syncingSection;

    /// <summary>Raised when the user changes the Settings sidebar section.</summary>
    public event Action<string>? SectionChanged;

    public PreferencesView()
    {
        InitializeComponent();
        // Rebuild the section being viewed when the theme changes so code-built labels pick up the new brushes.
        ThemeManager.ThemeChanged += OnThemeChanged;
        Loaded += PreferencesView_Loaded;
        Unloaded += (s, e) =>
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            Loaded -= PreferencesView_Loaded;
        };
        if (SettingsContent.Content == null)
            LoadSettingsSection("General");
    }

    private async void PreferencesView_Loaded(object sender, RoutedEventArgs e)
    {
        // After CEF starts, the live RequestContext is the source of truth. Refresh once when the
        // settings surface opens so changes made by Chromium/internal pages/extensions are visible.
        await AppSettings.RefreshCurrentProfileFromCefAsync();
        if (IsLoaded) LoadSettingsSection(_currentSection);
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
        if (_syncingSection || SettingsContent == null) return;
        if (sender is RadioButton rb && rb.Tag is string tag)
            NavigateToSection(tag, notifyRoute: true);
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (_syncingSection || SettingsContent == null) return;
        // Checked already performs navigation when the selected radio changes.
        // Only handle Click when a custom radio implementation did not check itself.
        if (sender is RadioButton rb && rb.IsChecked != true && rb.Tag is string tag)
            NavigateToSection(tag, notifyRoute: true);
    }

    /// <summary>
    /// Selects a Settings sidebar section. BrowserView calls this for
    /// zidimi://settings/&lt;section&gt; navigation; sidebar clicks can optionally
    /// notify BrowserView so the omnibox URL stays in sync.
    /// </summary>
    public void NavigateToSection(string section, bool notifyRoute = false)
    {
        if (SettingsContent == null) return;

        var normalized = string.IsNullOrWhiteSpace(section) ? "Profiles" : section.Trim();
        _syncingSection = true;
        try
        {
            foreach (var child in NavPanel.Children)
            {
                if (child is RadioButton rb && rb.Tag is string tag)
                    rb.IsChecked = string.Equals(tag, normalized, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _syncingSection = false;
        }

        LoadSettingsSection(normalized);
        if (notifyRoute)
            SectionChanged?.Invoke(normalized);
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
        tbHome.LostKeyboardFocus += (s, e) =>
        {
            var value = tbHome.Text.Trim();
            if (string.Equals(value.TrimEnd('/'), "chrome://newtab", StringComparison.OrdinalIgnoreCase))
            {
                AppSettings.Profile.HomePageUrl = "chrome://newtab/";
                tbHome.Text = AppSettings.Profile.HomePageUrl;
                AppSettings.SaveProfile();
                return;
            }
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                tbHome.Text = AppSettings.Profile.HomePageUrl;
                return;
            }

            AppSettings.Profile.HomePageUrl = uri.AbsoluteUri;
            tbHome.Text = AppSettings.Profile.HomePageUrl;
            AppSettings.SaveProfile();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_StartupPage"], LanguageManager.Instance["Pref_HomeUrl"], tbHome));

        var searchButton = MakeButton(LanguageManager.Instance["Pref_SearchEngineTitle"], 200);
        searchButton.Click += (s, e) => OpenChromiumSettings("chrome://settings/searchEngines");
        panel.Children.Add(CreateSettingRow(
            LanguageManager.Instance["Pref_DefaultEngine"],
            AppSettings.Profile.SearchEngine,
            searchButton));

        var startupCombo = MakeCombo(280, AppSettings.Profile.StartupBehavior, LanguageManager.Instance["Pref_StartupNewPage"], LanguageManager.Instance["Pref_StartupContinue"], LanguageManager.Instance["Pref_StartupSpecific"]);
        startupCombo.SelectionChanged += (s, e) => { AppSettings.Profile.StartupBehavior = startupCombo.SelectedIndex; AppSettings.SaveProfile(); };
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
        tbPages.LostKeyboardFocus += (s, e) =>
        {
            // Persist once after editing instead of rewriting JSON on every keystroke in the
            // multi-line startup-page editor.
            AppSettings.Profile.StartupPages = tbPages.Text
                .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
            AppSettings.SaveProfile();
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_SpecificPages"], LanguageManager.Instance["Pref_OnePerLine"], tbPages));

        return panel;
    }

    private UIElement BuildProfilesSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock
        {
            Text = LanguageManager.Instance["Pref_Profile"],
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Ink100Brush"),
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(new TextBlock
        {
            Text = LanguageManager.Instance["Pref_ProfileDesc"],
            Foreground = (Brush)FindResource("Ink400Brush"),
            Margin = new Thickness(0, 0, 0, 20),
        });

        // The Chromium folder id (Default/Profile N) is the profile identity. Friendly
        // names are presentation only; settings never use localized names as folder keys.
        var profiles = ChromiumProfileCatalog.GetProfiles(AppSettings.Global.Profiles).ToArray();
        var currentIndex = Array.FindIndex(profiles, p =>
            string.Equals(p.Id, AppSettings.Global.CurrentProfile, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0) currentIndex = 0;
        var current = profiles[currentIndex];

        var identityCard = new Border
        {
            Background = (Brush)FindResource("ZidimiBgSurfaceBrush"),
            BorderBrush = (Brush)FindResource("StrokeBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 16),
        };
        var identityGrid = new Grid();
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var avatar = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = (Brush)FindResource("CtaBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(current.DisplayName) ? "Z" : current.DisplayName[..1].ToUpperInvariant(),
                FontWeight = FontWeights.SemiBold,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("OnAccentBrush"),
            },
        };
        identityGrid.Children.Add(avatar);

        var identityText = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        identityText.Children.Add(new TextBlock
        {
            Text = current.DisplayName,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Ink100Brush"),
        });
        if (!string.IsNullOrWhiteSpace(current.UserName))
        {
            identityText.Children.Add(new TextBlock
            {
                Text = current.UserName,
                FontSize = 12,
                Foreground = (Brush)FindResource("Ink400Brush"),
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        identityText.Children.Add(new TextBlock
        {
            Text = $@"User Data\{current.Id}",
            FontSize = 11,
            Foreground = (Brush)FindResource("Ink500Brush"),
            Margin = new Thickness(0, 3, 0, 0),
        });
        Grid.SetColumn(identityText, 1);
        identityGrid.Children.Add(identityText);
        identityCard.Child = identityGrid;
        panel.Children.Add(identityCard);

        var profileLabels = profiles.Select(FormatProfileLabel).ToArray();
        var profileCombo = MakeCombo(260, currentIndex, profileLabels);
        profileCombo.SelectionChanged += (s, e) =>
        {
            if (profileCombo.SelectedIndex < 0 || profileCombo.SelectedIndex >= profiles.Length) return;
            var selected = profiles[profileCombo.SelectedIndex];
            if (string.Equals(AppSettings.Global.CurrentProfile, selected.Id, StringComparison.OrdinalIgnoreCase))
                return;

            AppSettings.Global.CurrentProfile = selected.Id;
            AppSettings.LoadProfile(selected.Id);
            AppSettings.SaveAll();
            App.ViewModel?.SwitchProfile(selected.Id);
            ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
            LoadSettingsSection("Profiles");
        };

        var btnManageProfile = new ZidimiButton
        {
            Content = LanguageManager.Instance["Pref_ManageProfile"],
            Padding = new Thickness(16, 8, 16, 8),
        };
        btnManageProfile.Click += (s, e) =>
        {
            new ProfileSelectorWindow { Owner = Window.GetWindow(this) }.ShowDialog();
            LoadSettingsSection("Profiles");
        };

        var profilePanel = new StackPanel { Orientation = Orientation.Horizontal };
        profilePanel.Children.Add(profileCombo);
        profilePanel.Children.Add(new Border { Width = 8 });
        profilePanel.Children.Add(btnManageProfile);
        panel.Children.Add(CreateSettingRow(
            LanguageManager.Instance["Pref_CurrentProfile"],
            LanguageManager.Instance["Pref_ProfileApplyDesc"],
            profilePanel));

        var otherProfiles = profiles
            .Where(p => !string.Equals(p.Id, AppSettings.Global.CurrentProfile, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (otherProfiles.Length > 0)
        {
            var copyFromCombo = MakeCombo(260, 0, otherProfiles.Select(FormatProfileLabel).ToArray());
            var btnCopyFrom = new ZidimiButton
            {
                Content = LanguageManager.Instance["Pref_CopyFromProfile"],
                Padding = new Thickness(16, 8, 16, 8),
            };
            btnCopyFrom.Click += async (s, e) =>
            {
                if (copyFromCombo.SelectedIndex < 0 || copyFromCombo.SelectedIndex >= otherProfiles.Length) return;
                var selectedProfile = otherProfiles[copyFromCombo.SelectedIndex];

                var sourceCtx = App.RequestContexts.GetProfileContext(selectedProfile.Id);
                var targetCtx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile)
                                ?? Cef.GetGlobalRequestContext();
                if (sourceCtx is null) return;

                var result = ZidimiMessageBox.Show(
                    string.Format(LanguageManager.Instance["Pref_ConfirmCopyData"], selectedProfile.DisplayName),
                    "Zidimi Browser", ZidimiMessageBoxButton.YesNo, ZidimiMessageBoxImage.Question,
                    Window.GetWindow(this));

                if (result != ZidimiMessageBoxResult.Yes) return;

                await CefProfileDataHelper.CopyAllAsync(sourceCtx, targetCtx);
                ZidimiMessageBox.Show(
                    LanguageManager.Instance["Pref_CopyComplete"],
                    "Zidimi Browser", ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information,
                    Window.GetWindow(this));
            };

            var copyPanel = new StackPanel { Orientation = Orientation.Horizontal };
            copyPanel.Children.Add(copyFromCombo);
            copyPanel.Children.Add(new Border { Width = 8 });
            copyPanel.Children.Add(btnCopyFrom);
            panel.Children.Add(CreateSettingRow(
                LanguageManager.Instance["Pref_CopyProfileData"],
                LanguageManager.Instance["Pref_CopyProfileDesc"],
                copyPanel));
        }

        return panel;
    }

    private static string FormatProfileLabel(ChromiumProfileCatalog.ProfileInfo profile)
        => string.Equals(profile.DisplayName, profile.Id, StringComparison.OrdinalIgnoreCase)
            ? profile.DisplayName
            : $"{profile.DisplayName}  ·  {profile.Id}";

    private UIElement BuildAutofillSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Autofill"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_AutofillDesc"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        // Chromium owns encryption and schema migrations for passwords/autofill.
        // Use its native UI for real CRUD instead of editing Web Data/Login Data directly.
        var btnPasswords = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManagePasswords"], Padding = new Thickness(16, 8, 16, 8) };
        btnPasswords.Click += (s, e) => OpenChromiumSettings("chrome://password-manager/passwords");
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PasswordManager"], LanguageManager.Instance["Pref_PasswordDesc"], btnPasswords));

        var btnCards = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManagePayments"], Padding = new Thickness(16, 8, 16, 8) };
        btnCards.Click += (s, e) => OpenChromiumSettings("chrome://settings/payments");
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_PaymentMethods"], LanguageManager.Instance["Pref_PaymentDesc"], btnCards));

        var btnAddress = new ZidimiButton { Content = LanguageManager.Instance["Pref_ManageAddresses"], Padding = new Thickness(16, 8, 16, 8) };
        btnAddress.Click += (s, e) => OpenChromiumSettings("chrome://settings/addresses");
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
        panel.Children.Add(CreateSettingRow(
            LanguageManager.Instance["Pref_DefaultBrowser"],
            LanguageManager.Instance["Pref_DefaultBrowserManaged"],
            btnDefault));

        return panel;
    }

    private UIElement BuildAppearanceSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_Appearance"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_CustomizeAppearance"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var themeOptions = new[]
        {
            (Key: "system", Label: LanguageManager.Instance["Pref_SystemTitle"]),
            (Key: "light", Label: LanguageManager.Instance["Pref_ThemeLight"]),
            (Key: "dark", Label: LanguageManager.Instance["Pref_ThemeDark"]),
        };
        var currentTheme = Infrastructure.ThemeManager.NormalizeThemeKey(AppSettings.Profile.Theme);
        var idxTheme = Array.FindIndex(themeOptions, o => o.Key == currentTheme);
        var themeCombo = MakeCombo(180, Math.Max(0, idxTheme), themeOptions.Select(o => o.Label).ToArray());
        themeCombo.SelectionChanged += (s, e) => 
        {
            if (themeCombo.SelectedIndex >= 0 && themeCombo.SelectedIndex < themeOptions.Length)
            {
                AppSettings.Profile.Theme = themeOptions[themeCombo.SelectedIndex].Key;
                Infrastructure.ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
                // browser.theme.color_scheme2 is a real Chromium profile preference.
                AppSettings.SaveProfile();
            }
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_Theme"], LanguageManager.Instance["Pref_SelectTheme"], themeCombo));

        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        var fontSizes = new[] { LanguageManager.Instance["Pref_SizeSmall"], LanguageManager.Instance["Pref_SizeMedium"], LanguageManager.Instance["Pref_SizeLarge"], LanguageManager.Instance["Pref_SizeExtraLarge"] };
        var currentFontSize = ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DefaultFontSize);
        int size = 16;
        if (currentFontSize is int i) size = i;
        var idxFont = size switch { <= 12 => 0, 16 => 1, 20 => 2, >= 24 => 3, _ => 1 };
        var fontCombo = MakeCombo(180, idxFont, fontSizes);
        fontCombo.SelectionChanged += async (s, e) =>
        {
            int newSize = fontCombo.SelectedIndex switch { 0 => 12, 1 => 16, 2 => 20, 3 => 24, _ => 16 };
            await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DefaultFontSize, newSize);
            await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DefaultFixedFontSize, newSize - 3);
        };
        panel.Children.Add(CreateSettingRow(LanguageManager.Instance["Pref_FontSize"], LanguageManager.Instance["Pref_DefaultFontSize"], fontCombo));

        var zooms = new[] { "25%", "50%", "75%", "90%", "100%", "110%", "125%", "150%", "200%" };
        var zoomLevels = new[] { -1.5, -1.0, -0.5, -0.2, 0.0, 0.5, 1.0, 1.5, 2.0 }; // CefSharp ZoomLevels are approx these values
        var currentZoomPref = ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DefaultZoomLevel);
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
        zoomCombo.SelectionChanged += async (s, e) =>
        {
            if (zoomCombo.SelectedIndex >= 0 && zoomCombo.SelectedIndex < zoomLevels.Length)
            {
                double newZoom = zoomLevels[zoomCombo.SelectedIndex];
                await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DefaultZoomLevel, newZoom);

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

        var searchButton = MakeButton(LanguageManager.Instance["Pref_SearchEngineTitle"], 200);
        searchButton.Click += (s, e) => OpenChromiumSettings("chrome://settings/searchEngines");
        panel.Children.Add(CreateSettingRow(
            LanguageManager.Instance["Pref_DefaultEngine"],
            AppSettings.Profile.SearchEngine,
            searchButton));

        var suggestCheck = MakeCheck(LanguageManager.Instance["Pref_ShowSearchSuggestions"], AppSettings.Profile.SearchSuggestEnabled);
        suggestCheck.Checked += (s, e) => { AppSettings.Profile.SearchSuggestEnabled = true; AppSettings.SaveProfile(); };
        suggestCheck.Unchecked += (s, e) => { AppSettings.Profile.SearchSuggestEnabled = false; AppSettings.SaveProfile(); };
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
        if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.CookieControlsMode) is int c) block3rd = c == 1;
        var chkCookie = MakeCheck(LanguageManager.Instance["Pref_BlockThirdPartyCookies"], block3rd);
        chkCookie.Checked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.CookieControlsMode, 1); };
        chkCookie.Unchecked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.CookieControlsMode, 0); };
        panel.Children.Add(CreateSettingRow("", "", chkCookie));

        bool doNotTrack = false;
        if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.EnableDoNotTrack) is bool dnt) doNotTrack = dnt;
        var chkDnt = MakeCheck(LanguageManager.Instance["Pref_DoNotTrack"], doNotTrack);
        chkDnt.Checked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.EnableDoNotTrack, true); };
        chkDnt.Unchecked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.EnableDoNotTrack, false); };
        panel.Children.Add(CreateSettingRow("", "", chkDnt));

        bool safeBrowsing = true;
        if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.SafeBrowsingEnabled) is bool sb) safeBrowsing = sb;
        var chkSafe = MakeCheck(LanguageManager.Instance["Pref_SafeBrowsing"], safeBrowsing);
        chkSafe.Checked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.SafeBrowsingEnabled, true); };
        chkSafe.Unchecked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.SafeBrowsingEnabled, false); };
        panel.Children.Add(CreateSettingRow("", "", chkSafe));

        var btnClear = MakeButton(LanguageManager.Instance["Pref_ClearBrowsingDataBtn"], 200);
        btnClear.Click += (s, e) =>
        {
            App.ViewModel.NewTab("chrome://settings/clearBrowserData");
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
        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
        var ask = LanguageManager.Instance["Perm_Ask"];
        var allow = LanguageManager.Instance["Perm_Allow"];
        var block = LanguageManager.Instance["Perm_Block"];

        void Row(string permKey, string propertyName)
        {
            var label = LanguageManager.Instance[permKey];
            var descKey = permKey + "_Desc";
            var desc = LanguageManager.Instance[descKey];
            if (desc == descKey) desc = string.Empty;

            var property = typeof(SitePermissions).GetProperty(propertyName);
            if (property == null) return;

            var value = (ContentPermission)(property.GetValue(perms) ?? ContentPermission.Ask);
            var combo = MakeCombo(160, (int)value, ask, allow, block);
            combo.SelectionChanged += async (s, e) =>
            {
                if (combo.SelectedIndex < 0) return;
                var permission = (ContentPermission)combo.SelectedIndex;
                property.SetValue(perms, permission);

                // CEF-first persistence where a public ContentSettingTypes mapping exists.
                // Unsupported permission kinds intentionally remain Zidimi policy instead of
                // writing undocumented Chromium preference keys.
                if (CefContentSettingsBridge.TryGetContentType(propertyName, out var contentType))
                    await CefContentSettingsBridge.SetDefaultAsync(ctx, contentType, permission);
            };
            panel.Children.Add(CreateSettingRow(label, desc, combo));
        }

        Row("Perm_Camera", nameof(SitePermissions.Camera));
        Row("Perm_Microphone", nameof(SitePermissions.Microphone));
        Row("Perm_Location", nameof(SitePermissions.Geolocation));
        Row("Perm_Notifications", nameof(SitePermissions.Notifications));
        Row("Perm_Clipboard", nameof(SitePermissions.Clipboard));
        Row("Perm_PointerLock", nameof(SitePermissions.PointerLock));
        Row("Perm_Midi", nameof(SitePermissions.MidiSysex));
        Row("Perm_FileSystem", nameof(SitePermissions.FileSystemAccess));
        Row("Perm_IdleDetection", nameof(SitePermissions.IdleDetection));
        Row("Perm_LocalFonts", nameof(SitePermissions.LocalFonts));
        Row("Perm_MultipleDownloads", nameof(SitePermissions.MultipleDownloads));
        Row("Perm_WindowManagement", nameof(SitePermissions.WindowManagement));
        Row("Perm_KeyboardLock", nameof(SitePermissions.KeyboardLock));
        Row("Perm_ProtectedMedia", nameof(SitePermissions.ProtectedMedia));
        Row("Perm_HandTracking", nameof(SitePermissions.HandTracking));
        Row("Perm_CameraPanTilt", nameof(SitePermissions.CameraPanTiltZoom));
        Row("Perm_CapturedSurface", nameof(SitePermissions.CapturedSurfaceControl));
        Row("Perm_StorageAccess", nameof(SitePermissions.StorageAccess));
        Row("Perm_TopLevelStorage", nameof(SitePermissions.TopLevelStorageAccess));
        Row("Perm_DiskQuota", nameof(SitePermissions.DiskQuota));
        Row("Perm_Vr", nameof(SitePermissions.VrSession));
        Row("Perm_Ar", nameof(SitePermissions.ArSession));
        Row("Perm_ProtocolHandler", nameof(SitePermissions.RegisterProtocolHandler));
        Row("Perm_WebAppInstall", nameof(SitePermissions.WebAppInstallation));
        Row("Perm_IdentityProvider", nameof(SitePermissions.IdentityProvider));
        Row("Perm_LocalNetworkAccess", nameof(SitePermissions.LocalNetworkAccess));
        Row("Perm_LocalNetwork", nameof(SitePermissions.LocalNetwork));
        Row("Perm_LoopbackNetwork", nameof(SitePermissions.LoopbackNetwork));

        var blockPopupsLabel = LanguageManager.Instance["Pref_BlockPopups"];
        var chkPopups = MakeCheck(blockPopupsLabel, AppSettings.Profile.SitePermissions.BlockPopups);
        chkPopups.Checked += async (s, e) =>
        {
            AppSettings.Profile.SitePermissions.BlockPopups = true;
            await CefContentSettingsBridge.SetPopupBlockingAsync(ctx, true);
        };
        chkPopups.Unchecked += async (s, e) =>
        {
            AppSettings.Profile.SitePermissions.BlockPopups = false;
            await CefContentSettingsBridge.SetPopupBlockingAsync(ctx, false);
        };
        var popupDesc = LanguageManager.Instance["Pref_Popups_Desc"];
        if (popupDesc == "Pref_Popups_Desc") popupDesc = string.Empty;
        var popupTitle = LanguageManager.Instance["Pref_Popups"];
        if (popupTitle == "Pref_Popups") popupTitle = "Pop-up";
        panel.Children.Add(CreateSettingRow(popupTitle, popupDesc, chkPopups));

        var btnExceptionsText = LanguageManager.Instance["Pref_ManageExceptions"];
        var btnExceptions = MakeButton(btnExceptionsText, 180);
        btnExceptions.Click += (s, e) => {
            var w = new SiteExceptionsWindow { Owner = Window.GetWindow(this) };
            w.ShowDialog();
        };
        var excDesc = LanguageManager.Instance["Pref_Exceptions_Desc"];
        if (excDesc == "Pref_Exceptions_Desc") excDesc = string.Empty;
        var excTitle = LanguageManager.Instance["Pref_Exceptions"];
        panel.Children.Add(CreateSettingRow(excTitle, excDesc, btnExceptions));

        return panel;
    }

    private UIElement BuildDownloadsSection()
    {
        var panel = new StackPanel { MinWidth = 600, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Downloads_Title"], FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Ink100Brush"), Margin = new Thickness(0, 0, 0, 16) });
        panel.Children.Add(new TextBlock { Text = LanguageManager.Instance["Pref_ManageDownloads"], Foreground = (Brush)FindResource("Ink400Brush"), Margin = new Thickness(0, 0, 0, 24) });

        var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();

        string currentDlPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
        if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DownloadDefaultDirectory) is string p && !string.IsNullOrEmpty(p)) currentDlPath = p;

        var tbDownload = new TextBox { Text = currentDlPath, IsReadOnly = true, FontSize = 13 };

        var btnBrowse = MakeButton(LanguageManager.Instance["Pref_ChooseFolder"], 130);
        btnBrowse.Click += async (s, e) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = Directory.Exists(currentDlPath)
                    ? currentDlPath
                    : System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                Title = LanguageManager.Instance["Pref_ChooseDownloadFolder"],
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            {
                currentDlPath = dlg.FolderName;
                await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DownloadDefaultDirectory, currentDlPath);
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
            catch (Exception ex) { AppLogger.Log("Downloads", ex, $"Opening download folder '{currentDlPath}'."); }
        };
        panel.Children.Add(CreateSettingRow("", "", btnOpen));

        bool askBeforeSave = true;
        if (ctx.GetPreferenceSafe(ChromiumPreferenceKeys.DownloadPromptForDownload) is bool ask) askBeforeSave = ask;
        var chkAsk = MakeCheck(LanguageManager.Instance["Pref_AskWhereToSave"], askBeforeSave);
        chkAsk.Checked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DownloadPromptForDownload, true); };
        chkAsk.Unchecked += async (s, e) => { await ctx.SetPreferenceSafeAsync(ChromiumPreferenceKeys.DownloadPromptForDownload, false); };
        panel.Children.Add(CreateSettingRow("", "", chkAsk));

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
                    // CurrentLanguage updates AppSettings and applies native Chromium language
                    // preferences through SaveGlobal; do not duplicate the write here.
                    LanguageManager.Instance.CurrentLanguage = selectedLang;
                    
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

        void AddGlobalToggle(string key, Func<bool> get, Action<bool> set, Action onChanged)
        {
            var check = MakeCheck(LanguageManager.Instance[key], get());
            check.Checked += (s, e) => { set(true); onChanged(); };
            check.Unchecked += (s, e) => { set(false); onChanged(); };
            panel.Children.Add(CreateSettingRow("", "", check));
        }

        // Proxy is a live per-profile Chromium preference. It is intentionally not a command-line
        // switch, so toggling this updates the active RequestContext immediately and Chromium saves
        // it in Preferences.
        AddGlobalToggle("Pref_UseSystemProxy", () => AppSettings.Global.UseSystemProxy,
            v => AppSettings.Global.UseSystemProxy = v, AppSettings.ApplyRuntimeGlobalPreferences);

        var btnProxy = MakeButton(LanguageManager.Instance["Pref_OpenProxySettings"], 200);
        btnProxy.Click += (s, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:network-proxy") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.Log("Settings", ex, "Opening Windows proxy settings.");
                ZidimiMessageBox.Show(LanguageManager.Instance["Pref_ProxyError"] + ex.Message,
                    LanguageManager.Instance["Pref_Error"], ZidimiMessageBoxButton.OK,
                    ZidimiMessageBoxImage.Error, Window.GetWindow(this));
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
            (LanguageManager.Instance["Pref_Version"], "Zidimi Browser " + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0")),
            (LanguageManager.Instance["Pref_EngineLabel"], "Chromium / CefSharp 150"),
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
            var valueText = new TextBlock { Text = value, FontSize = 13, Foreground = (Brush)FindResource("Ink200Brush") };
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);
            panel.Children.Add(grid);
        }

        var btnCheck = MakeButton(LanguageManager.Instance["Pref_CheckUpdate"], 160);
        btnCheck.Click += async (s, e) =>
        {
            var originalContent = btnCheck.Content;
            btnCheck.Content = LanguageManager.Instance["Pref_CheckingUpdate"];
            btnCheck.IsEnabled = false;
            try
            {
                var result = await UpdateService.CheckAsync();
                if (!result.Success)
                {
                    ZidimiMessageBox.Show(
                        string.Format(LanguageManager.Instance["Pref_UpdateCheckFailed"], result.Error ?? "Unknown error"),
                        LanguageManager.Instance["Pref_Update"], ZidimiMessageBoxButton.OK,
                        ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
                }
                else if (!result.IsUpdateAvailable)
                {
                    ZidimiMessageBox.Show(LanguageManager.Instance["Pref_UpToDate"],
                        LanguageManager.Instance["Pref_Update"], ZidimiMessageBoxButton.OK,
                        ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                }
                else
                {
                    var open = ZidimiMessageBox.Show(
                        string.Format(LanguageManager.Instance["Pref_UpdateAvailable"], result.LatestVersion),
                        LanguageManager.Instance["Pref_Update"], ZidimiMessageBoxButton.YesNo,
                        ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                    if (open == ZidimiMessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.PageUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.PageUrl)
                        {
                            UseShellExecute = true,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("Update", ex);
                ZidimiMessageBox.Show(
                    string.Format(LanguageManager.Instance["Pref_UpdateCheckFailed"], ex.Message),
                    LanguageManager.Instance["Pref_Update"], ZidimiMessageBoxButton.OK,
                    ZidimiMessageBoxImage.Error, Window.GetWindow(this));
            }
            finally
            {
                btnCheck.Content = originalContent;
                btnCheck.IsEnabled = true;
            }
        };
        btnCheck.Margin = new Thickness(0, 16, 0, 0);
        panel.Children.Add(btnCheck);

        return panel;
    }

    private static void OpenChromiumSettings(string url)
    {
        App.ViewModel?.NewTab(url);
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

