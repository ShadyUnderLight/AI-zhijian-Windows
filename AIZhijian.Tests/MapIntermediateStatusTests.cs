using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Tests;

public class MapIntermediateStatusTests
{
    [Fact]
    public void Queued_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "QUEUED" };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Pending_via_Status()
    {
        var r = new TaskPollResponse { Status = "PENDING" };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Waiting_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "WAITING" };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void InQueue_via_RhStatus()
    {
        var r = new TaskPollResponse { RhStatus = "IN_QUEUE" };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Processing_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "PROCESSING" };
        Assert.Equal("供应商生成中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Running_via_Status()
    {
        var r = new TaskPollResponse { Status = "RUNNING" };
        Assert.Equal("供应商生成中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void InProgress_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "IN_PROGRESS" };
        Assert.Equal("供应商生成中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Generating_via_RhStatus()
    {
        var r = new TaskPollResponse { RhStatus = "GENERATING" };
        Assert.Equal("供应商生成中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Rendering_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "RENDERING" };
        Assert.Equal("供应商生成中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Uploading_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "UPLOADING" };
        Assert.Equal("结果上传中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Saving_via_Status()
    {
        var r = new TaskPollResponse { Status = "SAVING" };
        Assert.Equal("结果上传中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Storing_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "STORING" };
        Assert.Equal("结果上传中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Downloading_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "DOWNLOADING" };
        Assert.Equal("取回结果中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Fetching_via_Status()
    {
        var r = new TaskPollResponse { Status = "FETCHING" };
        Assert.Equal("取回结果中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Retrieving_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "RETRIEVING" };
        Assert.Equal("取回结果中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void PostProcessing_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "POST_PROCESSING" };
        Assert.Equal("后处理中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Postprocessing_via_Status()
    {
        var r = new TaskPollResponse { Status = "POSTPROCESSING" };
        Assert.Equal("后处理中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Finalizing_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "FINALIZING" };
        Assert.Equal("后处理中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Submitted_via_DbStatus()
    {
        var r = new TaskPollResponse { DbStatus = "SUBMITTED" };
        Assert.Equal("已受理", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Accepted_via_Status()
    {
        var r = new TaskPollResponse { Status = "ACCEPTED" };
        Assert.Equal("已受理", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Started_via_TaskStatus()
    {
        var r = new TaskPollResponse { TaskStatus = "STARTED" };
        Assert.Equal("已受理", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Unknown_status_returns_null()
    {
        var r = new TaskPollResponse { Status = "SOME_UNKNOWN_STATUS" };
        Assert.Null(GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void All_fields_null_returns_null()
    {
        var r = new TaskPollResponse();
        Assert.Null(GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Priority_follows_RhStatus_first()
    {
        var r = new TaskPollResponse
        {
            RhStatus = "QUEUED",
            DbStatus = "PROCESSING",
            Status = "RENDERING"
        };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Normalizes_dashes_and_spaces()
    {
        var r = new TaskPollResponse { Status = "post-processing" };
        Assert.Equal("后处理中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }

    [Fact]
    public void Case_insensitive()
    {
        var r = new TaskPollResponse { DbStatus = "queued" };
        Assert.Equal("供应商排队中", GenerationTaskExecutor.MapIntermediateStatus(r));
    }
}
