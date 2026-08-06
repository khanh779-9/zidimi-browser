using Heco.Browser.Controls;
using Heco.Browser.Infrastructure;
using System;
using System.Threading.Tasks;
using System.Windows;
using Heco.Browser.Models;

namespace Heco.Browser.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtEmail.Text) || string.IsNullOrWhiteSpace(TxtPassword.Password))
            {
                LblError.Text = LanguageManager.Instance["Login_EmptyFields"];
                LblError.Visibility = Visibility.Visible;
                return;
            }

            if (!TxtEmail.Text.Contains("@"))
            {
                LblError.Text = LanguageManager.Instance["Login_InvalidEmail"];
                LblError.Visibility = Visibility.Visible;
                return;
            }

            LblError.Visibility = Visibility.Collapsed;
            BtnLogin.IsEnabled = false;
            var originalContent = BtnLogin.Content;
            BtnLogin.Content = LanguageManager.Instance["Login_Processing"];

            // Simulate network request
            await Task.Delay(1500);

            // Fake login success
            AppSettings.Global.LoggedInUser = TxtEmail.Text;
            AppSettings.SaveAll();

            HecoMessageBox.Show(string.Format(LanguageManager.Instance["Login_SuccessMsg"], TxtEmail.Text), LanguageManager.Instance["Login_Sync"], HecoMessageBoxButton.OK, HecoMessageBoxImage.Success, this);
            
            DialogResult = true;
            Close();
        }
    }
}

