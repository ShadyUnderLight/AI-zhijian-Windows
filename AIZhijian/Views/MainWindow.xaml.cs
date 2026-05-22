using System.Windows;
using System.Windows.Controls;

namespace AIZhijian.Views;

public partial class MainWindow : Window
{
    private readonly ViewModels.MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new ViewModels.MainViewModel();
        DataContext = _vm;
        _vm.StateChanged += OnStateChanged;
        Loaded += async (_, _) => await _vm.Initialize();
    }

    private void OnStateChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var api = Services.ApiService.Instance;

            if (api.IsCheckingSession)
            {
                LoginStateView.Visibility = Visibility.Visible;
                LoginPageControl.Visibility = Visibility.Collapsed;
                MainLayout.Visibility = Visibility.Collapsed;
            }
            else if (api.IsLoggedIn)
            {
                LoginStateView.Visibility = Visibility.Collapsed;
                LoginPageControl.Visibility = Visibility.Collapsed;
                MainLayout.Visibility = Visibility.Visible;
                UserInfoRun.Text = $"{api.Username} ({api.Role})";
            }
            else
            {
                LoginStateView.Visibility = Visibility.Collapsed;
                LoginPageControl.Visibility = Visibility.Visible;
                MainLayout.Visibility = Visibility.Collapsed;
            }
        });
    }

    public void NavigateTo(string tag)
    {
        ContentArea.Content = tag switch
        {
            "Dashboard" => new DashboardPage(),
            "ImageGen" => new ImageGenPage(),
            "Banana" => new BananaPage(),
            "Seedance" => new SeedancePage(),
            "Wan" => new WanPage(),
            "Veo" => new VeoPage(),
            "Grok" => new GrokPage(),
            "TaskList" => new TaskListPage(),
            "WorksGallery" => new WorksGalleryPage(),
            _ => ContentArea.Content
        };
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            NavigateTo(tag);
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => ContentArea.Content = new SettingsPage();
    private void Logout_Click(object sender, RoutedEventArgs e) => _ = _vm.Logout();
}
