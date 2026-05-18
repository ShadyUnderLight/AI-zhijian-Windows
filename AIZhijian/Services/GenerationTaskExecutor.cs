using System.Text.Json;
using AIZhijian.Models;

namespace AIZhijian.Services;

public class GenerationSubmitResult
{
    public string TaskId { get; set; } = "";
    public string? PriceUsd { get; set; }
    public List<string> ExtraTaskIds { get; set; } = new();
    public byte[]? BananaImageData { get; set; }
}

public enum PollTickResult { StillProcessing, Completed, Failed }

public class PollTick
{
    public PollTickResult Result { get; set; }
    public string? Detail { get; set; }
    public List<string>? ResultUrls { get; set; }
    public string? VideoUrl { get; set; }
}

public class GenerationTaskExecutor
{
    private readonly ApiService _api;
    public GenerationTaskExecutor(ApiService api) => _api = api;

    public async Task<GenerationSubmitResult> Submit(JobParams p)
    {
        switch (p)
        {
            case GptImageJobParams gp:
            {
                TaskSubmitResponse result;
                if (gp.IsImageToImage)
                    result = await _api.GenerateImageToImage(gp.Prompt, gp.Channel, gp.AspectRatio,
                        gp.Resolution, gp.Quality, gp.ReferenceImages);
                else
                    result = await _api.GenerateImage(gp.Prompt, gp.Channel, gp.AspectRatio,
                        gp.Resolution, gp.Quality, gp.PhotoReal);

                if (string.IsNullOrEmpty(result.OurTaskId))
                    throw new ApiException(result.Message ?? "未能获取任务ID");
                return new() { TaskId = result.OurTaskId, PriceUsd = result.PriceUsd };
            }

            case BananaJobParams bp:
            {
                var data = await _api.GenerateBanana(bp.Prompt, bp.Provider, bp.ReferenceImages);
                return new() { BananaImageData = data };
            }

            case SeedanceJobParams sp:
            {
                var result = await _api.GenerateSeedanceVideo(sp.Prompt, sp.Mode, sp.Model,
                    sp.Ratio, sp.Resolution, sp.Duration, sp.Count, sp.GenerateAudio, sp.Assets);

                if (result.Tasks is { Count: > 0 })
                {
                    var first = result.Tasks[0];
                    var extras = result.Tasks.Skip(1).Select(t => t.OurTaskId).ToList();
                    return new() { TaskId = first.OurTaskId, PriceUsd = result.PriceUsd, ExtraTaskIds = extras };
                }
                if (!string.IsNullOrEmpty(result.OurTaskId))
                    return new() { TaskId = result.OurTaskId, PriceUsd = result.PriceUsd };

                throw new ApiException(result.Message ?? "未能获取任务ID");
            }

            case WanJobParams wp:
            {
                TaskSubmitResponse result;
                if (wp.Mode == "image")
                {
                    if (wp.ImageData == null) throw new ApiException("请先选择输入图片");
                    result = await _api.GenerateWanVideo(wp.ImageData, wp.ImageName!, wp.ImageMime!,
                        wp.Prompt, wp.Width, wp.Height, wp.Seconds);
                }
                else
                {
                    if (wp.FirstFrame == null || wp.LastFrame == null) throw new ApiException("请先选择首帧和尾帧图片");
                    result = await _api.GenerateWanFirstLastVideo(wp.FirstFrame, wp.LastFrame,
                        wp.Prompt, wp.Seconds, wp.Enable48G);
                }

                if (string.IsNullOrEmpty(result.TaskId)) throw new ApiException(result.Message ?? "未能获取任务ID");
                return new() { TaskId = result.TaskId, PriceUsd = result.PriceUsd };
            }

            case VeoJobParams vp:
            {
                var result = await _api.GenerateVeoVideo(vp);
                if (string.IsNullOrEmpty(result.OurTaskId)) throw new ApiException(result.Message ?? "未能获取任务ID");
                return new() { TaskId = result.OurTaskId, PriceUsd = result.PriceUsd };
            }

            case GrokJobParams gkp:
            {
                var result = await _api.GenerateGrokVideo(gkp.Prompt, gkp.Channel, gkp.Mode,
                    gkp.AspectRatio, gkp.Resolution, gkp.Duration, gkp.ImageFiles,
                    gkp.VideoData, gkp.VideoName, gkp.VideoMime);
                if (string.IsNullOrEmpty(result.TaskId)) throw new ApiException(result.Message ?? "未能获取任务ID");
                return new() { TaskId = result.TaskId, PriceUsd = result.PriceUsd };
            }

            default: throw new ApiException("未知任务类型");
        }
    }

    public async Task<PollTick> Poll(string taskId, GenerationJobKind kind)
    {
        try
        {
            var resp = kind switch
            {
                GenerationJobKind.GptImage => await _api.PollImageTask(taskId),
                GenerationJobKind.Seedance => await _api.PollSeedanceTask(taskId),
                GenerationJobKind.Wan => await _api.PollMediaTask(taskId),
                GenerationJobKind.Veo => await _api.PollVeoTask(taskId),
                GenerationJobKind.Grok => await _api.PollGrokTask(taskId),
                _ => throw new ApiException("Banana 任务无需轮询")
            };

            return InterpretResponse(kind, resp);
        }
        catch (ApiException) { throw; }
        catch (Exception ex) { throw new ApiException(ex.Message); }
    }

    private static PollTick InterpretResponse(GenerationJobKind kind, TaskPollResponse r)
    {
        if (r.DbStatus?.ToUpper() == "SUCCESS" || r.Status?.ToUpper() == "SUCCESS" || r.TaskStatus?.ToUpper() == "SUCCESS")
        {
            if (kind == GenerationJobKind.GptImage)
                return new() { Result = PollTickResult.Completed, ResultUrls = r.ResultUrls ?? new() };
            var url = r.VideoUrl ?? r.OutputUrl;
            return new() { Result = PollTickResult.Completed, VideoUrl = url };
        }

        var dbStatus = r.DbStatus?.ToUpper() ?? "";
        if (dbStatus is "FAILED" or "CANCELLED" or "ERROR")
            return new() { Result = PollTickResult.Failed, Detail = r.ErrorMessage ?? r.Message ?? "任务失败" };

        var detail = MapIntermediateStatus(r);
        if (detail != null)
            return new() { Result = PollTickResult.StillProcessing, Detail = detail };

        return new() { Result = PollTickResult.StillProcessing };
    }

    private static string? MapIntermediateStatus(TaskPollResponse r)
    {
        foreach (var raw in new[] { r.RhStatus, r.DbStatus, r.Status, r.TaskStatus })
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var key = raw.ToUpper().Replace(" ", "_").Replace("-", "_");
            var detail = key switch
            {
                "QUEUED" or "PENDING" or "WAITING" or "IN_QUEUE" => "供应商排队中",
                "PROCESSING" or "RUNNING" or "IN_PROGRESS" or "GENERATING" or "RENDERING" => "供应商生成中",
                "UPLOADING" or "SAVING" or "STORING" => "结果上传中",
                "DOWNLOADING" or "FETCHING" or "RETRIEVING" => "取回结果中",
                "POST_PROCESSING" or "POSTPROCESSING" or "FINALIZING" => "后处理中",
                "SUBMITTED" or "ACCEPTED" or "STARTED" => "已受理",
                _ => null
            };
            if (detail != null) return detail;
        }
        return null;
    }
}
