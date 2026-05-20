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

public partial class WanPage : UserControl
{
    private byte[]? _imageData;
    private string? _imageName, _imageMime;
    private FileRef? _firstFrame, _lastFrame;
    private string? _lastVideoUrl;

    public WanPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPresetList();
    }

    private void ModeBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        var isImage = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "image";
        ImagePickerPanel.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
        FramePickerPanel.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PickImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg" };
        if (dlg.ShowDialog() != true) return;
        _imageData = System.IO.File.ReadAllBytes(dlg.FileName);
        _imageName = Path.GetFileName(dlg.FileName);
        _imageMime = Path.GetExtension(dlg.FileName).ToLower() == ".png" ? "image/png" : "image/jpeg";
        ImageFileLabel.Text = _imageName;
    }

    private void PickFirstFrame_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg" };
        if (dlg.ShowDialog() != true) return;
        var data = System.IO.File.ReadAllBytes(dlg.FileName);
        _firstFrame = new FileRef { Data = data, Name = Path.GetFileName(dlg.FileName),
            Mime = Path.GetExtension(dlg.FileName).ToLower() == ".png" ? "image/png" : "image/jpeg" };
        FirstFrameLabel.Text = _firstFrame.Name;
    }

    private void PickLastFrame_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg" };
        if (dlg.ShowDialog() != true) return;
        var data = System.IO.File.ReadAllBytes(dlg.FileName);
        _lastFrame = new FileRef { Data = data, Name = Path.GetFileName(dlg.FileName),
            Mime = Path.GetExtension(dlg.FileName).ToLower() == ".png" ? "image/png" : "image/jpeg" };
        LastFrameLabel.Text = _lastFrame.Name;
    }

    private void RefreshPresetList()
    {
        var presets = PresetStore.GetPresets(PresetKind.Wan);
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedIndex = -1;
        DeletePresetBtn.IsEnabled = false;
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) { DeletePresetBtn.IsEnabled = false; return; }
        DeletePresetBtn.IsEnabled = true;
        var preset = PresetStore.GetPreset(id, PresetKind.Wan);
        if (preset == null) return;

        try
        {
            var p = JsonSerializer.Deserialize<WanJobParams>(preset.ParamsJson);
            if (p == null) return;
            PromptBox.Text = p.Prompt;
            SetComboByTag(ModeBox, p.Mode);
            WidthBox.Text = p.Width.ToString();
            HeightBox.Text = p.Height.ToString();
            SecondsBox.Text = p.Seconds.ToString();
            Enable48GCheck.IsChecked = p.Enable48G;
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
        if (!int.TryParse(WidthBox.Text, out var w) || !int.TryParse(HeightBox.Text, out var h)
            || !int.TryParse(SecondsBox.Text, out var s))
        { StatusText.Text = "尺寸/时长格式错误"; return; }

        var dlg = new TextInputDialog("预设名称", "请输入预设名称:");
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Answer)) return;

        var mode = (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "image";
        var p = new WanJobParams
        {
            Prompt = PromptBox.Text.Trim(),
            Mode = mode,
            Width = w,
            Height = h,
            Seconds = s,
            Enable48G = Enable48GCheck.IsChecked ?? false
        };

        var name = dlg.Answer.Trim();
        var existing = PresetStore.FindByName(name, PresetKind.Wan);
        if (existing != null)
        {
            var overwrite = MessageBox.Show($"已存在名为 \"{name}\" 的预设，是否覆盖？", "预设已存在",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        var preset = new Preset
        {
            Name = name,
            Kind = PresetKind.Wan,
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
        var preset = PresetStore.GetPreset(id, PresetKind.Wan);
        if (preset == null) return;
        var result = MessageBox.Show($"确定删除预设 \"{preset.Name}\"?", "删除预设",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        PresetStore.DeletePreset(id, PresetKind.Wan);
        RefreshPresetList();
        StatusText.Text = $"预设 \"{preset.Name}\" 已删除";
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out var w) || !int.TryParse(HeightBox.Text, out var h) || !int.TryParse(SecondsBox.Text, out var s))
        { StatusText.Text = "尺寸/时长格式错误"; return; }

        var mode = ((ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "image";
        GenerateBtn.IsEnabled = false;
        ResultPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "提交中...";

        try
        {
            var api = ApiService.Instance;
            Models.TaskSubmitResponse result;

            if (mode == "image")
            {
                if (_imageData == null) { StatusText.Text = "请选择图片"; return; }
                result = await api.GenerateWanVideo(_imageData, _imageName!, _imageMime!,
                    PromptBox.Text.Trim(), w, h, s);
            }
            else
            {
                if (_firstFrame == null || _lastFrame == null) { StatusText.Text = "请选择首帧和尾帧"; return; }
                result = await api.GenerateWanFirstLastVideo(_firstFrame, _lastFrame,
                    PromptBox.Text.Trim(), s, Enable48GCheck.IsChecked ?? false);
            }

            if (!string.IsNullOrEmpty(result.TaskId))
            { StatusText.Text = $"已提交: {result.TaskId}"; PollWan(result.TaskId); }
            else StatusText.Text = result.Message ?? "提交失败";
        }
        catch (Exception ex) { StatusText.Text = $"错误: {ex.Message}"; }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void PollWan(string taskId)
    {
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(3000);
            try
            {
                var poll = await ApiService.Instance.PollMediaTask(taskId);
                var status = (poll.Status ?? poll.TaskStatus ?? "").ToUpper();
                if (status is "SUCCESS" or "COMPLETED")
                {
                    var url = poll.VideoUrl ?? poll.OutputUrl;
                    Dispatcher.Invoke(() =>
                    {
                        _lastVideoUrl = url;
                        ResultPanel.Visibility = Visibility.Visible;
                        ResultLink.Text = url ?? "视频链接";
                        StatusText.Text = "生成完成";
                    });
                    return;
                }
                if (status is "FAILED" or "CANCELLED" or "ERROR")
                {
                    Dispatcher.Invoke(() => StatusText.Text = poll.ErrorMessage ?? poll.Message ?? "任务失败");
                    return;
                }
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
