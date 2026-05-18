using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AIZhijian.Views;

public partial class TaskListPage : UserControl
{
    private readonly Services.GenerationQueueStore _queue;
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
        TaskListView.ItemsSource = null;
        TaskListView.ItemsSource = _queue.Items;
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
}
