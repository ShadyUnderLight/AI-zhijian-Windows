using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class ImageGenPage : UserControl
{
    private readonly List<FileRef> _images = new();
    private bool _isBatchMode;

    public ImageGenPage()
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
        ResultsPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "";
        GenerateBtn.Content = _isBatchMode ? "批量加入队列" : "生成图片";
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

    private string GetComboTag(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private async void PickImages_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count >= 10) { StatusText.Text = "最多10张参考图"; return; }

        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp|所有|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        var maxSize = 25L * 1024 * 1024;
        var remaining = 10 - _images.Count;
        var added = 0;
        var totalSelected = dlg.FileNames.Length;

        foreach (var path in dlg.FileNames.Take(remaining))
        {
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length > maxSize)
                {
                    StatusText.Text = $"文件 {fi.Name} 超过25MB限制，已跳过";
                    continue;
                }

                var data = await File.ReadAllBytesAsync(path);

                var ext = Path.GetExtension(path).ToLowerInvariant();
                var mime = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    _ => null
                };
                if (mime == null)
                {
                    StatusText.Text = $"不支持的文件格式 \"{ext}\"，已跳过";
                    continue;
                }

                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }

                _images.Add(new FileRef { Data = data, Name = fi.Name, Mime = mime });

                var dims = bmp.PixelWidth > 0 && bmp.PixelHeight > 0
                    ? $"{bmp.PixelWidth}x{bmp.PixelHeight}"
                    : "尺寸未知";

                var thumb = new Border
                {
                    Width = 80, Height = 80,
                    BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 6),
                    Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill }
                };

                var info = new TextBlock
                {
                    Text = $"{fi.Name}\n{dims}",
                    FontSize = 11, Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 6, 0)
                };

                var cell = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 8) };
                cell.Children.Add(thumb);
                cell.Children.Add(info);
                ImagesPanel.Children.Add(cell);
                added++;
            }
            catch (IOException ex)
            {
                StatusText.Text = $"读取文件 {Path.GetFileName(path)} 失败: {ex.Message}";
            }
            catch
            {
                StatusText.Text = $"文件 {Path.GetFileName(path)} 不是有效的图片，已跳过";
            }
        }

        StatusText.Text = totalSelected > remaining
            ? $"{added} 张已添加，{totalSelected - remaining} 张因数量限制被跳过"
            : $"{added} 张参考图已添加";

        UpdateImageModeState();
    }

    private void ClearImages_Click(object sender, RoutedEventArgs e)
    {
        _images.Clear();
        ImagesPanel.Children.Clear();
        UpdateImageModeState();
    }

    private void UpdateImageModeState()
    {
        var hasImages = _images.Count > 0;
        ClearImagesBtn.IsEnabled = hasImages;
        PhotoRealCheck.IsEnabled = !hasImages;
    }

    private void RefreshPresetList()
    {
        var presets = PresetStore.GetPresets(PresetKind.GptImage);
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedIndex = -1;
        DeletePresetBtn.IsEnabled = false;
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) { DeletePresetBtn.IsEnabled = false; return; }
        DeletePresetBtn.IsEnabled = true;
        var preset = PresetStore.GetPreset(id, PresetKind.GptImage);
        if (preset == null) return;

        try
        {
            var p = System.Text.Json.JsonSerializer.Deserialize<GptImageJobParams>(preset.ParamsJson);
            if (p == null) return;
            PromptBox.Text = p.Prompt;
            SetComboByTag(ChannelBox, p.Channel);
            SetComboByTag(AspectRatioBox, p.AspectRatio);
            SetComboByTag(ResolutionBox, p.Resolution);
            SetComboByTag(QualityBox, p.Quality);
            PhotoRealCheck.IsChecked = p.PhotoReal;
        }
        catch { StatusText.Text = "加载预设失败"; }
    }

    private static void SetComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
        {
            if (item.Tag?.ToString() == tag) { cb.SelectedItem = item; return; }
        }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TextInputDialog("预设名称", "请输入预设名称:");
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Answer)) return;

        var p = new GptImageJobParams
        {
            Prompt = _isBatchMode ? "" : PromptBox.Text.Trim(),
            Channel = GetComboTag(ChannelBox),
            AspectRatio = GetComboTag(AspectRatioBox),
            Resolution = GetComboTag(ResolutionBox),
            Quality = GetComboTag(QualityBox),
            PhotoReal = PhotoRealCheck.IsChecked ?? false
        };

        var name = dlg.Answer.Trim();
        var existing = PresetStore.FindByName(name, PresetKind.GptImage);
        if (existing != null)
        {
            var overwrite = MessageBox.Show($"已存在名为 \"{name}\" 的预设，是否覆盖？", "预设已存在",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        var preset = new Preset
        {
            Name = name,
            Kind = PresetKind.GptImage,
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
        var preset = PresetStore.GetPreset(id, PresetKind.GptImage);
        if (preset == null) return;
        var result = MessageBox.Show($"确定删除预设 \"{preset.Name}\"?", "删除预设",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        PresetStore.DeletePreset(id, PresetKind.GptImage);
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
        StatusText.Text = "提交中...";
        ResultsPanel.Visibility = Visibility.Collapsed;
        ResultsList.Children.Clear();

        try
        {
            var api = Services.ApiService.Instance;
            var imagesSnapshot = _images.ToList();
            var result = imagesSnapshot.Count > 0
                ? await api.GenerateImageToImage(prompt, GetComboTag(ChannelBox),
                    GetComboTag(AspectRatioBox), GetComboTag(ResolutionBox),
                    GetComboTag(QualityBox), imagesSnapshot)
                : await api.GenerateImage(prompt, GetComboTag(ChannelBox),
                    GetComboTag(AspectRatioBox), GetComboTag(ResolutionBox),
                    GetComboTag(QualityBox), PhotoRealCheck.IsChecked ?? false);

            StatusText.Text = result.Success
                ? $"任务已提交: {result.OurTaskId}"
                : result.Message ?? "提交失败";

            if (result.Success && !string.IsNullOrEmpty(result.OurTaskId))
                PollResult(result.OurTaskId);
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

        var channel = GetComboTag(ChannelBox);
        var aspectRatio = GetComboTag(AspectRatioBox);
        var resolution = GetComboTag(ResolutionBox);
        var quality = GetComboTag(QualityBox);
        var photoReal = PhotoRealCheck.IsChecked ?? false;
        var imagesSnapshot = _images.ToList();

        var queue = App.Api.GetQueue();
        var concurrencyLimit = queue.ConcurrencyLimit;

        var summary = $"批量提交 {lines.Count} 条\n" +
                      $"渠道: {channel}\n" +
                      $"画幅: {aspectRatio}\n" +
                      $"分辨率: {resolution}\n" +
                      $"质量: {quality}\n" +
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
                Kind = GenerationJobKind.GptImage,
                Params = new GptImageJobParams
                {
                    Prompt = prompt,
                    Channel = channel,
                    AspectRatio = aspectRatio,
                    Resolution = resolution,
                    Quality = quality,
                    PhotoReal = photoReal,
                    ReferenceImages = imagesSnapshot.Select(f => new FileRef
                    {
                        Data = f.Data,
                        Name = f.Name,
                        Mime = f.Mime
                    }).ToList()
                }
            }).ToList();

            queue.EnqueueBatch(items, $"GPT-Image x{items.Count}");
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

    private async void PollResult(string taskId)
    {
        try
        {
            for (int i = 0; i < 120; i++)
            {
                await Task.Delay(3000);
                var poll = await Services.ApiService.Instance.PollImageTask(taskId);
                var status = (poll.DbStatus ?? "").ToUpper();

                if (status == "SUCCESS")
                {
                    Dispatcher.Invoke(() => ShowResults(poll.ResultUrls));
                    StatusText.Text = "生成完成";
                    return;
                }
                if (status is "FAILED" or "CANCELLED")
                {
                    StatusText.Text = poll.ErrorMessage ?? "任务失败";
                    return;
                }
                StatusText.Text = poll.RhStatus ?? poll.DbStatus ?? "处理中...";
            }
            StatusText.Text = "任务超时";
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => StatusText.Text = $"轮询错误: {ex.Message}");
        }
    }

    private void ShowResults(List<string>? urls)
    {
        ResultsPanel.Visibility = Visibility.Visible;
        ResultsList.Children.Clear();
        if (urls == null) return;

        foreach (var url in urls)
        {
            var link = new TextBlock { Margin = new Thickness(0, 2, 0, 2) };
            link.Inlines.Add(new Run(url) { Foreground = System.Windows.Media.Brushes.Blue });
            link.Cursor = System.Windows.Input.Cursors.Hand;
            link.MouseLeftButtonDown += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            };
            ResultsList.Children.Add(link);
        }
    }
}
