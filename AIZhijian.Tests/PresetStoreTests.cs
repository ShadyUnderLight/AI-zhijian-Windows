using System.IO;
using System.Text.Json;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Tests;

public class PresetStoreTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static Preset MakePreset(string name, PresetKind kind, string paramsJson = "{}")
    {
        return new Preset { Name = name, Kind = kind, ParamsJson = paramsJson };
    }

    [Fact]
    public void Save_and_retrieve_preset()
    {
        var preset = MakePreset("sv_ret", PresetKind.GptImage);
        PresetStore.SavePreset(preset);

        var loaded = PresetStore.GetPreset(preset.Id, PresetKind.GptImage);
        Assert.NotNull(loaded);
        Assert.Equal(preset.Name, loaded!.Name);
        Assert.Equal(preset.Kind, loaded.Kind);
    }

    [Fact]
    public void FindByName_returns_matching_preset()
    {
        var preset = MakePreset("fn_ret", PresetKind.Veo);
        PresetStore.SavePreset(preset);

        var found = PresetStore.FindByName("fn_ret", PresetKind.Veo);
        Assert.NotNull(found);
        Assert.Equal(preset.Name, found!.Name);
        Assert.Equal(preset.Kind, found.Kind);
    }

    [Fact]
    public void FindByName_case_insensitive()
    {
        var preset = MakePreset("CaseMix", PresetKind.Wan);
        PresetStore.SavePreset(preset);

        Assert.NotNull(PresetStore.FindByName("casemix", PresetKind.Wan));
        Assert.NotNull(PresetStore.FindByName("CASEMIX", PresetKind.Wan));
    }

    [Fact]
    public void DeletePreset_removes_it()
    {
        var preset = MakePreset("dl_rem", PresetKind.Seedance);
        PresetStore.SavePreset(preset);
        Assert.NotNull(PresetStore.GetPreset(preset.Id, PresetKind.Seedance));

        PresetStore.DeletePreset(preset.Id, PresetKind.Seedance);
        Assert.Null(PresetStore.GetPreset(preset.Id, PresetKind.Seedance));
    }

    [Fact]
    public void GetPresets_returns_all_for_kind()
    {
        var p1 = MakePreset("lst1", PresetKind.Grok);
        var p2 = MakePreset("lst2", PresetKind.Grok);
        PresetStore.SavePreset(p1);
        PresetStore.SavePreset(p2);

        var all = PresetStore.GetPresets(PresetKind.Grok);
        Assert.Contains(all, p => p.Id == p1.Id);
        Assert.Contains(all, p => p.Id == p2.Id);
    }

    [Fact]
    public void Isolated_by_kind()
    {
        var preset = MakePreset("iso", PresetKind.GptImage);
        PresetStore.SavePreset(preset);

        Assert.NotNull(PresetStore.GetPreset(preset.Id, PresetKind.GptImage));
        Assert.Null(PresetStore.GetPreset(preset.Id, PresetKind.Banana));
    }

    [Fact]
    public void SavePreset_overwrites_existing_id()
    {
        var preset = MakePreset("ovw", PresetKind.Banana);
        PresetStore.SavePreset(preset);

        var updated = new Preset
        {
            Id = preset.Id,
            Name = "ovw_upd",
            Kind = PresetKind.Banana,
            ParamsJson = "{\"prompt\":\"hello\"}"
        };
        PresetStore.SavePreset(updated);

        var loaded = PresetStore.GetPreset(preset.Id, PresetKind.GptImage);
        Assert.Null(loaded);

        loaded = PresetStore.GetPreset(preset.Id, PresetKind.Banana);
        Assert.NotNull(loaded);
        Assert.Equal("ovw_upd", loaded!.Name);
    }

    [Fact]
    public void WriteWrapper_format_can_be_read()
    {
        var preset = MakePreset("wrfmt", PresetKind.GptImage, "{\"key\":\"val\"}");
        PresetStore.SavePreset(preset);

        var loaded = PresetStore.GetPreset(preset.Id, PresetKind.GptImage);
        Assert.NotNull(loaded);
        Assert.Equal("{\"key\":\"val\"}", loaded!.ParamsJson);
    }

    [Fact]
    public void Read_legacy_unversioned_format()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIZhijian", "presets");
        var filePath = Path.Combine(baseDir, "GptImage.json");

        var existingData = File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        try
        {
            Directory.CreateDirectory(baseDir);
            var legacyPresets = new List<Preset>
            {
                new() { Name = "legacy_1", Kind = PresetKind.GptImage, ParamsJson = "{}" },
                new() { Name = "legacy_2", Kind = PresetKind.GptImage, ParamsJson = "{\"a\":1}" }
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(legacyPresets, JsonOptions));

            var presets = PresetStore.GetPresets(PresetKind.GptImage);
            Assert.Contains(presets, p => p.Name == "legacy_1");
            Assert.Contains(presets, p => p.Name == "legacy_2");
        }
        finally
        {
            if (existingData != null)
                File.WriteAllText(filePath, existingData);
            else if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void Rejects_unknown_schema_version()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIZhijian", "presets");
        var filePath = Path.Combine(baseDir, "GptImage.json");

        var existingData = File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        try
        {
            Directory.CreateDirectory(baseDir);
            var futureWrapper = new PresetListWrapper
            {
                SchemaVersion = 999,
                Presets = new List<Preset> { new() { Name = "future_data", Kind = PresetKind.GptImage } }
            };
            File.WriteAllText(filePath, JsonSerializer.Serialize(futureWrapper, JsonOptions));

            var presets = PresetStore.GetPresets(PresetKind.GptImage);
            Assert.DoesNotContain(presets, p => p.Name == "future_data");
        }
        finally
        {
            if (existingData != null)
                File.WriteAllText(filePath, existingData);
            else if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void Update_existing_preset_by_id()
    {
        var preset = MakePreset("upd_id", PresetKind.Veo, "{\"a\":1}");
        PresetStore.SavePreset(preset);

        var loaded = PresetStore.GetPreset(preset.Id, PresetKind.Veo);
        Assert.NotNull(loaded);
        Assert.Equal("{\"a\":1}", loaded!.ParamsJson);

        loaded.ParamsJson = "{\"a\":2}";
        PresetStore.SavePreset(loaded);
        loaded = null;

        var reloaded = PresetStore.GetPreset(preset.Id, PresetKind.Veo);
        Assert.NotNull(reloaded);
        Assert.Equal("{\"a\":2}", reloaded!.ParamsJson);
    }
}