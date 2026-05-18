using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AIZhijian.Models;

namespace AIZhijian.Views;

public partial class BananaPage : UserControl
{
    private readonly List<FileRef> _images = new();

    public BananaPage() => InitializeComponent();

    private void PickImages_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count >= 3) { StatusText.Text = "最多3张参考图"; return; }

        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp|所有|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames.Take(3 - _images.Count))
        {
            var data = System.IO.File.ReadAllBytes(path);
            var mime = Path.GetExtension(path).ToLower() switch
            {
                ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp", _ => "image/png"
            };
            _images.Add(new FileRef { Data = data, Name = Path.GetFileName(path), Mime = mime });

            var tb = new TextBlock { Text = Path.GetFileName(path), FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) };
            ImagesPanel.Children.Add(tb);
        }
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) { StatusText.Text = "请输入提示词"; return; }

        GenerateBtn.IsEnabled = false;
        StatusText.Text = "生成中...";

        try
        {
            var provider = (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "third_party";
            var data = await Services.ApiService.Instance.GenerateBanana(prompt, provider, _images);

            Dispatcher.Invoke(() =>
            {
                ResultPanel.Visibility = Visibility.Visible;
                using var ms = new System.IO.MemoryStream(data);
                var bmp = new BitmapImage();
                bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms; bmp.EndInit();
                ResultImage.Source = bmp;
                StatusText.Text = "生成完成";
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"错误: {ex.Message}";
        }
        finally { GenerateBtn.IsEnabled = true; }
    }
}
