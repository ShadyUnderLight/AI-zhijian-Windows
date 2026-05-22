using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;
using System.Text.Json;

namespace AIZhijian.Views;

public partial class GrokPage : UserControl
{
    private readonly List<(byte[], string, string)> _imageFiles = new();
    private byte[]? _videoData;
    private string? _videoName, _videoMime;
    private string? _lastVideoUrl;

    public GrokPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshPresetList();
            TryApplyPendingParams();
        };
    }

    private void TryApplyPendingParams()
    {
        var pending = GenerationQueueStore.PendingEditParams;
        if (pending is not GrokJobParams p) return;
        GenerationQueueStore.PendingEditParams = null;
        PromptBox.Text = p.Prompt;
        SetComboByTag(ChannelBox, p.Channel);
        SetComboByTag(ModeBox, p.Mode);
        SetComboByTag(AspectRatioBox, p.AspectRatio);
        SetComboByTag(ResolutionBox, p.Resolution);
        if (!string.IsNullOrEmpty(p.Duration))
            SetComboByTag(DurationBox, p.Duration);
        StatusText.Text = "已从失败任务恢复参数，请重新选择文件后提交";
    }

    private string GetTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var mode = GetTag(ModeBox);
        var filter = mode is "extend" or "edit" ? "视频|*.mp4;*.mov;*.avi" : "图片/视频|*.png;*.jpg;*.jpeg;*.mp4;*.mov";
        var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() != true) return;

        var data = System.IO.File.ReadAllBytes(dlg.FileName);
        var ext = Path.GetExtension(dlg.FileName).ToLower();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
            ".mp4" => "video/mp4", ".mov" => "video/quicktime", _ => "application/octet-stream" };

        if (mime.StartsWith("video"))
        { _videoData = data; _videoName = Path.GetFileName(dlg.FileName); _videoMime = mime; }
        else _imageFiles.Add((data, Path.GetFileName(dlg.FileName), mime));

        FilesList.Children.Add(new TextBlock { Text = Path.GetFileName(dlg.FileName), FontSize = 12,
            Foreground = System.Windows.Media.Brushes.Gray });
    }

    private void RefreshPresetList()
    {
        var presets = PresetStore.GetPresets(PresetKind.Grok);
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedIndex = -1;
        DeletePresetBtn.IsEnabled = false;
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) { DeletePresetBtn.IsEnabled = false; return; }
        DeletePresetBtn.IsEnabled = true;
        var preset = PresetStore.GetPreset(id, PresetKind.Grok);
        if (preset == null) return;

        try
        {
            var p = JsonSerializer.Deserialize<GrokJobParams>(preset.ParamsJson);
            if (p == null) return;
            PromptBox.Text = p.Prompt;
            SetComboByTag(ChannelBox, p.Channel);
            SetComboByTag(ModeBox, p.Mode);
            SetComboByTag(AspectRatioBox, p.AspectRatio);
            SetComboByTag(ResolutionBox, p.Resolution);
            SetComboByTag(DurationBox, p.Duration);
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

        var p = new GrokJobParams
        {
            Prompt = PromptBox.Text.Trim(),
            Channel = GetTag(ChannelBox),
            Mode = GetTag(ModeBox),
            AspectRatio = GetTag(AspectRatioBox),
            Resolution = GetTag(ResolutionBox),
            Duration = GetTag(DurationBox)
        };

        var name = dlg.Answer.Trim();
        var existing = PresetStore.FindByName(name, PresetKind.Grok);
        if (existing != null)
        {
            var overwrite = MessageBox.Show($"已存在名为 \"{name}\" 的预设，是否覆盖？", "预设已存在",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        var preset = new Preset
        {
            Name = name,
            Kind = PresetKind.Grok,
            ParamsJson = JsonSerializer.Serialize(p)
        };
        PresetStore.SavePreset(preset);
        RefreshPresetList();
        PresetBox.SelectedValue = preset.Id;
        StatusText.Text = $"预设 \"{preset.Name}\" 已保存";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) return;
        var preset = PresetStore.GetPreset(id, PresetKind.Grok);
        if (preset == null) return;
        var result = MessageBox.Show($"确定删除预设 \"{preset.Name}\"?", "删除预设",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        PresetStore.DeletePreset(id, PresetKind.Grok);
        RefreshPresetList();
        StatusText.Text = $"预设 \"{preset.Name}\" 已删除";
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        GenerateBtn.IsEnabled = false; StatusText.Text = "提交中..."; ResultPanel.Visibility = Visibility.Collapsed;

        try
        {
            var result = await ApiService.Instance.GenerateGrokVideo(
                PromptBox.Text.Trim(), GetTag(ChannelBox), GetTag(ModeBox),
                GetTag(AspectRatioBox), GetTag(ResolutionBox), GetTag(DurationBox),
                _imageFiles, _videoData, _videoName, _videoMime);

            if (!string.IsNullOrEmpty(result.TaskId))
            { StatusText.Text = $"已提交: {result.TaskId}"; PollGrok(result.TaskId); }
            else StatusText.Text = result.Message ?? "提交失败";
        }
        catch (Exception ex) { StatusText.Text = $"错误: {ex.Message}"; }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void PollGrok(string taskId)
    {
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(3000);
            try
            {
                var poll = await ApiService.Instance.PollGrokTask(taskId);
                var status = (poll.Status ?? "").ToUpper();
                if (status == "SUCCESS")
                {
                    Dispatcher.Invoke(() =>
                    { _lastVideoUrl = poll.OutputUrl; ResultPanel.Visibility = Visibility.Visible;
                        ResultLink.Text = poll.OutputUrl ?? ""; StatusText.Text = "生成完成"; });
                    return;
                }
                if (status is "FAILED" or "CANCELLED" or "ERROR")
                { Dispatcher.Invoke(() => StatusText.Text = poll.ErrorMessage ?? "任务失败"); return; }
                Dispatcher.Invoke(() => StatusText.Text = poll.Status ?? "处理中...");
            }
            catch { }
        }
    }

    private void ResultLink_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastVideoUrl))
            try { Process.Start(new ProcessStartInfo(_lastVideoUrl) { UseShellExecute = true }); } catch { }
    }
}
