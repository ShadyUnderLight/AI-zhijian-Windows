using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class LoginPage : UserControl
{

    public LoginPage()
    {
        InitializeComponent();
        var savedUser = ApiService.Instance.GetSavedUsername();
        if (!string.IsNullOrEmpty(savedUser))
        {
            UsernameBox.Text = savedUser;
            RememberCheck.IsChecked = true;
        }

        ApiService.Instance.StateChanged += OnHealthChanged;
        IsVisibleChanged += OnVisibilityChanged;
        _ = RefreshHealthAsync();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            _ = RefreshHealthAsync();
    }

    private void OnHealthChanged()
    {
        Dispatcher.Invoke(() => UpdateHealthIndicator());
    }

    private async Task RefreshHealthAsync()
    {
        await ApiService.Instance.CheckBackendHealth();
    }

    private void UpdateHealthIndicator()
    {
        var state = ApiService.Instance.BackendHealth;
        var neutral = (Brush)(TryFindResource("TextSecondary") ?? Brushes.Gray);
        var success = (Brush)(TryFindResource("SuccessBrush") ?? Brushes.Green);
        var danger = (Brush)(TryFindResource("DangerBrush") ?? Brushes.Red);

        (HealthText.Text, HealthText.Foreground) = state switch
        {
            BackendHealthState.Unknown => ("检查后端状态...", neutral),
            BackendHealthState.Checking => ("检查后端状态...", neutral),
            BackendHealthState.Healthy => ("● 后端服务正常", success),
            BackendHealthState.Reachable => ("● 服务器可达", Brushes.Orange),
            BackendHealthState.Unhealthy => ("● 服务器异常", danger),
            BackendHealthState.Unreachable => ("● 服务器不可达", danger),
            _ => ("", Brushes.Transparent),
        };
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
