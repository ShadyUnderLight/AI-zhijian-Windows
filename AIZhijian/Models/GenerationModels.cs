using System.Text.Json.Serialization;
using AIZhijian.Services;

namespace AIZhijian.Models;

public enum GenerationJobKind
{
    GptImage, Banana, Seedance, Wan, Veo, Grok
}

public enum GenerationQueueStatus
{
    Pending, Submitting, Polling, Succeeded, Failed, Cancelled
}

public class FileRef
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string Name { get; set; } = "";
    public string Mime { get; set; } = "";
}

public abstract class JobParams { }

public class GptImageJobParams : JobParams
{
    public string Prompt { get; set; } = "";
    public string Channel { get; set; } = "official";
    public string AspectRatio { get; set; } = "1:1";
    public string Resolution { get; set; } = "2k";
    public string Quality { get; set; } = "medium";
    public bool PhotoReal { get; set; }
    public List<FileRef> ReferenceImages { get; set; } = new();
    public bool IsImageToImage => ReferenceImages.Count > 0;
}

public class BananaJobParams : JobParams
{
    public string Prompt { get; set; } = "";
    public string Provider { get; set; } = "third_party";
    public List<FileRef> ReferenceImages { get; set; } = new();
}

public class SeedanceJobParams : JobParams
{
    public string Prompt { get; set; } = "";
    public string Mode { get; set; } = "reference";
    public string Model { get; set; } = "dreamina-seedance-2-0-260128";
    public string Ratio { get; set; } = "adaptive";
    public string Resolution { get; set; } = "720p";
    public int Duration { get; set; } = 5;
    public int Count { get; set; } = 1;
    public bool GenerateAudio { get; set; } = true;
    public List<SeedanceAsset> Assets { get; set; } = new();
}

public class SeedanceAsset
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Size { get; set; }
    public double Duration { get; set; }
    public byte[]? Data { get; set; }
    public string? DataUrl { get; set; }

    public string GetDataUrl()
    {
        if (!string.IsNullOrEmpty(DataUrl)) return DataUrl!;
        if (Data == null) throw new InvalidOperationException("素材数据为空");
        var b64 = Convert.ToBase64String(Data);
        return $"data:{Mime};base64,{b64}";
    }
}

public class WanJobParams : JobParams
{
    public string Mode { get; set; } = "image";
    public string Prompt { get; set; } = "";
    public int Width { get; set; } = 720;
    public int Height { get; set; } = 1280;
    public int Seconds { get; set; } = 5;
    public bool Enable48G { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageName { get; set; }
    public string? ImageMime { get; set; }
    public FileRef? FirstFrame { get; set; }
    public FileRef? LastFrame { get; set; }
}

public class VeoJobParams : JobParams
{
    public string Channel { get; set; } = "budget";
    public string Model { get; set; } = "fast";
    public string Mode { get; set; } = "text";
    public string Prompt { get; set; } = "";
    public string AspectRatio { get; set; } = "9:16";
    public string Resolution { get; set; } = "720p";
    public string Duration { get; set; } = "8";
    public bool GenerateAudio { get; set; }
    public string? NegativePrompt { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageName { get; set; }
    public string? ImageMime { get; set; }
    public List<FileRef> ImageFiles { get; set; } = new();
    public byte[]? FirstImageData { get; set; }
    public string? FirstImageName { get; set; }
    public string? FirstImageMime { get; set; }
    public byte[]? LastImageData { get; set; }
    public string? LastImageName { get; set; }
    public string? LastImageMime { get; set; }
    public (byte[] Data, string Name, string Mime)? Ref1Data { get; set; }
    public (byte[] Data, string Name, string Mime)? Ref2Data { get; set; }
    public (byte[] Data, string Name, string Mime)? Ref3Data { get; set; }
    public byte[]? VideoData { get; set; }
    public string? VideoName { get; set; }
    public string? VideoMime { get; set; }
}

public class GrokJobParams : JobParams
{
    public string Prompt { get; set; } = "";
    public string Channel { get; set; } = "budget";
    public string Mode { get; set; } = "text";
    public string AspectRatio { get; set; } = "9:16";
    public string Resolution { get; set; } = "720p";
    public string Duration { get; set; } = "6";
    public List<(byte[] Data, string Name, string Mime)> ImageFiles { get; set; } = new();
    public byte[]? VideoData { get; set; }
    public string? VideoName { get; set; }
    public string? VideoMime { get; set; }
}

public class StatusEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class GenerationQueueItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public GenerationJobKind Kind { get; set; }
    public GenerationQueueStatus Status { get; set; } = GenerationQueueStatus.Pending;
    public string? TaskId { get; set; }
    public List<string> ResultUrls { get; set; } = new();
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchName { get; set; }
    public JobParams? Params { get; set; }
    public byte[]? BananaResultImageData { get; set; }
    public int ConsecutivePollFailures { get; set; }
    public string? LastPollError { get; set; }
    public string? PriceUsd { get; set; }
    public bool RestoredFromPersistence { get; set; }
    public string? PollDetail { get; set; }
    public List<StatusEvent> StatusHistory { get; set; } = new();
    public bool IsActive => Status == GenerationQueueStatus.Submitting || Status == GenerationQueueStatus.Polling;
    public string? RetryValidationError
{
    get
    {
        if (RestoredFromPersistence)
            return "从持久化恢复的任务无法重试";
        if (Params == null)
            return "参数已丢失，无法重试";
        return Params switch
        {
            GptImageJobParams p => ValidateGptImage(p),
            BananaJobParams p => ValidateBanana(p),
            SeedanceJobParams p => ValidateSeedance(p),
            WanJobParams p => ValidateWan(p),
            VeoJobParams p => ValidateVeo(p),
            GrokJobParams p => ValidateGrok(p),
            _ => null
        };
    }
}

public bool ShowRetry => Status == GenerationQueueStatus.Failed && RetryValidationError == null;

private static bool IsEmptyData(byte[]? data) => data == null || data.Length == 0;

private static string? ValidateGptImage(GptImageJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt) && p.ReferenceImages.Count == 0)
        return "提示词为空";
    if (p.ReferenceImages.Any(r => IsEmptyData(r.Data)))
        return "参考图数据无效";
    return null;
}

private static string? ValidateBanana(BananaJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt) && p.ReferenceImages.Count == 0)
        return "提示词为空";
    if (p.ReferenceImages.Any(r => IsEmptyData(r.Data)))
        return "参考图数据无效";
    return null;
}

private static string? ValidateSeedance(SeedanceJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt))
        return "提示词为空";
    if (p.Assets.Any(a => a.Data == null && string.IsNullOrEmpty(a.DataUrl)))
        return "素材数据无效";
    return null;
}

private static string? ValidateWan(WanJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt))
        return "提示词为空";
    if (p.Mode == "image" && IsEmptyData(p.ImageData))
        return "参考图片数据无效";
    if (p.Mode != "image" && (p.FirstFrame == null || p.LastFrame == null))
        return "非图片模式需要首帧和尾帧数据";
    if (p.FirstFrame != null && IsEmptyData(p.FirstFrame.Data))
        return "首帧数据无效";
    if (p.LastFrame != null && IsEmptyData(p.LastFrame.Data))
        return "尾帧数据无效";
    return null;
}

private static string? ValidateVeo(VeoJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt))
        return "提示词为空";
    if (!VeoRules.IsValidCombination(p.Channel, p.Model))
        return "渠道/模型组合无效";
    if (p.ImageData != null && IsEmptyData(p.ImageData))
        return "图片数据无效";
    if (p.ImageFiles.Any(f => IsEmptyData(f.Data)))
        return "参考图数据无效";
    if (p.FirstImageData != null && IsEmptyData(p.FirstImageData))
        return "首帧数据无效";
    if (p.LastImageData != null && IsEmptyData(p.LastImageData))
        return "尾帧数据无效";
    if (p.VideoData != null && IsEmptyData(p.VideoData))
        return "视频数据无效";
    if (p.Ref1Data.HasValue && IsEmptyData(p.Ref1Data.Value.Data))
        return "参考图1数据无效";
    if (p.Ref2Data.HasValue && IsEmptyData(p.Ref2Data.Value.Data))
        return "参考图2数据无效";
    if (p.Ref3Data.HasValue && IsEmptyData(p.Ref3Data.Value.Data))
        return "参考图3数据无效";
    return null;
}

private static string? ValidateGrok(GrokJobParams p)
{
    if (string.IsNullOrWhiteSpace(p.Prompt))
        return "提示词为空";
    if (p.ImageFiles.Any(f => IsEmptyData(f.Data)))
        return "参考图数据无效";
    if (p.VideoData != null && IsEmptyData(p.VideoData))
        return "视频数据无效";
    return null;
}
    public string Elapsed => $"{(DateTime.Now - (StartedAt ?? CreatedAt)).TotalSeconds:F0}s";
    public string? RestoredSummary { get; set; }
    public bool HasFileData => Params switch
    {
        GptImageJobParams p => p.ReferenceImages.Count > 0,
        BananaJobParams p => p.ReferenceImages.Count > 0,
        SeedanceJobParams p => p.Assets.Count > 0,
        WanJobParams p => p.ImageData != null || p.FirstFrame != null || p.LastFrame != null,
        VeoJobParams p => p.ImageData != null || p.ImageFiles.Count > 0
            || p.FirstImageData != null || p.LastImageData != null
            || p.VideoData != null || p.Ref1Data != null || p.Ref2Data != null || p.Ref3Data != null,
        GrokJobParams p => p.ImageFiles.Count > 0 || p.VideoData != null,
        _ => false,
    };
    public string Summary
    {
        get
        {
            if (Params != null)
            {
                return Params switch
                {
                    GptImageJobParams p => p.Prompt,
                    BananaJobParams p => p.Prompt,
                    SeedanceJobParams p => p.Prompt,
                    WanJobParams p => p.Prompt,
                    VeoJobParams p => p.Prompt,
                    GrokJobParams p => p.Prompt,
                    _ => ""
                };
            }
            return RestoredSummary ?? "";
        }
    }
}

public class QueueItemSnapshot
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("taskId")] public string? TaskId { get; set; }
    [JsonPropertyName("resultUrls")] public List<string> ResultUrls { get; set; } = new();
    [JsonPropertyName("videoUrl")] public string? VideoUrl { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("startedAt")] public DateTime? StartedAt { get; set; }
    [JsonPropertyName("completedAt")] public DateTime? CompletedAt { get; set; }
    [JsonPropertyName("retryCount")] public int RetryCount { get; set; }
    [JsonPropertyName("summaryText")] public string SummaryText { get; set; } = "";
    [JsonPropertyName("consecutivePollFailures")] public int ConsecutivePollFailures { get; set; }
    [JsonPropertyName("hasFileData")] public bool HasFileData { get; set; }
    [JsonPropertyName("priceUsd")] public string? PriceUsd { get; set; }
    [JsonPropertyName("pollDetail")] public string? PollDetail { get; set; }
    [JsonPropertyName("statusHistory")] public List<StatusEvent> StatusHistory { get; set; } = new();
    [JsonPropertyName("batchId")] public string? BatchId { get; set; }
    [JsonPropertyName("batchName")] public string? BatchName { get; set; }
}

public class GenerationSubmitResult
{
    public string TaskId { get; set; } = "";
    public string? PriceUsd { get; set; }
    public List<string> ExtraTaskIds { get; set; } = new();
    public byte[]? BananaImageData { get; set; }
    public bool IsBananaComplete => BananaImageData != null;
}

public enum GenerationPollTickType { StillProcessing, ProcessingDetail, Completed, Failed }

public class GenerationPollTick
{
    public GenerationPollTickType Type { get; set; }
    public string? Detail { get; set; }
    public List<string>? ResultUrls { get; set; }
    public string? VideoUrl { get; set; }
}

public class WorkRecordMetadata
{
    public string Model { get; set; } = "";
    public string Channel { get; set; } = "";
    public string AspectRatio { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string Duration { get; set; } = "";
}

public class WorkRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public GenerationJobKind Kind { get; set; }
    public string Prompt { get; set; } = "";
    public WorkRecordMetadata Metadata { get; set; } = new();
    public List<string> ResultUrls { get; set; } = new();
    public string? VideoUrl { get; set; }
    public string? LocalImagePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsVideo => Kind is not (GenerationJobKind.GptImage or GenerationJobKind.Banana);
    public bool IsSuccess => ErrorMessage == null && (ResultUrls.Count > 0 || VideoUrl != null || LocalImagePath != null);
}
