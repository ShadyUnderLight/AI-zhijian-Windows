using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public class BatchGroup
{
    public Guid? BatchId { get; init; }
    public string DisplayName { get; set; } = "";
    public string ItemCountText => $"{Items.Count} 个任务";
    public bool IsPaused { get; set; }
    public string PauseBtnText => IsPaused ? "▶ 恢复" : "⏸ 暂停";
    public List<GenerationQueueItem> Items { get; init; } = new();
}

public partial class TaskListPage : UserControl
{
    private readonly GenerationQueueStore _queue;
    private readonly DispatcherTimer _timer;

    public TaskListPage()
    {
        InitializeComponent();
        _queue = App.Api.GetQueue();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    private void Refresh()
    {
        var groups = _queue.Items
            .GroupBy(i => i.BatchId)
            .OrderBy(g => g.Key == null ? 1 : 0)
            .ThenBy(g => g.Min(i => i.CreatedAt))
            .Select(g =>
            {
                var first = g.First();
                var batchId = g.Key;
                var isPaused = batchId != null && _queue.PausedBatches.Contains(batchId.Value);
                return new BatchGroup
                {
                    BatchId = batchId,
                    DisplayName = first.BatchName ?? "未分组",
                    IsPaused = isPaused,
                    Items = g.OrderBy(i => i.CreatedAt).ToList()
                };
            })
            .ToList();

        BatchContainer.ItemsSource = groups;
        StatsText.Text = _queue.StatsSummary;
        PauseBtn.Content = _queue.IsPaused ? "▶ 继续" : "⏸ 暂停";
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_queue.IsPaused) _queue.ResumeQueue();
        else _queue.PauseQueue();
        Refresh();
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e) { _queue.ClearCompleted(); Refresh(); }
    private void ClearFailed_Click(object sender, RoutedEventArgs e) { _queue.ClearFailed(); Refresh(); }
    private void CancelAll_Click(object sender, RoutedEventArgs e) { _queue.CancelAndClearAll(); Refresh(); }

    private void BatchPauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid batchId)
        {
            if (_queue.PausedBatches.Contains(batchId))
                _queue.ResumeBatch(batchId);
            else
                _queue.PauseBatch(batchId);
            Refresh();
        }
    }

    private void BatchRename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid batchId)
        {
            var currentName = _queue.Items.FirstOrDefault(i => i.BatchId == batchId)?.BatchName ?? "";
            var dialog = new InputDialog("重命名批次", "批次名称:", currentName);
            if (dialog.ShowDialog() == true)
            {
                _queue.RenameBatch(batchId, dialog.Value);
                Refresh();
            }
        }
    }

    private void BatchRetry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid batchId)
        {
            _queue.RetryBatch(batchId);
            Refresh();
        }
    }

    private void BatchCancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid batchId)
        {
            _queue.CancelBatch(batchId);
            Refresh();
        }
    }

    private void BatchClear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid batchId)
        {
            _queue.ClearBatch(batchId);
            Refresh();
        }
    }

    private void RetryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            _queue.RetryFailed(id);
            Refresh();
        }
    }
}

public class InputDialog : Window
{
    public string Value { get; private set; } = "";

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 360;
        Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.MainWindow;
        ResizeMode = ResizeMode.NoResize;

        var stack = new StackPanel { Margin = new Thickness(16) };

        stack.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });

        var textBox = new TextBox { Text = defaultValue, Height = 30 };
        stack.Children.Add(textBox);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var okBtn = new Button
        {
            Content = "确定",
            Width = 80,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        okBtn.Click += (_, _) => { Value = textBox.Text; DialogResult = true; Close(); };
        btnPanel.Children.Add(okBtn);

        var cancelBtn = new Button
        {
            Content = "取消",
            Width = 80,
            Height = 30,
            IsCancel = true,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(cancelBtn);

        stack.Children.Add(btnPanel);
        Content = stack;

        textBox.SelectAll();
        textBox.Focus();
    }
}
