using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class SeedancePage : UserControl
{
    private readonly List<SeedanceAsset> _assets = new();
    private string? _lastVideoUrl;

    public SeedancePage() => InitializeComponent();

    private string GetTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void PickAssets_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "媒体文件|*.png;*.jpg;*.jpeg;*.mp4;*.mov;*.avi|所有|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            var data = System.IO.File.ReadAllBytes(path);
            var ext = Path.GetExtension(path).ToLower();
            var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".mp4" => "video/mp4", ".mov" => "video/quicktime", _ => "application/octet-stream" };
            var type = mime.StartsWith("video") ? "video" : "image";

            _assets.Add(new SeedanceAsset
            {
                Type = type, Name = Path.GetFileName(path), Mime = mime,
                Size = data.Length, Data = data
            });

            var tb = new TextBlock { Text = $"[{type}] {Path.GetFileName(path)}",
                FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray };
            AssetsPanel.Children.Add(tb);
        }
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationBox.Text, out var dur) || dur < 4 || dur > 15)
        { StatusText.Text = "时长需在4-15秒"; return; }
        if (!int.TryParse(CountBox.Text, out var cnt) || cnt < 1 || cnt > 4)
        { StatusText.Text = "数量需在1-4"; return; }

        GenerateBtn.IsEnabled = false;
        StatusText.Text = "提交中...";
        ResultPanel.Visibility = Visibility.Collapsed;

        try
        {
            var result = await ApiService.Instance.GenerateSeedanceVideo(
                PromptBox.Text.Trim(), GetTag(ModeBox), GetTag(ModelBox), GetTag(RatioBox),
                GetTag(ResolutionBox), dur, cnt, AudioCheck.IsChecked ?? false, _assets);

            if (!string.IsNullOrEmpty(result.OurTaskId))
            {
                StatusText.Text = $"已提交: {result.OurTaskId}";
                PollSeedance(result.OurTaskId);
            }
            else StatusText.Text = result.Message ?? "提交失败";
        }
        catch (Exception ex) { StatusText.Text = $"错误: {ex.Message}"; }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void PollSeedance(string taskId)
    {
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(3000);
            try
            {
                var poll = await ApiService.Instance.PollSeedanceTask(taskId);
                var status = (poll.DbStatus ?? "").ToUpper();
                if (status == "SUCCESS")
                {
                    Dispatcher.Invoke(() =>
                    {
                        _lastVideoUrl = poll.VideoUrl;
                        ResultPanel.Visibility = Visibility.Visible;
                        ResultLink.Text = poll.VideoUrl ?? "结果链接";
                        StatusText.Text = "生成完成";
                    });
                    return;
                }
                if (status is "FAILED" or "CANCELLED" or "ERROR")
                {
                    Dispatcher.Invoke(() => StatusText.Text = poll.ErrorMessage ?? "任务失败");
                    return;
                }
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
