using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public class BatchGroup
{
    public Guid? BatchId { get; init; }
    public string DisplayName { get; set; } = "";
    public bool IsPaused { get; set; }
    public string PauseBtnText => IsPaused ? "▶ 继续" : "⏸ 暂停";
    public bool IsExpanded { get; set; } = true;
    public string ChevronText => IsExpanded ? "▼" : "▶";
    public string StatusLabelText { get; set; } = "";
    public List<GenerationQueueItem> Items { get; init; } = new();
}

public partial class TaskListPage : UserControl
{
    private readonly GenerationQueueStore _queue;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<Guid, bool> _batchExpandedStates = new();

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

                var expanded = batchId == null || _batchExpandedStates.GetValueOrDefault(batchId.Value, true);

                var items = g.OrderBy(i => i.CreatedAt).ToList();
                var pending = items.Count(i => i.Status == GenerationQueueStatus.Pending);
                var active = items.Count(i => i.Status is GenerationQueueStatus.Submitting or GenerationQueueStatus.Polling);
                var done = items.Count(i => i.Status == GenerationQueueStatus.Succeeded);
                var failed = items.Count(i => i.Status == GenerationQueueStatus.Failed);

                var statusParts = new List<string>();
                if (pending > 0) statusParts.Add($"待提交 {pending}");
                if (active > 0) statusParts.Add($"进行中 {active}");
                if (done > 0) statusParts.Add($"完成 {done}");
                if (failed > 0) statusParts.Add($"失败 {failed}");

                return new BatchGroup
                {
                    BatchId = batchId,
                    DisplayName = first.BatchName ?? "未分组",
                    IsPaused = isPaused,
                    IsExpanded = expanded,
                    StatusLabelText = statusParts.Count > 0 ? string.Join(" | ", statusParts) : "",
                    Items = items
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

    private static Guid? GetBatchId(object sender)
    {
        return sender switch
        {
            Button btn => btn.Tag as Guid?,
            MenuItem mi => mi.Tag as Guid?,
            FrameworkElement fe => fe.Tag as Guid?,
            _ => null
        };
    }

    private void Chevron_Click(object sender, RoutedEventArgs e)
    {
        if (GetBatchId(sender) is Guid batchId)
        {
            var current = _batchExpandedStates.GetValueOrDefault(batchId, true);
            _batchExpandedStates[batchId] = !current;
            Refresh();
        }
    }

    private void RenameBatch(Guid batchId)
    {
        var currentName = _queue.Items.FirstOrDefault(i => i.BatchId == batchId)?.BatchName ?? "";
        var dialog = new InputDialog("重命名批次", "批次名称:", currentName);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value))
        {
            _queue.RenameBatch(batchId, dialog.Value.Trim());
            Refresh();
        }
    }

    private void BatchTitle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && GetBatchId(sender) is Guid batchId)
            RenameBatch(batchId);
    }

    private void BatchPauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (GetBatchId(sender) is Guid batchId)
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
        if (GetBatchId(sender) is Guid batchId)
            RenameBatch(batchId);
    }

    private void BatchRetry_Click(object sender, RoutedEventArgs e)
    {
        if (GetBatchId(sender) is Guid batchId)
        {
            _queue.RetryBatch(batchId);
            Refresh();
        }
    }

    private void BatchCancel_Click(object sender, RoutedEventArgs e)
    {
        if (GetBatchId(sender) is Guid batchId)
        {
            _queue.CancelBatch(batchId);
            Refresh();
        }
    }

    private void BatchClear_Click(object sender, RoutedEventArgs e)
    {
        if (GetBatchId(sender) is Guid batchId)
        {
            _queue.ClearBatch(batchId);
            Refresh();
        }
    }

    private void BatchMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is BatchGroup group && group.BatchId is Guid batchId)
        {
            var menu = new ContextMenu();

            AddMenuItem(menu, group.PauseBtnText, batchId, BatchPauseResume_Click);
            AddMenuItem(menu, "✏ 重命名", batchId, BatchRename_Click);
            AddMenuItem(menu, "重试失败", batchId, BatchRetry_Click);
            AddMenuItem(menu, "✕ 取消", batchId, BatchCancel_Click);
            AddMenuItem(menu, "清除", batchId, BatchClear_Click);

            btn.ContextMenu = menu;
            menu.IsOpen = true;
        }
    }

    private static void AddMenuItem(ContextMenu menu, string header, Guid batchId, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header, Tag = batchId };
        item.Click += handler;
        menu.Items.Add(item);
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
