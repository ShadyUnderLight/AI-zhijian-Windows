using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class VeoPage : UserControl
{
    private VeoJobParams _params = new();
    private string? _lastVideoUrl;

    public VeoPage()
    {
        InitializeComponent();
        PopulateChannels();
    }

    private string GetTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    private static ComboBoxItem Item(string content, string tag) => new() { Content = content, Tag = tag };

    private void PopulateChannels()
    {
        ChannelBox.Items.Clear();
        foreach (var (value, label) in VeoRules.Channels)
            ChannelBox.Items.Add(Item($"{label} ({value})", value));
        ChannelBox.SelectedIndex = 0;
    }

    private void ChannelChanged(object s, SelectionChangedEventArgs e) => RefreshModels();
    private void ModelChanged(object s, SelectionChangedEventArgs e) => RefreshModes();

    private void ModeChanged(object s, SelectionChangedEventArgs e) => RefreshUI();

    private void RefreshModels()
    {
        var channel = GetTag(ChannelBox);
        ModelBox.Items.Clear();
        foreach (var m in VeoRules.ValidModels(channel))
            ModelBox.Items.Add(Item(m, m));
        ModelBox.SelectedIndex = ModelBox.Items.Count > 0 ? 0 : -1;
    }

    private void RefreshModes()
    {
        var channel = GetTag(ChannelBox); var model = GetTag(ModelBox);
        ModeBox.Items.Clear();
        foreach (var (value, label) in VeoRules.ValidModes(channel, model))
            ModeBox.Items.Add(Item(label, value));
        ModeBox.SelectedIndex = ModeBox.Items.Count > 0 ? 0 : -1;
    }

    private void RefreshUI()
    {
        var channel = GetTag(ChannelBox); var model = GetTag(ModelBox); var mode = GetTag(ModeBox);

        AspectRatioPanel.Visibility = mode is not ("reference" or "extend") ? Visibility.Visible : Visibility.Collapsed;
        DurationPanel.Visibility = VeoRules.SupportsDuration(channel, model, mode) ? Visibility.Visible : Visibility.Collapsed;
        AudioCheckBox.Visibility = VeoRules.SupportsAudio(channel, model, mode) ? Visibility.Visible : Visibility.Collapsed;
        NegPromptPanel.Visibility = VeoRules.SupportsNegativePrompt(channel) ? Visibility.Visible : Visibility.Collapsed;

        ResolutionBox.Items.Clear();
        foreach (var r in VeoRules.ValidResolutions(channel, model, mode))
            ResolutionBox.Items.Add(Item(r.Label, r.Value));
        ResolutionBox.SelectedIndex = 0;
    }

    private void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        var mode = GetTag(ModeBox);
        string filter = mode switch
        {
            "extend" or "edit" => "视频|*.mp4;*.mov;*.avi",
            _ => "图片/视频|*.png;*.jpg;*.jpeg;*.mp4;*.mov"
        };

        var dlg = new OpenFileDialog { Filter = filter, Multiselect = mode == "image" };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
            AddFile(path);
    }

    private void AddFile(string path)
    {
        var data = System.IO.File.ReadAllBytes(path);
        var ext = Path.GetExtension(path).ToLower();
        var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
            ".mp4" => "video/mp4", ".mov" => "video/quicktime", _ => "application/octet-stream" };
        var mode = GetTag(ModeBox);

        if (mime.StartsWith("video")) { _params.VideoData = data; _params.VideoName = Path.GetFileName(path); _params.VideoMime = mime; }
        else switch (mode)
        {
            case "image": _params.ImageFiles.Add(new FileRef { Data = data, Name = Path.GetFileName(path), Mime = mime }); break;
            case "start_end":
                if (_params.FirstImageData == null) { _params.FirstImageData = data; _params.FirstImageName = Path.GetFileName(path); _params.FirstImageMime = mime; }
                else { _params.LastImageData = data; _params.LastImageName = Path.GetFileName(path); _params.LastImageMime = mime; }
                break;
            case "reference":
                if (_params.Ref1Data == null) _params.Ref1Data = (data, Path.GetFileName(path), mime);
                else if (_params.Ref2Data == null) _params.Ref2Data = (data, Path.GetFileName(path), mime);
                else if (_params.Ref3Data == null) _params.Ref3Data = (data, Path.GetFileName(path), mime);
                break;
            default: _params.ImageData = data; _params.ImageName = Path.GetFileName(path); _params.ImageMime = mime; break;
        }
        FilesList.Children.Add(new TextBlock { Text = Path.GetFileName(path), FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray });
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        GenerateBtn.IsEnabled = false; StatusText.Text = "提交中..."; ResultPanel.Visibility = Visibility.Collapsed;

        _params.Channel = GetTag(ChannelBox); _params.Model = GetTag(ModelBox); _params.Mode = GetTag(ModeBox);
        _params.Prompt = PromptBox.Text; _params.AspectRatio = GetTag(AspectRatioBox);
        _params.Resolution = GetTag(ResolutionBox);
        _params.GenerateAudio = AudioCheckBox.IsChecked ?? false;
        _params.NegativePrompt = NegPromptBox.Text;

        if (VeoRules.ShouldSendDuration(_params.Channel, _params.Model, _params.Mode))
            _params.Duration = GetTag(DurationBox);
        else
            _params.Duration = VeoRules.FixedDuration(_params.Channel, _params.Model, _params.Mode) ?? _params.Duration;

        try
        {
            var result = await ApiService.Instance.GenerateVeoVideo(_params);
            if (!string.IsNullOrEmpty(result.OurTaskId))
            { StatusText.Text = $"已提交: {result.OurTaskId}"; PollVeo(result.OurTaskId); }
            else StatusText.Text = result.Message ?? "提交失败";
        }
        catch (Exception ex) { StatusText.Text = $"错误: {ex.Message}"; }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void PollVeo(string taskId)
    {
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(3000);
            try
            {
                var poll = await ApiService.Instance.PollVeoTask(taskId);
                var status = (poll.DbStatus ?? "").ToUpper();
                if (status == "SUCCESS")
                {
                    Dispatcher.Invoke(() =>
                    { _lastVideoUrl = poll.VideoUrl; ResultPanel.Visibility = Visibility.Visible;
                        ResultLink.Text = poll.VideoUrl ?? ""; StatusText.Text = "生成完成"; });
                    return;
                }
                if (status is "FAILED" or "CANCELLED" or "ERROR")
                { Dispatcher.Invoke(() => StatusText.Text = poll.ErrorMessage ?? "任务失败"); return; }
                Dispatcher.Invoke(() => StatusText.Text = poll.RhStatus ?? poll.DbStatus ?? "处理中...");
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
