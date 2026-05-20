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

    private static readonly object _lock = new();

    private static string GetFilePath(PresetKind kind)
        => Path.Combine(BaseDir, $"{kind}.json");

    public static List<Preset> GetPresets(PresetKind kind)
    {
        var path = GetFilePath(kind);
        if (!File.Exists(path)) return new();
        try
        {
            lock (_lock)
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Preset>>(json, JsonOptions) ?? new();
            }
        }
        catch { return new(); }
    }

    public static void SavePreset(Preset preset)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                var path = GetFilePath(preset.Kind);
                var presets = File.Exists(path)
                    ? JsonSerializer.Deserialize<List<Preset>>(File.ReadAllText(path), JsonOptions) ?? new()
                    : new List<Preset>();
                var existing = presets.FindIndex(p => p.Id == preset.Id);
                if (existing >= 0)
                    presets[existing] = preset;
                else
                    presets.Add(preset);
                File.WriteAllText(path, JsonSerializer.Serialize(presets, JsonOptions));
            }
            catch { }
        }
    }

    public static void DeletePreset(string id, PresetKind kind)
    {
        lock (_lock)
        {
            try
            {
                var path = GetFilePath(kind);
                if (!File.Exists(path)) return;
                var presets = JsonSerializer.Deserialize<List<Preset>>(File.ReadAllText(path), JsonOptions) ?? new();
                presets.RemoveAll(p => p.Id == id);
                File.WriteAllText(path, JsonSerializer.Serialize(presets, JsonOptions));
            }
            catch { }
        }
    }

    public static Preset? GetPreset(string id, PresetKind kind)
        => GetPresets(kind).FirstOrDefault(p => p.Id == id);

    public static Preset? FindByName(string name, PresetKind kind)
        => GetPresets(kind).FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
