using System.Text.Json.Serialization;

namespace AIZhijian.Models;

public class LoginResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class CheckResponse
{
    [JsonPropertyName("authenticated")] public bool Authenticated { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("userId")] public int UserId { get; set; }
    [JsonPropertyName("contentAuditPermission")] public bool ContentAuditPermission { get; set; }
}

public class TaskSubmitResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("ourTaskId")] public string? OurTaskId { get; set; }
    [JsonPropertyName("rhTaskId")] public string? RhTaskId { get; set; }
    [JsonPropertyName("tasks")] public List<TaskInfo>? Tasks { get; set; }
    [JsonPropertyName("taskId")] public string? TaskId { get; set; }
    [JsonPropertyName("priceUsd")] public string? PriceUsd { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class TaskInfo
{
    [JsonPropertyName("ourTaskId")] public string OurTaskId { get; set; } = "";
    [JsonPropertyName("rhTaskId")] public string? RhTaskId { get; set; }
}

public class TaskPollResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("dbStatus")] public string? DbStatus { get; set; }
    [JsonPropertyName("rhStatus")] public string? RhStatus { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("taskStatus")] public string? TaskStatus { get; set; }
    [JsonPropertyName("resultUrls")] public List<string>? ResultUrls { get; set; }
    [JsonPropertyName("videoUrl")] public string? VideoUrl { get; set; }
    [JsonPropertyName("outputUrl")] public string? OutputUrl { get; set; }
    [JsonPropertyName("resultData")] public string? ResultData { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("detailMessage")] public string? DetailMessage { get; set; }
    [JsonPropertyName("ourTaskId")] public string? OurTaskId { get; set; }
    [JsonPropertyName("rhTaskId")] public string? RhTaskId { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class ApiKeyInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class SeedanceVirtualAssetConfigResponse
{
    [JsonPropertyName("assetApiConfigured")] public bool? AssetApiConfigured { get; set; }
    [JsonPropertyName("assetAccessKeyPresent")] public bool? AssetAccessKeyPresent { get; set; }
    [JsonPropertyName("assetSecretKeyPresent")] public bool? AssetSecretKeyPresent { get; set; }
    [JsonPropertyName("cosConfigured")] public bool? CosConfigured { get; set; }
}

public class SeedanceVirtualAssetGroup
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("arkGroupId")] public string? ArkGroupId { get; set; }
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class SeedanceVirtualAssetItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("arkAssetId")] public string? ArkAssetId { get; set; }
    [JsonPropertyName("assetUri")] public string? AssetUri { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("sourcePublicUrl")] public string? SourcePublicUrl { get; set; }
    [JsonPropertyName("lastStatus")] public string? LastStatus { get; set; }
    public bool IsActive => (LastStatus ?? "").ToLower() == "active";
}

public class SeedanceVirtualAssetGroupListResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("items")] public List<SeedanceVirtualAssetGroup>? Items { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class SeedanceVirtualAssetItemListResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("items")] public List<SeedanceVirtualAssetItem>? Items { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class SeedanceVirtualAssetMutationResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("item")] public SeedanceVirtualAssetItem? Item { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class EmptyResponse { }
