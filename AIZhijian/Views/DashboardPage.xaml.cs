using System.Windows.Controls;

namespace AIZhijian.Views;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        var api = Services.ApiService.Instance;
        ServerInfo.Text = $"{api.ServerDisplayOrigin} | 用户: {api.Username} ({api.Role})";
    }
}
