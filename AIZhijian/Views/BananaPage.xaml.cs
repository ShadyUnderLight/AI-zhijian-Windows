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
    private bool _isBatchMode;

    public BananaPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPresetList();
    }

    private void ModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _isBatchMode = BatchModeRadio.IsChecked == true;
        PromptBox.Height = _isBatchMode ? 200 : 80;
        PromptBox.Text = "";
        BatchCountText.Visibility = _isBatchMode ? Visibility.Visible : Visibility.Collapsed;
        CostBanner.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "";
        GenerateBtn.Content = _isBatchMode ? "批量加入队列" : "生成";
        UpdateBatchCount();
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isBatchMode)
            UpdateBatchCount();
    }

    private void UpdateBatchCount()
    {
        if (!_isBatchMode) return;
        var raw = PromptBox.Text;
        var count = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(l => l.Length > 0);
        BatchCountText.Text = count > 0
            ? $"检测到 {count} 条提示词{'，'}点击「批量加入队列」提交"
            : "输入提示词，每行一条";
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
            Prompt = _isBatchMode ? "" : PromptBox.Text.Trim(),
            Provider = (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "third_party"
        };

        var name = dlg.Answer.Trim();
        var existing = PresetStore.FindByName(name, PresetKind.Banana);
        if (existing != null)
        {
            var overwrite = MessageBox.Show($"已存在名为 \"{name}\" 的预设，是否覆盖？", "预设已存在",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        var preset = new Preset
        {
            Name = name,
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
        if (_isBatchMode)
        {
            SubmitBatch();
            return;
        }

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

    private void SubmitBatch()
    {
        var raw = PromptBox.Text;
        var lines = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0) { StatusText.Text = "请在下框中输入提示词，每行一条"; return; }

        var longLines = lines.Where(l => l.Length > 8000).ToList();
        if (longLines.Count > 0)
        {
            StatusText.Text = $"以下行超过 8000 字符限制: {string.Join(", ", longLines.Take(3).Select(l => $"\"{l[..Math.Min(20, l.Length)]}...\""))}{(longLines.Count > 3 ? $" 等 {longLines.Count} 行" : "")}";
            return;
        }

        var provider = (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "third_party";
        var imagesSnapshot = _images.ToList();

        var queue = App.Api.GetQueue();
        var concurrencyLimit = queue.ConcurrencyLimit;

        var summary = $"批量提交 {lines.Count} 条\n" +
                      $"供应商: {provider}\n" +
                      $"并发数: {concurrencyLimit}";
        if (imagesSnapshot.Count > 0)
            summary += $"\n参考图: {imagesSnapshot.Count} 张";

        var confirm = MessageBox.Show(summary, "确认批量提交",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        GenerateBtn.IsEnabled = false;
        StatusText.Text = $"正在将 {lines.Count} 个任务加入队列...";

        try
        {
            var items = lines.Select(prompt => new GenerationQueueItem
            {
                Kind = GenerationJobKind.Banana,
                Params = new BananaJobParams
                {
                    Prompt = prompt,
                    Provider = provider,
                    ReferenceImages = imagesSnapshot.Select(f => new FileRef
                    {
                        Data = f.Data,
                        Name = f.Name,
                        Mime = f.Mime
                    }).ToList()
                }
            }).ToList();

            queue.EnqueueBatch(items, $"Banana x{items.Count}");
            StatusText.Text = $"✅ {items.Count} 个任务已加入队列";

            CostBanner.Visibility = Visibility.Visible;
            CostText.Text = $"{items.Count} 个任务等待提交 | 并发上限: {concurrencyLimit} | 可前往「⏳ 任务」页查看进度";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"错误: {ex.Message}";
        }
        finally { GenerateBtn.IsEnabled = true; }
    }
}
