using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;
using CefSharp;

namespace Heco.Browser.Views;

public partial class ProfileSelectorWindow : HecoWindow
{
    public ObservableCollection<ProfileSelectorItem> Profiles { get; } = new();
    private bool _isUpdating = true;

    public ProfileSelectorWindow()
    {
        InitializeComponent();
        ProfileItemsControl.ItemsSource = Profiles;
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        _isUpdating = true;
        Profiles.Clear();

        try
        {
            if (File.Exists(UserDataPaths.LocalStatePath))
            {
                var json = File.ReadAllText(UserDataPaths.LocalStatePath);
                if (JsonSerializer.Deserialize<JsonNode>(json) is JsonObject root && 
                    root.TryGetPropertyValue("profile", out var profileNode) && 
                    profileNode is JsonObject profileObj)
                {
                    if (profileObj.TryGetPropertyValue("show_picker_on_startup", out var showPickerNode) && showPickerNode != null)
                    {
                        ShowOnStartupToggle.IsChecked = showPickerNode.GetValue<bool>();
                    }
                    else
                    {
                        ShowOnStartupToggle.IsChecked = true;
                    }

                    if (profileObj.TryGetPropertyValue("info_cache", out var infoCacheNode) && infoCacheNode is JsonObject infoCache)
                    {
                        var orderedFolders = new List<string>();
                        if (profileObj.TryGetPropertyValue("profiles_order", out var orderNode) && orderNode is JsonArray orderArray)
                        {
                            foreach (var item in orderArray)
                            {
                                if (item != null) orderedFolders.Add(item.GetValue<string>());
                            }
                        }

                        foreach (var kvp in infoCache)
                        {
                            if (!orderedFolders.Contains(kvp.Key))
                            {
                                orderedFolders.Add(kvp.Key);
                            }
                        }

                        foreach (var folderName in orderedFolders)
                        {
                            if (!infoCache.TryGetPropertyValue(folderName, out var itemNode) || !(itemNode is JsonObject itemObj))
                                continue;

                            var name = folderName;
                            if (itemObj.TryGetPropertyValue("name", out var nameNode) && nameNode != null)
                            {
                                name = nameNode.GetValue<string>();
                            }

                            var avatarPath = UserDataPaths.AvatarIconFile(folderName);
                            if (!File.Exists(avatarPath))
                            {
                                AvatarGenerator.GenerateAndSave(folderName);
                            }

                            Profiles.Add(new ProfileSelectorItem
                            {
                                FolderName = folderName,
                                Name = name,
                                AvatarPath = avatarPath,
                                IsAddButton = false
                            });
                        }
                    }
                }
            }
        }
        catch { }

        // Fallback for first run / empty
        if (Profiles.Count == 0)
        {
            var defaultName = UserDataPaths.DefaultProfileName;
            var folderName = UserDataPaths.ProfileFolder(defaultName);
            var avatarPath = UserDataPaths.AvatarIconFile(folderName);
            if (!File.Exists(avatarPath)) AvatarGenerator.GenerateAndSave(folderName);

            Profiles.Add(new ProfileSelectorItem
            {
                FolderName = folderName,
                Name = defaultName,
                AvatarPath = avatarPath,
                IsAddButton = false
            });
            ShowOnStartupToggle.IsChecked = true;
        }

        // Add Button
        Profiles.Add(new ProfileSelectorItem { IsAddButton = true });
        _isUpdating = false;
    }

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ProfileSelectorItem item)
        {
            if (item.IsAddButton)
            {
                // Inline add-profile logic (formerly in ProfileManagerWindow.Add_Click)
                var newProfile = AppSettings.NextProfileName();
                AppSettings.Global.Profiles.Add(newProfile);
                AppSettings.Global.CurrentProfile = newProfile;
                AppSettings.LoadProfile(newProfile);
                AppSettings.SaveAll();

                UserDataPaths.EnsureProfileDir(newProfile);
                AvatarGenerator.GenerateAndSave(newProfile);
                UserDataPaths.RegisterProfile(newProfile);
                App.ViewModel?.SwitchProfile(newProfile);
                ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

                LoadProfiles();
                return;
            }

            LaunchProfile(item.Name);
        }
    }

    private void MoreOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
        e.Handled = true;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi && mi.Parent is System.Windows.Controls.ContextMenu cm && 
            cm.PlacementTarget is FrameworkElement b && b.Tag is ProfileSelectorItem p)
        {
            if (AppSettings.Global.Profiles.Count <= 1)
            {
                HecoMessageBox.Show(
                    LanguageManager.Instance["ProfileManager_CantDeleteActiveMsg"],
                    LanguageManager.Instance["ProfileManager_CantDeleteActiveTitle"],
                    HecoMessageBoxButton.OK,
                    HecoMessageBoxImage.Warning,
                    this);
                return;
            }

            var res = HecoMessageBox.Show(
                LanguageManager.Instance["ProfileManager_DeleteConfirmMsg"],
                LanguageManager.Instance["ProfileManager_DeleteConfirmTitle"],
                HecoMessageBoxButton.YesNo,
                HecoMessageBoxImage.Warning,
                this);

            if (res == HecoMessageBoxResult.Yes)
            {
                AppSettings.Global.Profiles.Remove(p.Name);
                if (AppSettings.Global.CurrentProfile == p.Name)
                {
                    AppSettings.Global.CurrentProfile = AppSettings.Global.Profiles.FirstOrDefault() ?? UserDataPaths.DefaultProfileName;
                    AppSettings.LoadProfile(AppSettings.Global.CurrentProfile);
                }
                AppSettings.SaveAll();

                UserDataPaths.UpdateLocalState(root =>
                {
                    if (root["profile"] is JsonObject prof)
                    {
                        if (prof["info_cache"] is JsonObject cache)
                            cache.Remove(p.FolderName);
                        
                        if (prof["profiles_order"] is JsonArray order)
                        {
                            var toRemove = System.Linq.Enumerable.FirstOrDefault(order, x => x?.GetValue<string>() == p.FolderName);
                            if (toRemove != null) order.Remove(toRemove);
                        }
                    }
                });

                try
                {
                    var dir = UserDataPaths.ProfileDir(p.Name);
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                catch { }

                LoadProfiles();
            }
        }
    }

    private void Guest_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).InitializeBrowser();
        if (!App.ViewModel.IsGuestMode)
        {
            App.ViewModel.ToggleGuestMode();
        }
        this.Close();
    }

    private void ShowOnStartup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        bool show = ShowOnStartupToggle.IsChecked == true;
        App.ShowPickerOnStartupPreference = show;

        UserDataPaths.UpdateLocalState(root =>
        {
            var profileObj = (JsonObject?)root["profile"] ?? (JsonObject)(root["profile"] = new JsonObject());
            profileObj["show_picker_on_startup"] = show;
        });

        if (App.CefReady)
        {
            var ctx = App.RequestContexts?.GetProfileContext(AppSettings.Global.CurrentProfile) ?? Cef.GetGlobalRequestContext();
            ctx?.SetPreference("profile.show_picker_on_startup", show, out _);
        }
    }

    private void LaunchProfile(string profileName)
    {
        AppSettings.Global.CurrentProfile = profileName;
        AppSettings.SaveGlobal();
        AppSettings.LoadProfile(profileName);

        ((App)Application.Current).InitializeBrowser();
        this.Close();
    }
}

public class ProfileSelectorItem
{
    public string FolderName { get; set; } = "";
    public string Name { get; set; } = "";
    public string AvatarPath { get; set; } = "";
    public bool IsAddButton { get; set; }
}
