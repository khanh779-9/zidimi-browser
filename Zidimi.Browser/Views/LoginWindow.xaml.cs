using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using System;
using System.Threading.Tasks;
using System.Windows;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views
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

            ZidimiMessageBox.Show(string.Format(LanguageManager.Instance["Login_SuccessMsg"], TxtEmail.Text), LanguageManager.Instance["Login_Sync"], ZidimiMessageBoxButton.OK, ZidimiMessageBoxImage.Success, this);
            
            DialogResult = true;
            Close();
        }
    }
}

