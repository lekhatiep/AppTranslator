using System.Windows;

namespace AppTranslator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    // PasswordBox không bind được mật khẩu (cố ý, để key không nằm trong thuộc tính chuỗi); đọc khi bấm Lưu rồi xóa ô.
    private void OnSaveApiKeyClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveApiKey(ApiKeyBox.Password);
        ApiKeyBox.Clear();
    }
}
