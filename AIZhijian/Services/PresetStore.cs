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

    private static string GetBackupPath(PresetKind kind)
        => Path.Combine(BaseDir, $"{kind}.json.bak");

    public static List<Preset> GetPresets(PresetKind kind)
    {
        var path = GetFilePath(kind);
        if (!File.Exists(path)) return new();
        lock (_lock)
        {
            try
            {
                var json = File.ReadAllText(path);
                return ReadPresets(json, kind);
            }
            catch
            {
                return TryRecoverFromBackup(kind);
            }
        }
    }

    private static List<Preset> ReadPresets(string json, PresetKind kind)
    {
        var wrapper = TryDeserialize<PresetListWrapper>(json);
        if (wrapper?.Presets != null)
            return wrapper.Presets;

        var legacy = TryDeserialize<List<Preset>>(json);
        if (legacy != null)
            return legacy;

        return RecoverItemByItem(json, kind);
    }

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch { return null; }
    }

    private static List<Preset> RecoverItemByItem(string json, PresetKind kind)
    {
        BackupCorrupted(json, kind);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new();

            var presets = new List<Preset>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var preset = JsonSerializer.Deserialize<Preset>(element.GetRawText(), JsonOptions);
                    if (preset != null) presets.Add(preset);
                }
                catch { }
            }
            return presets;
        }
        catch
        {
            return new();
        }
    }

    private static void BackupCorrupted(string corruptedJson, PresetKind kind)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            File.WriteAllText(GetBackupPath(kind), corruptedJson);
        }
        catch { }
    }

    private static List<Preset> TryRecoverFromBackup(PresetKind kind)
    {
        try
        {
            var backupPath = GetBackupPath(kind);
            if (!File.Exists(backupPath)) return new();
            var json = File.ReadAllText(backupPath);
            return ReadPresets(json, kind);
        }
        catch
        {
            return new();
        }
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
                    ? ReadPresets(File.ReadAllText(path), preset.Kind)
                    : new List<Preset>();

                var existing = presets.FindIndex(p => p.Id == preset.Id);
                if (existing >= 0)
                    presets[existing] = preset;
                else
                    presets.Add(preset);

                WriteWrapper(path, presets);
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
                var presets = ReadPresets(File.ReadAllText(path), kind);
                presets.RemoveAll(p => p.Id == id);
                WriteWrapper(path, presets);
            }
            catch { }
        }
    }

    private static void WriteWrapper(string path, List<Preset> presets)
    {
        var wrapper = new PresetListWrapper
        {
            SchemaVersion = 1,
            Presets = presets
        };
        File.WriteAllText(path, JsonSerializer.Serialize(wrapper, JsonOptions));
    }

    public static Preset? GetPreset(string id, PresetKind kind)
        => GetPresets(kind).FirstOrDefault(p => p.Id == id);

    public static Preset? FindByName(string name, PresetKind kind)
        => GetPresets(kind).FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
