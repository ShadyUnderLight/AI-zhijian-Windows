using System.Text.Json;
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
    public void RetryFailed_rejects_restoredFromPersistence()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            RestoredFromPersistence = true
        };
        _store.Enqueue(item);

        _store.RetryFailed(item.Id);

        Assert.Equal(GenerationQueueStatus.Failed, item.Status);
        Assert.NotNull(item.ErrorMessage);
        Assert.Contains("持久化", item.ErrorMessage);
    }

    [Fact]
    public void RetryFailed_rejects_empty_prompt()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            Params = new GptImageJobParams { Prompt = "" }
        };
        _store.Enqueue(item);

        _store.RetryFailed(item.Id);

        Assert.Equal(GenerationQueueStatus.Failed, item.Status);
        Assert.NotNull(item.ErrorMessage);
        Assert.Contains("提示词", item.ErrorMessage);
    }

    [Fact]
    public void RetryFailed_increments_retryCount_on_success()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            Params = new GptImageJobParams { Prompt = "valid prompt" }
        };
        _store.Enqueue(item);

        _store.RetryFailed(item.Id);

        Assert.Equal(GenerationQueueStatus.Pending, item.Status);
        Assert.Equal(1, item.RetryCount);
        Assert.Null(item.ErrorMessage);
        Assert.Null(item.TaskId);
    }

    [Fact]
    public void RetryBatch_retries_all_failed_items()
    {
        var items = new List<GenerationQueueItem>
        {
            new() { Kind = GenerationJobKind.GptImage, Status = GenerationQueueStatus.Failed, Params = new GptImageJobParams { Prompt = "a" } },
            new() { Kind = GenerationJobKind.GptImage, Status = GenerationQueueStatus.Failed, Params = new GptImageJobParams { Prompt = "b" } },
            new() { Kind = GenerationJobKind.GptImage, Status = GenerationQueueStatus.Succeeded, Params = new GptImageJobParams { Prompt = "c" } },
        };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.RetryBatch(batchId);

        Assert.Equal(GenerationQueueStatus.Pending, items[0].Status);
        Assert.Equal(GenerationQueueStatus.Pending, items[1].Status);
        Assert.Equal(GenerationQueueStatus.Succeeded, items[2].Status);
    }

    [Fact]
    public void RetryBatch_skips_invalid_items()
    {
        var items = new List<GenerationQueueItem>
        {
            new() { Kind = GenerationJobKind.GptImage, Status = GenerationQueueStatus.Failed, Params = new GptImageJobParams { Prompt = "valid" } },
            new() { Kind = GenerationJobKind.GptImage, Status = GenerationQueueStatus.Failed, RestoredFromPersistence = true },
        };
        _store.EnqueueBatch(items);
        var batchId = items[0].BatchId!.Value;

        _store.RetryBatch(batchId);

        Assert.Equal(GenerationQueueStatus.Pending, items[0].Status);
        Assert.Equal(GenerationQueueStatus.Failed, items[1].Status);
    }

    [Fact]
    public void ShowRetry_false_when_validation_fails()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            RestoredFromPersistence = true
        };

        Assert.False(item.ShowRetry);
    }

    [Fact]
    public void ShowRetry_true_when_all_valid()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            Params = new GptImageJobParams { Prompt = "valid prompt" }
        };

        Assert.True(item.ShowRetry);
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

    [Fact]
    public void RetryValidation_Veo_rejects_invalid_channel_model()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Status = GenerationQueueStatus.Failed,
            Params = new VeoJobParams { Prompt = "valid", Channel = "invalid", Model = "fast" }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("渠道", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Veo_rejects_empty_ImageData()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Status = GenerationQueueStatus.Failed,
            Params = new VeoJobParams { Prompt = "valid", ImageData = Array.Empty<byte>() }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("图片", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Veo_rejects_empty_ImageFile_entry()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Status = GenerationQueueStatus.Failed,
            Params = new VeoJobParams
            {
                Prompt = "valid",
                ImageFiles = { new FileRef { Data = Array.Empty<byte>(), Name = "empty.jpg", Mime = "image/jpeg" } }
            }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("参考图", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Veo_rejects_empty_FirstImageData()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Status = GenerationQueueStatus.Failed,
            Params = new VeoJobParams { Prompt = "valid", FirstImageData = Array.Empty<byte>() }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("首帧", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Veo_rejects_Ref1Data_with_null_Data()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Status = GenerationQueueStatus.Failed,
            Params = new VeoJobParams
            {
                Prompt = "valid",
                Ref1Data = (null!, "", "")
            }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("参考图1", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Grok_rejects_empty_ImageFile_entry()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Grok,
            Status = GenerationQueueStatus.Failed,
            Params = new GrokJobParams
            {
                Prompt = "valid",
                ImageFiles = { (Array.Empty<byte>(), "img.jpg", "image/jpeg") }
            }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("参考图", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Grok_rejects_empty_VideoData()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Grok,
            Status = GenerationQueueStatus.Failed,
            Params = new GrokJobParams { Prompt = "valid", VideoData = Array.Empty<byte>() }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("视频", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Wan_rejects_non_image_mode_without_frames()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Wan,
            Status = GenerationQueueStatus.Failed,
            Params = new WanJobParams { Prompt = "valid", Mode = "first_last" }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("首帧", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Wan_passes_mode_image_with_ImageData()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Wan,
            Status = GenerationQueueStatus.Failed,
            Params = new WanJobParams { Prompt = "valid", Mode = "image", ImageData = new byte[] { 1, 2, 3 } }
        };

        Assert.Null(item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_Banana_rejects_empty_ReferenceImage()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Banana,
            Status = GenerationQueueStatus.Failed,
            Params = new BananaJobParams
            {
                Prompt = "valid",
                ReferenceImages = { new FileRef { Data = Array.Empty<byte>() } }
            }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("参考图", item.RetryValidationError);
    }

    [Fact]
    public void RetryValidation_GptImage_rejects_FileRef_with_null_Data()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Status = GenerationQueueStatus.Failed,
            Params = new GptImageJobParams
            {
                Prompt = "valid",
                ReferenceImages = { new FileRef { Data = null! } }
            }
        };

        Assert.NotNull(item.RetryValidationError);
        Assert.Contains("参考图", item.RetryValidationError);
    }

    [Fact]
    public void HasFileData_GptImage_with_references_returns_true()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Params = new GptImageJobParams
            {
                Prompt = "test",
                ReferenceImages = { new FileRef { Data = new byte[] { 1, 2, 3 }, Name = "ref.jpg", Mime = "image/jpeg" } }
            }
        };

        Assert.True(item.HasFileData);
    }

    [Fact]
    public void HasFileData_GptImage_without_references_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Params = new GptImageJobParams { Prompt = "test" }
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Seedance_with_assets_returns_true()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Seedance,
            Params = new SeedanceJobParams
            {
                Prompt = "test",
                Assets = { new SeedanceAsset { Type = "image", Name = "asset.png", Mime = "image/png", Data = new byte[] { 1 } } }
            }
        };

        Assert.True(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Seedance_without_assets_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Seedance,
            Params = new SeedanceJobParams { Prompt = "test" }
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Wan_with_ImageData_returns_true()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Wan,
            Params = new WanJobParams { Prompt = "test", ImageData = new byte[] { 1, 2, 3 } }
        };

        Assert.True(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Wan_without_binary_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Wan,
            Params = new WanJobParams { Prompt = "test" }
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void HasFileData_null_Params_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.GptImage,
            Params = null
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Veo_with_ImageFiles_returns_true()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Params = new VeoJobParams
            {
                Prompt = "test",
                ImageFiles = { new FileRef { Data = new byte[] { 1 }, Name = "img.jpg", Mime = "image/jpeg" } }
            }
        };

        Assert.True(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Veo_without_binary_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Veo,
            Params = new VeoJobParams { Prompt = "test" }
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Grok_with_VideoData_returns_true()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Grok,
            Params = new GrokJobParams { Prompt = "test", VideoData = new byte[] { 1, 2 } }
        };

        Assert.True(item.HasFileData);
    }

    [Fact]
    public void HasFileData_Grok_without_binary_returns_false()
    {
        var item = new GenerationQueueItem
        {
            Kind = GenerationJobKind.Grok,
            Params = new GrokJobParams { Prompt = "test" }
        };

        Assert.False(item.HasFileData);
    }

    [Fact]
    public void Restore_skips_snapshot_items_with_HasFileData()
    {
        var snapshot = new List<QueueItemSnapshot>
        {
            new() { Id = "skip-id", Kind = "GptImage", Status = "Polling", TaskId = "task1", SummaryText = "skip", HasFileData = true },
            new() { Id = "keep-id", Kind = "GptImage", Status = "Polling", TaskId = "task2", SummaryText = "keep" },
        };
        var json = JsonSerializer.Serialize(snapshot);
        Properties.Settings.Default.QueueSnapshot = json;
        Properties.Settings.Default.Save();

        var store = new GenerationQueueStore(_api);
        store.Restore();

        Assert.Single(store.Items);
        Assert.Equal("keep-id", store.Items[0].Id);
    }
}
