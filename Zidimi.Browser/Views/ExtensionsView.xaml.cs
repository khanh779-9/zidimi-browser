using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views;

public partial class ExtensionsView : UserControl
{
    private List<ExtensionInfo> _items = new();

    public ExtensionsView()
    {
        InitializeComponent();
        Loaded += ExtensionsView_Loaded;
        Unloaded += ExtensionsView_Unloaded;
    }

    private void ExtensionsView_Loaded(object sender, RoutedEventArgs e)
    {
        ExtensionService.Instance.ExtensionsChanged -= ExtensionService_ExtensionsChanged;
        ExtensionService.Instance.ExtensionsChanged += ExtensionService_ExtensionsChanged;
        RefreshList();
    }

    private void ExtensionsView_Unloaded(object sender, RoutedEventArgs e)
    {
        ExtensionService.Instance.ExtensionsChanged -= ExtensionService_ExtensionsChanged;
    }

    private void ExtensionService_ExtensionsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RefreshList));
    }

    public void RefreshList()
    {
        var filter = SearchBox.Text?.Trim() ?? string.Empty;
        _items = ExtensionService.Instance.InstalledExtensions
            .OrderByDescending(x => x.IsPinned)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (!string.IsNullOrEmpty(filter))
        {
            _items = _items.Where(x =>
                x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        ExtensionsList.ItemsSource = _items;
        EmptyState.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private void OpenWebStoreBtn_Click(object sender, RoutedEventArgs e)
    {
        App.ViewModel?.NewTab("https://chromewebstore.google.com/");
    }

    private async void InstallWebStoreBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ZidimiJsDialog
            {
                DialogTitle = LanguageManager.Instance["Ext_InstallFromWebStore"],
                MessageText = LanguageManager.Instance["Ext_EnterUrlOrIdPrompt"],
                IsPrompt = true,
                ShowCancel = true,
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var urlOrId = dialog.InputText.Trim();
                var context = App.ViewModel?.GetRequestContext();

                InstallWebStoreBtn.IsEnabled = false;
                LoadUnpackedBtn.IsEnabled = false;

                var res = await ExtensionService.Instance.DownloadAndInstallFromWebStoreAsync(urlOrId, context);

                InstallWebStoreBtn.IsEnabled = true;
                LoadUnpackedBtn.IsEnabled = true;

                if (res.success)
                {
                    await ActivateInCurrentBrowserAsync(res.ext);
                    ZidimiMessageBox.Show(res.message, LanguageManager.Instance["Ext_Title"],
                        ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                    RefreshList();
                }
                else
                {
                    ZidimiMessageBox.Show(res.message, LanguageManager.Instance["Ext_Title"],
                        ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
                }
            }
        }
        catch (Exception ex)
        {
            InstallWebStoreBtn.IsEnabled = true;
            LoadUnpackedBtn.IsEnabled = true;
            ZidimiMessageBox.Show(ex.Message, LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    private async void LoadUnpackedBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = LanguageManager.Instance["Ext_SelectFolderTitle"]
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FolderName))
            {
                var context = App.ViewModel?.GetRequestContext();
                var res = ExtensionService.Instance.LoadUnpackedExtension(dialog.FolderName, context);
                if (res.success)
                {
                    await ActivateInCurrentBrowserAsync(res.ext);
                    ZidimiMessageBox.Show(res.message, LanguageManager.Instance["Ext_Title"],
                        ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                    RefreshList();
                }
                else
                {
                    ZidimiMessageBox.Show(res.message, LanguageManager.Instance["Ext_Title"],
                        ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Warning, Window.GetWindow(this));
                }
            }
        }
        catch (Exception ex)
        {
            ZidimiMessageBox.Show(ex.Message, LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    private static async System.Threading.Tasks.Task ActivateInCurrentBrowserAsync(ExtensionInfo? ext)
    {
        if (ext == null) return;
        var vm = App.ViewModel;
        var activeTab = vm?.ActiveTab;
        if (vm == null || activeTab == null) return;

        if (vm.GetBrowser(activeTab) is CefSharp.IChromiumWebBrowserBase browser &&
            !browser.IsDisposed && browser.BrowserCore != null)
        {
            var runtime = await ExtensionService.Instance.EnsureExtensionRuntimeLoadedAsync(ext, browser);
            if (!runtime.success)
                AppLogger.Log("ExtensionRuntime", $"Installed {ext.Name}, but runtime load failed: {runtime.message}");
        }
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ExtensionInfo ext)
        {
            var context = App.ViewModel?.GetRequestContext();
            ExtensionService.Instance.ToggleExtension(ext, ext.IsEnabled, context);
        }
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ExtensionInfo ext)
        {
            ExtensionService.Instance.TogglePinned(ext, !ext.IsPinned);
            RefreshList();
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ExtensionInfo ext)
        {
            var confirm = ZidimiMessageBox.Show(
                string.Format(LanguageManager.Instance["Ext_RemoveConfirm"], ext.Name),
                LanguageManager.Instance["Ext_Title"],
                ZidimiMessageBoxButton.YesNo,
                ZidimiMessageBoxImage.Warning,
                Window.GetWindow(this));

            if (confirm == ZidimiMessageBoxResult.Yes)
            {
                var context = App.ViewModel?.GetRequestContext();
                ExtensionService.Instance.RemoveExtension(ext, context);
                ZidimiMessageBox.Show(LanguageManager.Instance["Ext_RemovedSuccess"], LanguageManager.Instance["Ext_Title"],
                    ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Information, Window.GetWindow(this));
                RefreshList();
            }
        }
    }
}
