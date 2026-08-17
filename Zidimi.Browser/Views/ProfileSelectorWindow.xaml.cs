using System.Collections.ObjectModel;
using System.Windows;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

public partial class ProfileSelectorWindow : ZidimiWindow
{
    public ObservableCollection<ProfileSelectorItem> Profiles { get; } = new();

    public ProfileSelectorWindow()
    {
        InitializeComponent();
        ProfileItemsControl.ItemsSource = Profiles;
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var globalChanged = false;

        var discovered = ChromiumProfileCatalog.GetProfiles(AppSettings.Global.Profiles);
        foreach (var profile in discovered)
        {
            if (!AppSettings.Global.Profiles.Contains(profile.Id, StringComparer.OrdinalIgnoreCase))
            {
                AppSettings.Global.Profiles.Add(profile.Id);
                globalChanged = true;
            }

            Profiles.Add(new ProfileSelectorItem
            {
                ProfileId = profile.Id,
                FolderName = profile.Id,
                Name = profile.DisplayName,
                UserName = profile.UserName,
                AvatarSource = AvatarGenerator.CreateImageSource(profile.Id),
                IsAddButton = false,
            });
        }

        if (Profiles.Count == 0)
        {
            AppSettings.Global.Profiles.Add(UserDataPaths.DefaultProfileId);
            AppSettings.Global.CurrentProfile = UserDataPaths.DefaultProfileId;
            AppSettings.SaveGlobal();
            LoadProfiles();
            return;
        }

        Profiles.Add(new ProfileSelectorItem { IsAddButton = true });
        // The directory catalog is derived state; only shell state is persisted through CEF.
        if (globalChanged) AppSettings.SaveGlobal();
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProfileSelectorItem item }) return;

        if (item.IsAddButton)
        {
            var profileId = ChromiumProfileCatalog.NextProfileId(AppSettings.Global.Profiles);
            var displayName = AppSettings.NextProfileName();

            // Selecting the new CachePath is enough; CEF creates and owns the profile files.
            AppSettings.Global.Profiles.Add(profileId);
            AppSettings.Global.CurrentProfile = profileId;
            AppSettings.LoadProfile(profileId);
            AppSettings.Profile.DisplayName = displayName;
            AppSettings.SaveAll();

            App.ViewModel?.SwitchProfile(profileId);
            ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
            LoadProfiles();
            return;
        }

        LaunchProfile(item.ProfileId);
    }

    private void MoreOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: not null } button)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
        e.Handled = true;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Parent: System.Windows.Controls.ContextMenu menu } ||
            menu.PlacementTarget is not FrameworkElement { Tag: ProfileSelectorItem profile })
            return;

        if (AppSettings.Global.Profiles.Count <= 1 ||
            string.Equals(profile.ProfileId, UserDataPaths.DefaultProfileId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AppSettings.Global.CurrentProfile, profile.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            ZidimiMessageBox.Show(
                LanguageManager.Instance["ProfileManager_CantDeleteActiveMsg"],
                LanguageManager.Instance["ProfileManager_CantDeleteActiveTitle"],
                ZidimiMessageBoxButton.OK,
                ZidimiMessageBoxImage.Warning,
                this);
            return;
        }

        var result = ZidimiMessageBox.Show(
            LanguageManager.Instance["ProfileManager_DeleteConfirmMsg"],
            LanguageManager.Instance["ProfileManager_DeleteConfirmTitle"],
            ZidimiMessageBoxButton.YesNo,
            ZidimiMessageBoxImage.Warning,
            this);

        if (result != ZidimiMessageBoxResult.Yes) return;

        App.RequestContexts?.ReleaseProfileContext(profile.ProfileId);

        try
        {
            var directory = UserDataPaths.ProfileDir(profile.ProfileId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

            AppSettings.Global.Profiles.RemoveAll(id =>
                string.Equals(id, profile.ProfileId, StringComparison.OrdinalIgnoreCase));
            ChromiumProfileCatalog.ForgetProfile(profile.ProfileId);
            AppSettings.SaveGlobal();
            LoadProfiles();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Profile", ex, $"Deleting profile folder '{profile.ProfileId}'.");
            ZidimiMessageBox.Show(
                ex.Message,
                LanguageManager.Instance["Pref_Error"],
                ZidimiMessageBoxButton.OK,
                ZidimiMessageBoxImage.Error,
                this);
        }
    }

    private void Guest_Click(object sender, RoutedEventArgs e)
    {
        if (App.ViewModel is not null && !App.ViewModel.IsGuestMode)
            App.ViewModel.ToggleGuestMode();

        ((App)Application.Current).InitializeBrowser();
        Close();
    }

    private void LaunchProfile(string profileId)
    {
        var app = (App)Application.Current;
        var resolvedId = ChromiumProfileCatalog.ResolveProfileId(profileId, AppSettings.Global.Profiles);

        // App.ViewModel is created before CEF starts so the splash/profile selector can use the
        // same services. Therefore ViewModel != null does NOT mean that a browser window exists.
        // The old test skipped InitializeBrowser() during startup, then closed the picker (the last
        // WPF window), which made the process exit with no MainWindow.
        var browserAlreadyRunning = app.HasLiveBrowserWindow;
        var profileChanged = !string.Equals(
            AppSettings.Global.CurrentProfile, resolvedId, StringComparison.OrdinalIgnoreCase);

        AppSettings.Global.CurrentProfile = resolvedId;
        AppSettings.SaveGlobal();
        AppSettings.LoadProfile(resolvedId);

        if (browserAlreadyRunning)
        {
            if (profileChanged || App.ViewModel.IsGuestMode)
            {
                App.ViewModel.SwitchProfile(resolvedId);
                ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
            }

            // This picker was opened from the live browser (normally as a modal dialog).
            // The owner/main browser stays alive, so it is safe to close only the picker.
            Close();
            return;
        }

        // Startup picker path. InitializeBrowser() synchronously re-shows the integrated Zidimi
        // shell before returning, so this picker can close without leaving WPF with zero windows.
        app.InitializeBrowser();
        Close();
    }
}

public class ProfileSelectorItem
{
    public string ProfileId { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public System.Windows.Media.ImageSource? AvatarSource { get; set; }
    public bool IsAddButton { get; set; }
}
