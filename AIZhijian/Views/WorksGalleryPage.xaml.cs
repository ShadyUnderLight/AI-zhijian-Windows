using System.Windows;
using System.Windows.Controls;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class WorksGalleryPage : UserControl
{
    private readonly WorksStore _works = new();

    public WorksGalleryPage()
    {
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        var api = App.Api;
        var queue = api.GetQueue();
        foreach (var item in queue.Items)
        {
            if (item.Status is Models.GenerationQueueStatus.Succeeded or Models.GenerationQueueStatus.Failed)
                _works.AddRecord(item);
        }

        WorksItems.ItemsSource = _works.Records;
        EmptyText.Visibility = _works.Records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var ids = _works.Records.Select(r => r.Id).ToList();
        foreach (var id in ids) _works.DeleteRecord(id);
        RefreshList();
    }
}
