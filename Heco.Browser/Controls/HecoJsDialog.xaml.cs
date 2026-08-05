using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Heco.Browser.Controls;

public partial class HecoJsDialog : Window, INotifyPropertyChanged
{
    private string _dialogTitle = "Thông báo từ trang web";
    private string _messageText = "";
    private string _inputText = "";
    private bool _isPrompt = false;
    private bool _showCancel = false;

    public string DialogTitle
    {
        get => _dialogTitle;
        set { _dialogTitle = value; OnPropertyChanged(); }
    }

    public string MessageText
    {
        get => _messageText;
        set { _messageText = value; OnPropertyChanged(); }
    }

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public bool IsPrompt
    {
        get => _isPrompt;
        set { _isPrompt = value; OnPropertyChanged(); }
    }

    public bool ShowCancel
    {
        get => _showCancel;
        set { _showCancel = value; OnPropertyChanged(); }
    }

    public HecoJsDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
