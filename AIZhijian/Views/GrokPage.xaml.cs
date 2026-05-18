using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class GrokPage : UserControl
{
    private readonly List<(byte[], string, string)> _imageFiles = new();
    private byte[]? _videoData;
    private string? _videoName, _videoMime;
    private string? _lastVideoUrl;

    public GrokPage() => InitializeComponent();

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
