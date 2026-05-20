using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Tests;

public class GenerationQueueStoreBatchTests : IDisposable
{
    private readonly ApiService _api;
    private readonly GenerationQueueStore _store;

    public GenerationQueueStoreBatchTests()
    {
        _api = ApiService.Instance;
        _store = new GenerationQueueStore(_api);
        _store.IsPaused = true;
    }

    public void Dispose()
    {
        _store.CancelAndClearAll();
    }

    private static GenerationQueueItem MakeItem(string prompt = "test prompt")
    {
        return new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Params = new GptImageJobParams { Prompt = prompt }
        };
    }

    [Fact]
    public void EnqueueBatch_assigns_batchId()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);

        Assert.All(items, item =>
        {
            Assert.NotNull(item.BatchId);
            Assert.NotEqual(Guid.Empty, item.BatchId!.Value);
        });
    }

    [Fact]
    public void EnqueueBatch_assigns_same_batchId_to_all_items()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);

        var batchId = items[0].BatchId;
        Assert.All(items, item => Assert.Equal(batchId, item.BatchId));
    }

    [Fact]
    public void EnqueueBatch_sets_batchName_from_first_item()
    {
        var items = new List<GenerationQueueItem>
        {
            MakeItem("first item prompt"),
            MakeItem("second item prompt")
        };
        _store.EnqueueBatch(items, batchName: null);

        Assert.Equal("first item prompt", items[0].BatchName);
        Assert.All(items, item => Assert.Equal("first item prompt", item.BatchName));
    }

    [Fact]
    public void EnqueueBatch_uses_explicit_batchName()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items, "My Batch");

        Assert.All(items, item => Assert.Equal("My Batch", item.BatchName));
    }

    [Fact]
    public void EnqueueBatch_adds_items_to_store()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);

        Assert.Equal(3, _store.Items.Count);
    }

    [Fact]
    public void EnqueueBatch_truncates_long_batchName()
    {
        var longPrompt = new string('A', 100);
        var items = new List<GenerationQueueItem> { MakeItem(longPrompt) };
        _store.EnqueueBatch(items);

        Assert.Equal(30, items[0].BatchName!.Length);
    }

    [Fact]
    public void PauseBatch_adds_to_paused_set()
    {
        var items = new List<GenerationQueueItem> { MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.PauseBatch(batchId);

        Assert.Contains(batchId, _store.PausedBatches);
    }

    [Fact]
    public void Paused_batch_items_remain_pending()
    {
        var items = new List<GenerationQueueItem> { MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.PauseBatch(batchId);
        _store.IsPaused = false;

        Assert.Equal(GenerationQueueStatus.Pending, items[0].Status);
    }

    [Fact]
    public void ResumeBatch_removes_from_paused()
    {
        var items = new List<GenerationQueueItem> { MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.PauseBatch(batchId);
        _store.ResumeBatch(batchId);

        Assert.DoesNotContain(batchId, _store.PausedBatches);
    }

    [Fact]
    public void CancelBatch_cancels_pending_items()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.CancelBatch(batchId);

        Assert.All(_store.Items, item => Assert.Equal(GenerationQueueStatus.Cancelled, item.Status));
    }

    [Fact]
    public void CancelBatch_skips_already_succeeded_items()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        items[0].Status = GenerationQueueStatus.Succeeded;
        _store.CancelBatch(batchId);

        Assert.Equal(GenerationQueueStatus.Succeeded, items[0].Status);
        Assert.Equal(GenerationQueueStatus.Cancelled, items[1].Status);
    }

    [Fact]
    public void RenameBatch_updates_all_items_in_batch()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items, "Old Name");
        var batchId = items[0].BatchId!.Value;

        _store.RenameBatch(batchId, "New Name");

        Assert.All(_store.Items, item => Assert.Equal("New Name", item.BatchName));
    }

    [Fact]
    public void RenameBatch_does_not_affect_other_batches()
    {
        var items1 = new List<GenerationQueueItem> { MakeItem() };
        var items2 = new List<GenerationQueueItem> { MakeItem() };
        _store.EnqueueBatch(items1, "Batch A");
        _store.EnqueueBatch(items2, "Batch B");

        _store.RenameBatch(items1[0].BatchId!.Value, "Renamed A");

        Assert.Equal("Renamed A", items1[0].BatchName);
        Assert.Equal("Batch B", items2[0].BatchName);
    }

    [Fact]
    public void ClearBatch_removes_completed_items_only()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        items[0].Status = GenerationQueueStatus.Succeeded;
        items[1].Status = GenerationQueueStatus.Failed;
        _store.ClearBatch(batchId);

        Assert.Single(_store.Items);
        Assert.Equal(GenerationQueueStatus.Pending, _store.Items[0].Status);
    }

    [Fact]
    public void ClearBatch_keeps_active_items()
    {
        var items = new List<GenerationQueueItem> { MakeItem(), MakeItem() };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        items[0].Status = GenerationQueueStatus.Polling;
        items[1].Status = GenerationQueueStatus.Succeeded;
        _store.ClearBatch(batchId);

        Assert.Single(_store.Items);
        Assert.Equal(GenerationQueueStatus.Polling, _store.Items[0].Status);
    }

    [Fact]
    public void StatsSummary_reflects_batch_state()
    {
        _store.Enqueue(MakeItem());
        _store.Items[0].Status = GenerationQueueStatus.Succeeded;
        _store.Enqueue(MakeItem());
        _store.Items[1].Status = GenerationQueueStatus.Failed;
        _store.Enqueue(MakeItem());

        var summary = _store.StatsSummary;

        Assert.Contains("待提交 1", summary);
        Assert.Contains("完成 1", summary);
        Assert.Contains("失败 1", summary);
    }
}
