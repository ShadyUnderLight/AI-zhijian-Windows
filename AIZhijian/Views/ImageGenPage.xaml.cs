using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace AIZhijian.Views;

public partial class ImageGenPage : UserControl
{
    public ImageGenPage() => InitializeComponent();

    private string GetComboTag(ComboBox cb)
        => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

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
            var result = await api.GenerateImage(prompt, GetComboTag(ChannelBox),
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
