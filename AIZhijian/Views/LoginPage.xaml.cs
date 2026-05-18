using System.Windows;
using System.Windows.Controls;

namespace AIZhijian.Views;

public partial class LoginPage : UserControl
{

    public LoginPage()
    {
        InitializeComponent();
        var savedUser = Services.ApiService.Instance.GetSavedUsername();
        if (!string.IsNullOrEmpty(savedUser))
        {
            UsernameBox.Text = savedUser;
            RememberCheck.IsChecked = true;
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ErrorText.Text = "请输入用户名和密码";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        LoginButton.IsEnabled = false;
        LoadingText.Visibility = Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            await Services.ApiService.Instance.Login(
                UsernameBox.Text, PasswordBox.Password, RememberCheck.IsChecked ?? false);

            if (!Services.ApiService.Instance.IsLoggedIn)
            {
                ErrorText.Text = Services.ApiService.Instance.LoginError ?? "登录失败";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoadingText.Visibility = Visibility.Collapsed;
        }
    }
}
