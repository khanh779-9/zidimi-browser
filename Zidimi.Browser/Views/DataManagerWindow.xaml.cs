using Zidimi.Browser.Infrastructure;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views
{
    public partial class DataManagerWindow : Window
    {
        private string _mode;

        public DataManagerWindow(string mode)
        {
            InitializeComponent();
            _mode = mode;
            SetupMode();
        }

        private void SetupMode()
        {
            if (_mode == "passwords")
            {
                LblTitle.Text = LanguageManager.Instance["DataMgr_ManagePasswords"];
                var sitePlaceholder = LanguageManager.Instance["DataMgr_WebsitePlaceholder"];
                TxtF1.Tag = sitePlaceholder;
                TxtF1.ToolTip = sitePlaceholder;
                TxtF2.Tag = LanguageManager.Instance["DataMgr_Username"];
                TxtF2.ToolTip = LanguageManager.Instance["DataMgr_Username"];
                TxtF3.Tag = LanguageManager.Instance["DataMgr_Password"];
                TxtF3.ToolTip = LanguageManager.Instance["DataMgr_Password"];
                ListItems.ItemTemplate = (DataTemplate)FindResource("PasswordTemplate");
                ListItems.ItemsSource = AutofillManager.Data.Passwords;
            }
            else if (_mode == "cards")
            {
                LblTitle.Text = LanguageManager.Instance["DataMgr_ManageCards"];
                TxtF1.Tag = LanguageManager.Instance["DataMgr_NameOnCard"];
                TxtF1.ToolTip = LanguageManager.Instance["DataMgr_NameOnCard"];
                TxtF2.Tag = LanguageManager.Instance["DataMgr_CardNumber"];
                TxtF2.ToolTip = LanguageManager.Instance["DataMgr_CardNumber"];
                TxtF3.Tag = LanguageManager.Instance["DataMgr_ExpiryDate"];
                TxtF3.ToolTip = LanguageManager.Instance["DataMgr_ExpiryDate"];
                ListItems.ItemTemplate = (DataTemplate)FindResource("CardTemplate");
                ListItems.ItemsSource = AutofillManager.Data.Cards;
            }
            else if (_mode == "addresses")
            {
                LblTitle.Text = LanguageManager.Instance["DataMgr_ManageAddresses"];
                TxtF1.Tag = LanguageManager.Instance["DataMgr_FullName"];
                TxtF1.ToolTip = LanguageManager.Instance["DataMgr_FullName"];
                TxtF2.Tag = LanguageManager.Instance["DataMgr_Phone"];
                TxtF2.ToolTip = LanguageManager.Instance["DataMgr_Phone"];
                TxtF3.Tag = LanguageManager.Instance["DataMgr_AddressDetail"];
                TxtF3.ToolTip = LanguageManager.Instance["DataMgr_AddressDetail"];
                ListItems.ItemTemplate = (DataTemplate)FindResource("AddressTemplate");
                ListItems.ItemsSource = AutofillManager.Data.Addresses;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtF1.Text) && string.IsNullOrWhiteSpace(TxtF2.Text)) return;

            if (_mode == "passwords")
            {
                AutofillManager.Data.Passwords.Add(new PasswordEntry { Url = TxtF1.Text, Username = TxtF2.Text, Password = TxtF3.Text });
            }
            else if (_mode == "cards")
            {
                AutofillManager.Data.Cards.Add(new CardEntry { Name = TxtF1.Text, CardNumber = TxtF2.Text, Expiry = TxtF3.Text });
            }
            else if (_mode == "addresses")
            {
                AutofillManager.Data.Addresses.Add(new AddressEntry { Name = TxtF1.Text, Phone = TxtF2.Text, Address = TxtF3.Text });
            }

            AutofillManager.Save();
            
            // Refresh
            ListItems.ItemsSource = null;
            SetupMode();
            
            TxtF1.Text = "";
            TxtF2.Text = "";
            TxtF3.Text = "";
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                if (_mode == "passwords")
                {
                    if (long.TryParse(id, out var idLong))
                        AutofillManager.Data.Passwords.RemoveAll(x => x.Id == idLong);
                }
                else if (_mode == "cards")
                    AutofillManager.Data.Cards.RemoveAll(x => x.Guid == id);
                else if (_mode == "addresses")
                    AutofillManager.Data.Addresses.RemoveAll(x => x.Guid == id);

                AutofillManager.Save();

                // Refresh
                ListItems.ItemsSource = null;
                SetupMode();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
