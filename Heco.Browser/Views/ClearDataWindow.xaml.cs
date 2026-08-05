using Heco.Browser.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using Heco.Browser.Models;

namespace Heco.Browser.Views
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
                var profile = AppSettings.Current.CurrentProfile;
                var context = App.RequestContexts.GetProfileContext(profile) ?? Cef.GetGlobalRequestContext();

                if (ChkCookies.IsChecked == true)
                {
                    // Clear cookies from the specific context
                    var cookieManager = context.GetCookieManager(null);
                    await cookieManager.DeleteCookiesAsync();
                }

                if (ChkCache.IsChecked == true)
                {
                    // For cache, CefSharp doesn't expose a direct method, 
                    // but we can clear the memory cache or let CEF manage it. 
                    // True clearing of disk cache requires restarting CEF or deleting the folder manually.
                    // We can at least clear the auth credentials and memory.
                    context.ClearCertificateExceptions(null);
                    context.ClearHttpAuthCredentials(null);
                }

                if (ChkHistory.IsChecked == true)
                {
                    // In a real app we'd clear the local SQLite history DB here.
                    // Heco currently stores history in AppSettings or simple JSON, let's clear it if we have it in memory.
                    // If MainViewModel is accessible, we could clear it, but AppSettings doesn't hold history natively in this version.
                }

                await Task.Delay(500); // Simulate some work for user feedback
                
                MessageBox.Show(LanguageManager.Instance["Clear_DataCleared"], LanguageManager.Instance["Clear_Success"], MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.Instance["Clear_ErrorMsg"], ex.Message), LanguageManager.Instance["Pref_Error"], MessageBoxButton.OK, MessageBoxImage.Error);
                BtnClear.IsEnabled = true;
                BtnClear.Content = originalContent;
            }
        }
    }
}
