using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using Heco.Browser.Models;

namespace Heco.Browser.Views;

public partial class ProfileManagerWindow : HecoWindow
{
    public ObservableCollection<ProfileItem> Profiles { get; } = new();

    public ProfileManagerWindow()
    {
        InitializeComponent();
        ProfileList.ItemsSource = Profiles;
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        var current = AppSettings.Global.CurrentProfile;
        foreach (var p in AppSettings.Global.Profiles)
        {
            AvatarGenerator.GenerateAndSave(p);
            Profiles.Add(new ProfileItem
            {
                Name = p,
                Initial = string.IsNullOrEmpty(p) ? "P" : p.Substring(0, 1).ToUpper(),
                IsActive = p == current
            });
        }
    }

private void Add_Click(object sender, RoutedEventArgs e)
    {
        var newProfile = AppSettings.NextProfileName();
        AppSettings.Global.Profiles.Add(newProfile);
        AppSettings.Global.CurrentProfile = newProfile;
        AppSettings.LoadProfile(newProfile);
        AppSettings.SaveAll();

        UserDataPaths.EnsureProfileDir(newProfile);
        AvatarGenerator.GenerateAndSave(newProfile);
        UserDataPaths.RegisterProfile(newProfile);
        App.ViewModel?.SwitchProfile(newProfile);
        Infrastructure.ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);

        LoadProfiles();
    }

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement b && b.Tag is ProfileItem p)
        {
            if (AppSettings.Global.CurrentProfile == p.Name) return;
            AppSettings.Global.CurrentProfile = p.Name;
            AppSettings.LoadProfile(p.Name);
            AppSettings.SaveAll();
            App.ViewModel?.SwitchProfile(p.Name);
            Infrastructure.ThemeManager.ApplyFromSettings(AppSettings.Profile.Theme);
            LoadProfiles();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement b && b.Tag is ProfileItem p)
        {
            if (p.IsActive)
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
                // Delete from settings
                AppSettings.Global.Profiles.Remove(p.Name);
                AppSettings.SaveAll();

// Delete from local state info_cache + profiles_order
                var folderName = UserDataPaths.ProfileFolder(p.Name);
                UserDataPaths.UpdateLocalState(root =>
                {
                    if (root["profile"] is System.Text.Json.Nodes.JsonObject prof)
                    {
                        if (prof["info_cache"] is System.Text.Json.Nodes.JsonObject cache)
                        {
                            cache.Remove(folderName);
                        }
                        if (prof["profiles_order"] is System.Text.Json.Nodes.JsonArray order)
                        {
                            var toRemove = System.Linq.Enumerable.FirstOrDefault(
                                order, x => x?.GetValue<string>() == folderName);
                            if (toRemove != null) order.Remove(toRemove);
                        }
                    }
                });

                // Delete folder
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
}

public class ProfileItem
{
    public string Name { get; set; } = "";
    public string Initial { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsNotActive => !IsActive;
}

