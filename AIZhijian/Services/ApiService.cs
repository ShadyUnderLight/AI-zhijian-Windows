using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIZhijian.Models;

namespace AIZhijian.Services;

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}

public static class AppConfig
{
    private const string CustomUrlKey = "api_base_url_override";
    private const string DefaultUrlString = "http://43.139.67.8:7777";

    public static Uri ApiBaseUrl
    {
        get
        {
            var custom = AIZhijian.Properties.Settings.Default.ApiBaseUrlOverride;
            if (!string.IsNullOrEmpty(custom) && Uri.TryCreate(custom, UriKind.Absolute, out var uri))
                return uri;
            return new Uri(DefaultUrlString);
        }
    }

    public static void SetCustomBaseUrl(string url)
    {
        AIZhijian.Properties.Settings.Default.ApiBaseUrlOverride = url;
        AIZhijian.Properties.Settings.Default.Save();
    }
}

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private static ApiService? _instance;
    public static ApiService Instance => _instance ??= new ApiService();

    private GenerationQueueStore? _queueStore;
    public GenerationQueueStore GetQueue()
    {
        if (_queueStore == null)
        {
            _queueStore = new GenerationQueueStore(this);
            _queueStore.Restore();
        }
        return _queueStore;
    }

    public bool IsLoggedIn { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public int UserId { get; set; }
    public bool IsCheckingSession { get; set; } = true;
    public bool IsLoggingIn { get; set; }
    public string? LoginError { get; set; }
    public bool RememberLogin
    {
        get => AIZhijian.Properties.Settings.Default.RememberLogin;
        set { AIZhijian.Properties.Settings.Default.RememberLogin = value; AIZhijian.Properties.Settings.Default.Save(); }
    }

    public List<ActiveTask> ActiveTasks { get; set; } = new();
    public string ServerDisplayOrigin => $"{AppConfig.ApiBaseUrl.Scheme}://{AppConfig.ApiBaseUrl.Host}:{AppConfig.ApiBaseUrl.Port}";
    public string ServerScheme => AppConfig.ApiBaseUrl.Scheme.ToLower();
    public bool IsHttpWithoutLocalhost => ServerScheme == "http" && !IsLoopbackHost(AppConfig.ApiBaseUrl.Host);

    public event Action? StateChanged;

    private ApiService()
    {
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        Username = AIZhijian.Properties.Settings.Default.CachedUsername;
        Role = AIZhijian.Properties.Settings.Default.CachedRole;
        UserId = AIZhijian.Properties.Settings.Default.CachedUserId;
    }

    private Uri BuildUrl(string path)
        => new Uri(AppConfig.ApiBaseUrl, path);

    private static bool IsLoopbackHost(string? host)
        => host is "localhost" or "localhost." or "127.0.0.1" or "::1";

    public void NotifyStateChanged() => StateChanged?.Invoke();

    // ── Auth ──

    public async Task CheckSessionStatus()
    {
        try
        {
            var result = await Check(5);
            if (result.Authenticated)
            {
                Username = result.Username ?? "";
                Role = result.Role ?? "USER";
                UserId = result.UserId;
                IsLoggedIn = true;
                SaveUserInfo();
            }
            else
            {
                await LoginWithSavedCredentialsOrReset();
            }
        }
        catch
        {
            await LoginWithSavedCredentialsOrReset();
        }
        finally
        {
            IsCheckingSession = false;
            NotifyStateChanged();
        }
    }

    public async Task<CheckResponse> Check(int timeoutSeconds = 30)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl("/api/auth/check"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendAsync<CheckResponse>(req, timeoutSeconds);
    }

    public async Task Login(string username, string password, bool? remember = null)
    {
        IsLoggingIn = true;
        LoginError = null;
        try
        {
            var body = new { username, password };
            var result = await PostJsonAsync<LoginResponse>("/api/auth/login", body);
            if (result.Success)
            {
                var checkResult = await Check();
                Username = checkResult.Username ?? username;
                Role = checkResult.Role ?? result.Role ?? "USER";
                UserId = checkResult.UserId;
                IsLoggedIn = true;
                SaveUserInfo();
                if (remember.HasValue) RememberLogin = remember.Value;
                UpdateSavedCredentials(username, password);
            }
            else
            {
                LoginError = result.Message ?? "登录失败";
            }
        }
        catch (Exception ex)
        {
            LoginError = ex.Message;
        }
        finally
        {
            IsLoggingIn = false;
            NotifyStateChanged();
        }
    }

    public async Task Logout()
    {
        try { await PostJsonAsync<EmptyResponse>("/api/auth/logout", new { }); } catch { }
        ResetAuthState(true);
        RememberLogin = false;
        NotifyStateChanged();
    }

    public string? GetSavedUsername()
    {
        if (!RememberLogin) return null;
        var cred = CredentialStore.Load();
        return cred?.Username;
    }

    public string? GetSavedPassword()
    {
        if (!RememberLogin) return null;
        var cred = CredentialStore.Load();
        return cred?.Password;
    }

    // ── Image Generation ──

    public async Task<TaskSubmitResponse> GenerateImage(string prompt, string channel, string aspectRatio,
        string resolution, string quality, bool photoReal)
    {
        var body = new Dictionary<string, object>
        {
            ["prompt"] = prompt, ["channel"] = channel,
            ["aspectRatio"] = aspectRatio, ["photoReal"] = photoReal
        };
        if (channel == "official") { body["resolution"] = resolution; body["quality"] = quality; }
        return await PostJsonAsync<TaskSubmitResponse>("/api/gpt-image-2/text-to-image", body);
    }

    public async Task<TaskSubmitResponse> GenerateImageToImage(string prompt, string channel, string aspectRatio,
        string resolution, string quality, List<FileRef> referenceImages)
    {
        var fields = new Dictionary<string, string>
        {
            ["prompt"] = prompt, ["channel"] = channel, ["aspectRatio"] = aspectRatio
        };
        if (channel == "official") { fields["resolution"] = resolution; fields["quality"] = quality; }
        var files = referenceImages.Select(f => ("files", f.Name, f.Mime, f.Data)).ToList();
        return await UploadMultipartAsync<TaskSubmitResponse>("/api/gpt-image-2/image-to-image", fields, files);
    }

    public async Task<TaskPollResponse> PollImageTask(string taskId)
        => await GetJsonAsync<TaskPollResponse>($"/api/gpt-image-2/poll?ourTaskId={Uri.EscapeDataString(taskId)}");

    // ── Banana ──

    public async Task<byte[]> GenerateBanana(string prompt, string provider, List<FileRef> referenceImages)
    {
        var fields = new Dictionary<string, string> { ["prompt"] = prompt, ["provider"] = provider };
        var files = referenceImages.Select(f => ("image", f.Name, f.Mime, f.Data)).ToList();
        var (data, contentType) = await UploadMultipartRawAsync("/api/media/banana", fields, files);
        if (data != null && (contentType?.Contains("image") == true))
            return data;

        if (data != null)
        {
            var resp = JsonSerializer.Deserialize<TaskSubmitResponse>(data);
            if (resp is { Success: false }) throw new ApiException(resp.Message ?? "生成失败");
        }
        throw new ApiException("未返回图片数据");
    }

    // ── Seedance ──

    public async Task<TaskSubmitResponse> GenerateSeedanceVideo(string prompt, string mode, string model,
        string ratio, string resolution, int duration, int count, bool generateAudio, List<SeedanceAsset> assets)
    {
        var payloadAssets = assets.Select(a => new Dictionary<string, object>
        {
            ["type"] = a.Type, ["name"] = a.Name, ["mime"] = a.Mime,
            ["size"] = a.Size, ["duration"] = a.Duration, ["dataUrl"] = a.GetDataUrl()
        }).ToList();

        var body = new Dictionary<string, object>
        {
            ["prompt"] = prompt, ["mode"] = mode, ["model"] = model,
            ["ratio"] = ratio, ["resolution"] = resolution, ["duration"] = duration,
            ["count"] = count, ["generateAudio"] = generateAudio, ["assets"] = payloadAssets
        };
        return await PostJsonAsync<TaskSubmitResponse>("/api/seedance20/submit", body);
    }

    public async Task<TaskPollResponse> PollSeedanceTask(string taskId)
        => await GetJsonAsync<TaskPollResponse>($"/api/seedance20/poll?ourTaskId={Uri.EscapeDataString(taskId)}");

    public async Task<SeedanceVirtualAssetConfigResponse> GetSeedanceVirtualAssetConfig()
        => await GetJsonAsync<SeedanceVirtualAssetConfigResponse>("/api/seedance20/virtual-assets/config");

    public async Task<SeedanceVirtualAssetGroupListResponse> GetSeedanceVirtualAssetGroups()
        => await GetJsonAsync<SeedanceVirtualAssetGroupListResponse>("/api/seedance20/virtual-assets/groups");

    public async Task<SeedanceVirtualAssetMutationResponse> CreateSeedanceVirtualAssetGroup(string displayName)
        => await PostJsonAsync<SeedanceVirtualAssetMutationResponse>("/api/seedance20/virtual-assets/groups", new { displayName, description = "" });

    public async Task<SeedanceVirtualAssetItemListResponse> GetSeedanceVirtualAssetItems(int groupId)
        => await GetJsonAsync<SeedanceVirtualAssetItemListResponse>($"/api/seedance20/virtual-assets/groups/{groupId}/items");

    public async Task<SeedanceVirtualAssetMutationResponse> ImportSeedanceVirtualAssetImage(int groupId, string displayName, FileRef image)
    {
        var dataUrl = $"data:{image.Mime};base64,{Convert.ToBase64String(image.Data)}";
        return await PostJsonAsync<SeedanceVirtualAssetMutationResponse>($"/api/seedance20/virtual-assets/groups/{groupId}/import-image", new { displayName, dataUrl });
    }

    public async Task<SeedanceVirtualAssetMutationResponse> RefreshSeedanceVirtualAssetItem(int localId)
        => await PostJsonAsync<SeedanceVirtualAssetMutationResponse>($"/api/seedance20/virtual-assets/items/{localId}/refresh", new { });

    // ── Wan ──

    public async Task<TaskSubmitResponse> GenerateWanVideo(byte[] imageData, string fileName, string mimeType,
        string prompt, int width, int height, int seconds)
    {
        var fields = new Dictionary<string, string>
        {
            ["text"] = prompt, ["width"] = width.ToString(),
            ["height"] = height.ToString(), ["seconds"] = seconds.ToString()
        };
        var files = new List<(string, string, string, byte[])> { ("image", fileName, mimeType, imageData) };
        return await UploadMultipartAsync<TaskSubmitResponse>("/api/media/wan2-image-to-video", fields, files);
    }

    public async Task<TaskSubmitResponse> GenerateWanFirstLastVideo(FileRef firstFrame, FileRef lastFrame,
        string prompt, int seconds, bool enable48G)
    {
        var fields = new Dictionary<string, string>
        {
            ["text"] = prompt, ["seconds"] = seconds.ToString(),
            ["enable48G"] = enable48G ? "true" : "false"
        };
        var files = new List<(string, string, string, byte[])>
        {
            ("firstFrame", firstFrame.Name, firstFrame.Mime, firstFrame.Data),
            ("lastFrame", lastFrame.Name, lastFrame.Mime, lastFrame.Data)
        };
        return await UploadMultipartAsync<TaskSubmitResponse>("/api/media/wan2-first-last-frame", fields, files);
    }

    public async Task<TaskPollResponse> PollMediaTask(string taskId)
    {
        var fields = new Dictionary<string, string> { ["taskId"] = taskId };
        return await UploadMultipartAsync<TaskPollResponse>("/api/media/task-result", fields, new());
    }

    // ── Veo ──

    public async Task<TaskSubmitResponse> GenerateVeoVideo(VeoJobParams p)
    {
        var fields = new Dictionary<string, string>
        {
            ["channel"] = p.Channel, ["model"] = p.Model, ["mode"] = p.Mode,
            ["prompt"] = p.Prompt, ["resolution"] = p.Resolution
        };
        if (p.Mode != "reference" && p.Mode != "extend")
            fields["aspectRatio"] = p.AspectRatio;
        if (VeoRules.ShouldSendDuration(p.Channel, p.Model, p.Mode))
            fields["duration"] = p.Duration;
        if (!string.IsNullOrEmpty(p.NegativePrompt))
            fields["negativePrompt"] = p.NegativePrompt;
        if (VeoRules.SupportsAudio(p.Channel, p.Model, p.Mode))
            fields["generateAudio"] = p.GenerateAudio ? "true" : "false";

        var files = new List<(string, string, string, byte[])>();
        for (int i = 0; i < Math.Min(p.ImageFiles.Count, 3); i++)
        {
            var f = p.ImageFiles[i];
            files.Add((i == 0 ? "image" : $"image{i + 1}", f.Name, f.Mime, f.Data));
        }
        if (p.ImageData != null && files.Count == 0)
            files.Add(("image", p.ImageName ?? "", p.ImageMime ?? "", p.ImageData));
        if (p.FirstImageData != null)
            files.Add(("firstImage", p.FirstImageName ?? "", p.FirstImageMime ?? "", p.FirstImageData));
        if (p.LastImageData != null)
            files.Add(("lastImage", p.LastImageName ?? "", p.LastImageMime ?? "", p.LastImageData));
        var refs = new[] { p.Ref1Data, p.Ref2Data, p.Ref3Data };
        for (int i = 0; i < refs.Length; i++)
            if (refs[i].HasValue)
                files.Add(($"refImage{i + 1}", refs[i]!.Value.Name, refs[i]!.Value.Mime, refs[i]!.Value.Data));
        if (p.VideoData != null)
            files.Add(("video", p.VideoName ?? "", p.VideoMime ?? "", p.VideoData));

        return await UploadMultipartAsync<TaskSubmitResponse>("/api/veo-video/submit", fields, files);
    }

    public async Task<TaskPollResponse> PollVeoTask(string taskId)
        => await GetJsonAsync<TaskPollResponse>($"/api/veo-video/poll?ourTaskId={Uri.EscapeDataString(taskId)}");

    // ── Grok ──

    public async Task<TaskSubmitResponse> GenerateGrokVideo(string prompt, string channel, string mode,
        string aspectRatio, string resolution, string duration,
        List<(byte[], string, string)> imageFiles,
        byte[]? videoData, string? videoName, string? videoMime)
    {
        var fields = new Dictionary<string, string>
        {
            ["prompt"] = prompt, ["channel"] = channel, ["mode"] = mode,
            ["aspectRatio"] = aspectRatio, ["resolution"] = resolution, ["duration"] = duration
        };
        var files = imageFiles.Select(f => ("images", f.Item2, f.Item3, f.Item1)).ToList();
        if (videoData != null)
            files.Add(("video", videoName ?? "", videoMime ?? "", videoData));

        return await UploadMultipartAsync<TaskSubmitResponse>("/api/grok-video/submit", fields, files);
    }

    public async Task<TaskPollResponse> PollGrokTask(string taskId)
        => await GetJsonAsync<TaskPollResponse>($"/api/grok-video/task/{Uri.EscapeDataString(taskId)}");

    // ── Active Tasks ──

    public void AddTask(string id, string type, string desc)
    {
        if (!ActiveTasks.Any(t => t.Id == id))
            ActiveTasks.Add(new ActiveTask { Id = id, Type = type, Desc = desc, StartTime = DateTime.Now });
    }

    public void RemoveTask(string id) => ActiveTasks.RemoveAll(t => t.Id == id);

    // ── HTTP Helpers ──

    private async Task<T> GetJsonAsync<T>(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl(path));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendAsync<T>(req);
    }

    private async Task<T> PostJsonAsync<T>(string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, BuildUrl(path));
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await SendAsync<T>(req);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, int timeoutSeconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var response = await _httpClient.SendAsync(request, cts.Token);
        var json = await response.Content.ReadAsStringAsync(cts.Token);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new ApiException("未登录");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new ApiException("无权限");
        if (!response.IsSuccessStatusCode)
        {
            var msg = TryGetErrorMessage(json) ?? $"请求失败 ({(int)response.StatusCode})";
            throw new ApiException(msg);
        }

        try { return JsonSerializer.Deserialize<T>(json) ?? throw new ApiException("数据解析失败"); }
        catch (JsonException) { throw new ApiException("数据解析失败"); }
    }

    private async Task<T> UploadMultipartAsync<T>(string path, Dictionary<string, string> fields,
        List<(string Name, string FileName, string Mime, byte[] Data)> files)
    {
        var (data, _) = await UploadMultipartRawAsync(path, fields, files);
        if (data == null) throw new ApiException("无效响应");
        return JsonSerializer.Deserialize<T>(data) ?? throw new ApiException("数据解析失败");
    }

    private async Task<(byte[]? Data, string? ContentType)> UploadMultipartRawAsync(string path,
        Dictionary<string, string> fields, List<(string Name, string FileName, string Mime, byte[] Data)> files)
    {
        var boundary = $"Boundary-{Guid.NewGuid()}";
        using var content = new MultipartFormDataContent(boundary);

        foreach (var (name, value) in fields)
            content.Add(new StringContent(value), name);

        foreach (var (name, fileName, mime, data) in files)
        {
            var fileContent = new ByteArrayContent(data);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
            content.Add(fileContent, name, fileName);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var response = await _httpClient.PostAsync(BuildUrl(path), content, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            throw new ApiException(TryGetErrorMessage(json) ?? $"请求失败 ({(int)response.StatusCode})");
        }

        var responseData = await response.Content.ReadAsByteArrayAsync(cts.Token);
        var contentType = response.Content.Headers.ContentType?.ToString();
        return (responseData, contentType);
    }

    private static string? TryGetErrorMessage(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("errorMessage", out var emsg))
                return emsg.GetString();
        }
        catch { }
        return !string.IsNullOrWhiteSpace(json) ? json[..Math.Min(json.Length, 300)] : null;
    }

    // ── Private helpers ──

    private async Task LoginWithSavedCredentialsOrReset()
    {
        var cred = CredentialStore.Load();
        if (cred == null) { ResetAuthState(true); return; }
        await Login(cred.Username, cred.Password);
        if (!IsLoggedIn) { ResetAuthState(true); RememberLogin = false; }
    }

    private void UpdateSavedCredentials(string username, string password)
    {
        if (RememberLogin)
            CredentialStore.Save(username, password);
        else
            CredentialStore.Delete();
    }

    private void SaveUserInfo()
    {
        AIZhijian.Properties.Settings.Default.CachedUsername = Username;
        AIZhijian.Properties.Settings.Default.CachedRole = Role;
        AIZhijian.Properties.Settings.Default.CachedUserId = UserId;
        AIZhijian.Properties.Settings.Default.Save();
    }

    public void ResetAuthState(bool clearCache = false)
    {
        _cookieContainer.GetAllCookies().ToList().ForEach(c => c.Expired = true);
        IsLoggedIn = false;
        Username = "";
        Role = "";
        UserId = 0;
        ActiveTasks.Clear();
        if (clearCache)
        {
            AIZhijian.Properties.Settings.Default.CachedUsername = "";
            AIZhijian.Properties.Settings.Default.CachedRole = "";
            AIZhijian.Properties.Settings.Default.CachedUserId = 0;
            AIZhijian.Properties.Settings.Default.Save();
        }
    }

    public void ResetForNewHost()
    {
        ResetAuthState(true);
        IsCheckingSession = false;
        RememberLogin = false;
        CredentialStore.Delete();
    }
}

public class ActiveTask
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Desc { get; set; } = "";
    public DateTime StartTime { get; set; } = DateTime.Now;
}
