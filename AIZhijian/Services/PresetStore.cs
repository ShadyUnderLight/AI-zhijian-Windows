using System.IO;
using System.Text.Json;
using AIZhijian.Models;

namespace AIZhijian.Services;

public static class PresetStore
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIZhijian", "presets");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string GetFilePath(PresetKind kind)
        => Path.Combine(BaseDir, $"{kind}.json");

    public static List<Preset> GetPresets(PresetKind kind)
    {
        var path = GetFilePath(kind);
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Preset>>(json, JsonOptions) ?? new();
        }
        catch { return new(); }
    }

    public static void SavePreset(Preset preset)
    {
        Directory.CreateDirectory(BaseDir);
        var path = GetFilePath(preset.Kind);
        var presets = GetPresets(preset.Kind);
        var existing = presets.FindIndex(p => p.Id == preset.Id);
        if (existing >= 0)
            presets[existing] = preset;
        else
            presets.Add(preset);
        File.WriteAllText(path, JsonSerializer.Serialize(presets, JsonOptions));
    }

    public static void DeletePreset(string id, PresetKind kind)
    {
        var path = GetFilePath(kind);
        if (!File.Exists(path)) return;
        var presets = GetPresets(kind);
        presets.RemoveAll(p => p.Id == id);
        File.WriteAllText(path, JsonSerializer.Serialize(presets, JsonOptions));
    }

    public static Preset? GetPreset(string id, PresetKind kind)
        => GetPresets(kind).FirstOrDefault(p => p.Id == id);
}
