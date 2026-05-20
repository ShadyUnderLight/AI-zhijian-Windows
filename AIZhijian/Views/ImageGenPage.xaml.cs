using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class ImageGenPage : UserControl
{
    private readonly List<FileRef> _images = new();

    public ImageGenPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPresetList();
    }

    private string GetComboTag(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void PickImages_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count >= 10) { StatusText.Text = "最多10张参考图"; return; }

        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg;*.webp|所有|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames.Take(10 - _images.Count))
        {
            var data = File.ReadAllBytes(path);
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
            Prompt = PromptBox.Text.Trim(),
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
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) { StatusText.Text = "请输入提示词"; return; }

        GenerateBtn.IsEnabled = false;
        StatusText.Text = "提交中...";
        ResultsPanel.Visibility = Visibility.Collapsed;
        ResultsList.Children.Clear();

        try
        {
            var api = Services.ApiService.Instance;
            var result = _images.Count > 0
                ? await api.GenerateImageToImage(prompt, GetComboTag(ChannelBox),
                    GetComboTag(AspectRatioBox), GetComboTag(ResolutionBox),
                    GetComboTag(QualityBox), _images)
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
