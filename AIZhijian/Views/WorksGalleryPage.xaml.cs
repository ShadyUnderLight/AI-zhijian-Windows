using System.Windows;
using System.Windows.Controls;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class WorksGalleryPage : UserControl
{
    private readonly WorksStore _works = new();
    private bool _showFavoritesOnly;

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

        var source = _showFavoritesOnly
            ? _works.Records.Where(r => _works.FavoriteIds.Contains(r.Id))
            : _works.Records;

        var displayList = source.Select(r => new
        {
            r.Id,
            r.Prompt,
            r.Kind,
            r.CreatedAt,
            r.ErrorMessage,
            IsFavorite = _works.FavoriteIds.Contains(r.Id),
            FavoriteStar = _works.FavoriteIds.Contains(r.Id) ? "⭐" : "☆"
        }).ToList();

        WorksItems.ItemsSource = displayList;
        EmptyText.Text = _showFavoritesOnly ? "暂无收藏作品" : "暂无作品";
        EmptyText.Visibility = displayList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
            _works.ToggleFavorite(id);
        RefreshList();
    }

    private void FavFilter_Click(object sender, RoutedEventArgs e)
    {
        _showFavoritesOnly = !_showFavoritesOnly;
        FavFilterBtn.Content = _showFavoritesOnly ? "★ 显示全部" : "⭐ 仅收藏";
        FavFilterBtn.Foreground = _showFavoritesOnly
            ? System.Windows.Media.Brushes.White
            : (System.Windows.Media.Brush)FindResource("TextPrimary");
        FavFilterBtn.Background = _showFavoritesOnly
            ? (System.Windows.Media.Brush)FindResource("AccentBrush")
            : System.Windows.Media.Brushes.Transparent;
        RefreshList();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var ids = _works.Records.Select(r => r.Id).ToList();
        foreach (var id in ids) _works.DeleteRecord(id);
        RefreshList();
    }
}
