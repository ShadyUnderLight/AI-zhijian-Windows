using System.Text.Json;
using AIZhijian.Models;

namespace AIZhijian.Services;

public class WorksStore
{
    private readonly List<WorkRecord> _records = new();
    private readonly HashSet<string> _favoriteIds = new();
    private const string RecordsKey = "WorksStore.records";
    private const string FavoritesKey = "WorksStore.favorites";

    public List<WorkRecord> Records => _records;
    public HashSet<string> FavoriteIds => _favoriteIds;

    public event Action? StateChanged;

    public WorksStore() => Load();
    public void Notify() => StateChanged?.Invoke();

    public void AddRecord(GenerationQueueItem item)
    {
        if (item.Status is not (GenerationQueueStatus.Succeeded or GenerationQueueStatus.Failed)) return;

        var record = new WorkRecord
        {
            Id = item.Id,
            Kind = item.Kind,
            Prompt = item.Summary ?? "",
            ResultUrls = item.ResultUrls,
            VideoUrl = item.VideoUrl,
            ErrorMessage = item.ErrorMessage,
            CreatedAt = item.CreatedAt,
            CompletedAt = item.CompletedAt,
            Metadata = new WorkRecordMetadata()
        };

        _records.RemoveAll(r => r.Id == record.Id);
        _records.Add(record);
        if (_records.Count > 500) _records.RemoveRange(0, _records.Count - 500);
        Persist();
    }

    public void ToggleFavorite(string id)
    {
        if (_favoriteIds.Contains(id)) _favoriteIds.Remove(id);
        else _favoriteIds.Add(id);
        PersistFavorites();
    }

    public void DeleteRecord(string id)
    {
        _records.RemoveAll(r => r.Id == id);
        _favoriteIds.Remove(id);
        Persist();
        PersistFavorites();
    }

    private void Persist()
    {
        try { AIZhijian.Properties.Settings.Default.WorksRecords = JsonSerializer.Serialize(_records); AIZhijian.Properties.Settings.Default.Save(); } catch { }
    }

    private void PersistFavorites()
    {
        try { AIZhijian.Properties.Settings.Default.WorksFavorites = JsonSerializer.Serialize(_favoriteIds.ToList()); AIZhijian.Properties.Settings.Default.Save(); } catch { }
    }

    private void Load()
    {
        try
        {
            if (!string.IsNullOrEmpty(AIZhijian.Properties.Settings.Default.WorksRecords))
            {
                var records = JsonSerializer.Deserialize<List<WorkRecord>>(AIZhijian.Properties.Settings.Default.WorksRecords);
                if (records != null) _records.AddRange(records);
            }
            if (!string.IsNullOrEmpty(AIZhijian.Properties.Settings.Default.WorksFavorites))
            {
                var favs = JsonSerializer.Deserialize<List<string>>(AIZhijian.Properties.Settings.Default.WorksFavorites);
                if (favs != null) favs.ForEach(id => _favoriteIds.Add(id));
            }
        }
        catch { }
    }
}
