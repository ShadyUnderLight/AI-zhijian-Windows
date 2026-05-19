using System.Text.Json;
using System.Text.Json.Serialization;
using AIZhijian.Models;

namespace AIZhijian.Services;

public class GenerationQueueStore
{
    private readonly ApiService _api;
    private readonly GenerationTaskExecutor _executor;
    private readonly List<GenerationQueueItem> _items = new();
    private CancellationTokenSource? _processCts;
    private bool _isProcessing;

    public List<GenerationQueueItem> Items => _items;
    public bool IsPaused { get; set; }
    public bool IsProcessing => _isProcessing;
    public int ConcurrencyLimit { get; set; } = 3;
    public int MaxConsecutivePollFailures { get; set; } = 5;

    public event Action? StateChanged;
    private readonly Action<string>? _log;

    public GenerationQueueStore(ApiService api, Action<string>? log = null)
    {
        _api = api;
        _executor = new GenerationTaskExecutor(api);
        _log = log;
    }

    public string StatsSummary
    {
        get
        {
            var pending = _items.Count(i => i.Status == GenerationQueueStatus.Pending);
            var submitting = _items.Count(i => i.Status == GenerationQueueStatus.Submitting);
            var polling = _items.Count(i => i.Status == GenerationQueueStatus.Polling);
            var succeeded = _items.Count(i => i.Status == GenerationQueueStatus.Succeeded);
            var failed = _items.Count(i => i.Status == GenerationQueueStatus.Failed);
            return $"待提交 {pending} | 提交中 {submitting} | 轮询中 {polling} | 完成 {succeeded} | 失败 {failed}";
        }
    }

    public void Enqueue(GenerationQueueItem item)
    {
        _items.Add(item);
        NotifyAndProcess();
    }

    public void EnqueueBatch(List<GenerationQueueItem> batch, string? batchName = null)
    {
        var batchId = Guid.NewGuid();
        var name = batchName ?? batch.FirstOrDefault()?.Summary?[..Math.Min(30, batch.FirstOrDefault()?.Summary?.Length ?? 0)] ?? "";
        foreach (var item in batch) { item.BatchId = batchId; item.BatchName = name; }
        _items.AddRange(batch);
        NotifyAndProcess();
    }

    public void CancelPending(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item?.Status == GenerationQueueStatus.Pending)
        {
            item.Status = GenerationQueueStatus.Cancelled;
            item.CompletedAt = DateTime.Now;
        }
        NotifyState();
    }

    public void RetryFailed(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item?.Status != GenerationQueueStatus.Failed) return;

        item.Status = GenerationQueueStatus.Pending;
        item.ErrorMessage = null;
        item.RetryCount++;
        item.TaskId = null;
        item.ResultUrls.Clear();
        item.VideoUrl = null;
        item.PriceUsd = null;
        item.StartedAt = null;
        item.CompletedAt = null;
        item.ConsecutivePollFailures = 0;
        item.StatusHistory.Clear();
        NotifyAndProcess();
    }

    public void ClearCompleted()
    {
        _items.RemoveAll(i => i.Status is GenerationQueueStatus.Succeeded or GenerationQueueStatus.Cancelled);
        NotifyState();
    }

    public void ClearFailed()
    {
        _items.RemoveAll(i => i.Status == GenerationQueueStatus.Failed);
        NotifyState();
    }

    // ── Paused Batches ──

    private readonly HashSet<Guid> _pausedBatches = new();

    public IReadOnlySet<Guid> PausedBatches => _pausedBatches;

    public void PauseQueue() => IsPaused = true;

    public void ResumeQueue()
    {
        IsPaused = false;
        StartProcessing();
    }

    public void PauseBatch(Guid batchId) { _pausedBatches.Add(batchId); NotifyState(); }

    public void ResumeBatch(Guid batchId) { _pausedBatches.Remove(batchId); NotifyState(); }

    public void RenameBatch(Guid batchId, string name)
    {
        foreach (var item in _items.Where(i => i.BatchId == batchId))
            item.BatchName = name;
        NotifyState();
    }

    public void CancelBatch(Guid batchId)
    {
        foreach (var item in _items.Where(i => i.BatchId == batchId))
        {
            if (item.Status is GenerationQueueStatus.Pending or GenerationQueueStatus.Submitting or GenerationQueueStatus.Polling)
                item.Status = GenerationQueueStatus.Cancelled;
        }
        NotifyState();
    }

    public void ClearBatch(Guid batchId)
    {
        _items.RemoveAll(i => i.BatchId == batchId
            && i.Status is GenerationQueueStatus.Succeeded or GenerationQueueStatus.Failed or GenerationQueueStatus.Cancelled);
        NotifyState();
    }

    public void Restore()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Count == 0) return;

        foreach (var s in snapshot)
        {
            if (!Enum.TryParse<GenerationJobKind>(s.Kind, out var kind)) continue;
            if (!Enum.TryParse<GenerationQueueStatus>(s.Status, out var status)) continue;
            if (status != GenerationQueueStatus.Polling) continue;
            if (string.IsNullOrEmpty(s.TaskId)) continue;

            var item = new GenerationQueueItem
            {
                Id = s.Id,
                Kind = kind,
                Status = status,
                TaskId = s.TaskId,
                ResultUrls = s.ResultUrls,
                VideoUrl = s.VideoUrl,
                ErrorMessage = s.ErrorMessage,
                CreatedAt = s.CreatedAt,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                RetryCount = s.RetryCount,
                ConsecutivePollFailures = s.ConsecutivePollFailures,
                PriceUsd = s.PriceUsd,
                PollDetail = s.PollDetail,
                StatusHistory = s.StatusHistory,
                BatchId = s.BatchId != null && Guid.TryParse(s.BatchId, out var batchId) ? batchId : null,
                BatchName = s.BatchName,
                RestoredFromPersistence = true,
                RestoredSummary = s.SummaryText,
            };
            _items.Add(item);
        }

        if (_items.Count > 0) StartProcessing();
        NotifyState();
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private void SaveSnapshot()
    {
        try
        {
            var active = _items
                .Where(i => i.Status == GenerationQueueStatus.Polling && i.TaskId != null)
                .Select(i => new QueueItemSnapshot
                {
                    Id = i.Id,
                    Kind = i.Kind.ToString(),
                    Status = i.Status.ToString(),
                    TaskId = i.TaskId,
                    ResultUrls = i.ResultUrls,
                    VideoUrl = i.VideoUrl,
                    ErrorMessage = i.ErrorMessage,
                    CreatedAt = i.CreatedAt,
                    StartedAt = i.StartedAt,
                    CompletedAt = i.CompletedAt,
                    RetryCount = i.RetryCount,
                    SummaryText = i.Summary,
                    ConsecutivePollFailures = i.ConsecutivePollFailures,
                    PriceUsd = i.PriceUsd,
                    PollDetail = i.PollDetail,
                    StatusHistory = i.StatusHistory,
                    BatchId = i.BatchId?.ToString(),
                    BatchName = i.BatchName,
                })
                .ToList();

            var json = JsonSerializer.Serialize(active, SnapshotJsonOptions);
            Properties.Settings.Default.QueueSnapshot = json;
            Properties.Settings.Default.Save();
        }
        catch { }
    }

    private static List<QueueItemSnapshot>? LoadSnapshot()
    {
        try
        {
            var json = Properties.Settings.Default.QueueSnapshot;
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<List<QueueItemSnapshot>>(json);
        }
        catch { return null; }
    }

    public void CancelAndClearAll()
    {
        _processCts?.Cancel();
        foreach (var item in _items)
        {
            if (item.Status is GenerationQueueStatus.Pending or GenerationQueueStatus.Submitting or GenerationQueueStatus.Polling)
                item.Status = GenerationQueueStatus.Cancelled;
        }
        _items.Clear();
        NotifyState();
    }

    // ── Private Processing ──

    private void NotifyAndProcess()
    {
        NotifyState();
        StartProcessing();
    }

    private void NotifyState()
    {
        StateChanged?.Invoke();
        SaveSnapshot();
    }

    private void StartProcessing()
    {
        if (_isProcessing) return;
        _processCts?.Cancel();
        _processCts = new CancellationTokenSource();
        _ = ProcessLoop(_processCts.Token);
    }

    private async Task ProcessLoop(CancellationToken ct)
    {
        _isProcessing = true;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!IsPaused)
                {
                    await SubmitPendingItems(ct);
                    await PollActiveItems(ct);
                }

                var allDone = _items.All(i => i.Status is GenerationQueueStatus.Succeeded
                    or GenerationQueueStatus.Failed or GenerationQueueStatus.Cancelled);
                if (allDone && !_items.Any(i => i.Status == GenerationQueueStatus.Pending)) break;

                await Task.Delay(3000, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { _isProcessing = false; }
    }

    private async Task SubmitPendingItems(CancellationToken ct)
    {
        var activeCount = _items.Count(i => i.IsActive);
        var capacity = Math.Max(0, ConcurrencyLimit - activeCount);
        if (capacity <= 0) return;

        var pending = _items.Where(i => i.Status == GenerationQueueStatus.Pending
            && (i.BatchId == null || !_pausedBatches.Contains(i.BatchId.Value))).Take(capacity).ToList();
        foreach (var item in pending) item.Status = GenerationQueueStatus.Submitting;
        NotifyState();

        var tasks = pending.Select(item => SubmitOne(item, ct));
        await Task.WhenAll(tasks);
    }

    private async Task SubmitOne(GenerationQueueItem item, CancellationToken ct)
    {
        try
        {
            if (item.Params == null)
            {
                item.Status = GenerationQueueStatus.Failed;
                item.ErrorMessage = "缺少任务参数";
                NotifyState();
                return;
            }
            var result = await _executor.Submit(item.Params);
            var idx = _items.IndexOf(item);
            if (idx < 0 || item.Status != GenerationQueueStatus.Submitting) return;

            if (result.BananaImageData != null)
            {
                item.BananaResultImageData = result.BananaImageData;
                item.Status = GenerationQueueStatus.Succeeded;
                item.CompletedAt = DateTime.Now;
            }
            else
            {
                item.PriceUsd = result.PriceUsd;
                item.TaskId = result.TaskId;
                item.Status = GenerationQueueStatus.Polling;
                item.StartedAt = DateTime.Now;
                _api.AddTask(item.Id, item.Kind.ToString(), item.Summary?[..Math.Min(30, item.Summary?.Length ?? 0)] ?? "");
            }
        }
        catch (Exception ex)
        {
            item.Status = GenerationQueueStatus.Failed;
            item.ErrorMessage = ex.Message;
        }
        NotifyState();
    }

    private async Task PollActiveItems(CancellationToken ct)
    {
        var polling = _items.Where(i => i.Status == GenerationQueueStatus.Polling && i.TaskId != null).ToList();
        foreach (var item in polling)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var tick = await _executor.Poll(item.TaskId!, item.Kind);
                switch (tick.Result)
                {
                    case PollTickResult.Completed:
                        item.Status = GenerationQueueStatus.Succeeded;
                        item.ResultUrls = tick.ResultUrls ?? new();
                        item.VideoUrl = tick.VideoUrl;
                        item.CompletedAt = DateTime.Now;
                        _api.RemoveTask(item.Id);
                        break;
                    case PollTickResult.Failed:
                        item.Status = GenerationQueueStatus.Failed;
                        item.ErrorMessage = tick.Detail;
                        item.CompletedAt = DateTime.Now;
                        _api.RemoveTask(item.Id);
                        break;
                    default:
                        item.ConsecutivePollFailures = 0;
                        if (tick.Detail != null) item.PollDetail = tick.Detail;
                        break;
                }
            }
            catch (Exception ex)
            {
                item.ConsecutivePollFailures++;
                if (item.ConsecutivePollFailures >= MaxConsecutivePollFailures)
                {
                    item.Status = GenerationQueueStatus.Failed;
                    item.ErrorMessage = $"轮询连续失败 {MaxConsecutivePollFailures} 次: {ex.Message}";
                }
            }
            NotifyState();
        }
    }
}
