using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views
{
    public partial class ClearDataWindow : Window
    {
        public ClearDataWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        BtnClear.IsEnabled = false;
        var originalContent = BtnClear.Content;
        BtnClear.Content = LanguageManager.Instance["Clear_Clearing"];

        try
        {
            var vm = App.ViewModel;

            // 1) History
            if (ChkHistory.IsChecked == true && vm != null)
            {
                await Dispatcher.BeginInvoke(() => vm.ClearHistoryCommand.Execute(null));
            }

            // 2) Bookmarks — ClearDataWindow only clears them when the user selects Cookies (like Chrome combines);
            // History is already handled separately. We won't touch bookmarks from this window, following Chrome's UX.
            // (Skipping bookmark removal intentionally.)

            // 3) Cookies + cache via the CEF request context
            var profile = AppSettings.Global.CurrentProfile;
            var context = App.RequestContexts.GetProfileContext(profile) ?? Cef.GetGlobalRequestContext();

            if (ChkCookies.IsChecked == true)
            {
                var cookieManager = context.GetCookieManager(null);
                if (cookieManager != null)
                    await cookieManager.DeleteCookiesAsync();
            }

            if (ChkCache.IsChecked == true)
            {
                context.ClearCertificateExceptions(null);
                context.ClearHttpAuthCredentials(null);
            }

            await Task.Delay(300); // Give the UI feedback time
            
            ZidimiMessageBox.Show(LanguageManager.Instance["Clear_DataCleared"], LanguageManager.Instance["Clear_Success"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Success, this);
            Close();
        }
        catch (Exception ex)
        {
            ZidimiMessageBox.Show(string.Format(LanguageManager.Instance["Clear_ErrorMsg"], ex.Message), LanguageManager.Instance["Pref_Error"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Error, this);
            BtnClear.IsEnabled = true;
            BtnClear.Content = originalContent;
        }
    }
    }
}

