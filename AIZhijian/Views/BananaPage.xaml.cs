using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class BananaPage : UserControl
{
    private readonly List<FileRef> _images = new();

    public BananaPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPresetList();
    }

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

    private void RefreshPresetList()
    {
        var presets = PresetStore.GetPresets(PresetKind.Banana);
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedIndex = -1;
        DeletePresetBtn.IsEnabled = false;
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) { DeletePresetBtn.IsEnabled = false; return; }
        DeletePresetBtn.IsEnabled = true;
        var preset = PresetStore.GetPreset(id, PresetKind.Banana);
        if (preset == null) return;

        try
        {
            var p = System.Text.Json.JsonSerializer.Deserialize<BananaJobParams>(preset.ParamsJson);
            if (p == null) return;
            PromptBox.Text = p.Prompt;
            SetComboByTag(ProviderBox, p.Provider);
        }
        catch { StatusText.Text = "加载预设失败"; }
    }

    private static void SetComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
            if (item.Tag?.ToString() == tag) { cb.SelectedItem = item; return; }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TextInputDialog("预设名称", "请输入预设名称:");
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Answer)) return;

        var p = new BananaJobParams
        {
            Prompt = PromptBox.Text.Trim(),
            Provider = (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "third_party"
        };

        var preset = new Preset
        {
            Name = dlg.Answer.Trim(),
            Kind = PresetKind.Banana,
            ParamsJson = System.Text.Json.JsonSerializer.Serialize(p)
        };
        PresetStore.SavePreset(preset);
        RefreshPresetList();
        PresetBox.SelectedValue = preset.Id;
        StatusText.Text = $"预设 \"{preset.Name}\" 已保存";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) return;
        var preset = PresetStore.GetPreset(id, PresetKind.Banana);
        if (preset == null) return;
        var result = MessageBox.Show($"确定删除预设 \"{preset.Name}\"?", "删除预设",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        PresetStore.DeletePreset(id, PresetKind.Banana);
        RefreshPresetList();
        StatusText.Text = $"预设 \"{preset.Name}\" 已删除";
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
