using System.Windows;
using System.Windows.Controls;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        UrlBox.Text = AppConfig.ApiBaseUrl.ToString();
        ConcurrencyBox.Text = App.Api.GetQueue().ConcurrencyLimit.ToString();
    }

    private void ChangeUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _)) return;

        AppConfig.SetCustomBaseUrl(url);
        _ = Task.Run(async () =>
        {
            await App.Api.Logout();
            App.Api.ResetForNewHost();
            App.Api.IsCheckingSession = true;
            Dispatcher.Invoke(() => MessageBox.Show("服务器地址已修改，请重新登录", "提示"));
        });
    }

    private void SaveConcurrency_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(ConcurrencyBox.Text, out var v) && v >= 1 && v <= 5)
            App.Api.GetQueue().ConcurrencyLimit = v;
    }
}
