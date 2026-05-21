using System.Text.Json.Serialization;

namespace AIZhijian.Models;

public enum PresetKind { GptImage, Banana, Seedance, Wan, Veo, Grok }

public class Preset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public PresetKind Kind { get; set; }
    public string ParamsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PresetListWrapper
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Preset> Presets { get; set; } = new();
}
