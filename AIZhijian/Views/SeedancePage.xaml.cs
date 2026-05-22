using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;
using System.Text.Json;

namespace AIZhijian.Views;

public partial class SeedancePage : UserControl
{
    private readonly List<SeedanceAsset> _assets = new();
    private List<SeedanceVirtualAssetGroup> _assetGroups = new();
    private bool _assetConfigured;
    private int _assetLoadingCount;
    private int? _selectedAssetGroupId;
    private int? _loadingAssetGroupId;
    private string? _lastVideoUrl;

    public SeedancePage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            RefreshPresetList();
            await LoadVirtualAssetsAsync();
            TryApplyPendingParams();
        };
    }

    private void TryApplyPendingParams()
    {
        var pending = GenerationQueueStore.PendingEditParams;
        if (pending is not SeedanceJobParams p) return;
        GenerationQueueStore.PendingEditParams = null;
        PromptBox.Text = p.Prompt;
        SetComboByTag(ModeBox, p.Mode);
        SetComboByTag(ModelBox, p.Model);
        SetComboByTag(RatioBox, p.Ratio);
        SetComboByTag(ResolutionBox, p.Resolution);
        DurationBox.Text = p.Duration.ToString();
        CountBox.Text = p.Count.ToString();
        AudioCheck.IsChecked = p.GenerateAudio;
        StatusText.Text = "已从失败任务恢复参数，请重新选择素材后提交";
    }

    private string GetTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void PickAssets_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "媒体文件|*.png;*.jpg;*.jpeg;*.mp4;*.mov;*.avi|所有|*.*", Multiselect = true };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            var data = System.IO.File.ReadAllBytes(path);
            var ext = Path.GetExtension(path).ToLower();
            var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".mp4" => "video/mp4", ".mov" => "video/quicktime", _ => "application/octet-stream" };
            var type = mime.StartsWith("video") ? "video" : "image";

            _assets.Add(new SeedanceAsset
            {
                Type = type, Name = Path.GetFileName(path), Mime = mime,
                Size = data.Length, Data = data
            });

            var tb = new TextBlock { Text = $"[{type}] {Path.GetFileName(path)}",
                FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray };
            AssetsPanel.Children.Add(tb);
        }
    }

    private async void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationBox.Text, out var dur) || dur < 4 || dur > 15)
        { StatusText.Text = "时长需在4-15秒"; return; }
        if (!int.TryParse(CountBox.Text, out var cnt) || cnt < 1 || cnt > 4)
        { StatusText.Text = "数量需在1-4"; return; }

        GenerateBtn.IsEnabled = false;
        StatusText.Text = "提交中...";
        ResultPanel.Visibility = Visibility.Collapsed;

        try
        {
            var result = await ApiService.Instance.GenerateSeedanceVideo(
                PromptBox.Text.Trim(), GetTag(ModeBox), GetTag(ModelBox), GetTag(RatioBox),
                GetTag(ResolutionBox), dur, cnt, AudioCheck.IsChecked ?? false, _assets);

            if (!string.IsNullOrEmpty(result.OurTaskId))
            {
                StatusText.Text = $"已提交: {result.OurTaskId}";
                PollSeedance(result.OurTaskId);
            }
            else StatusText.Text = result.Message ?? "提交失败";
        }
        catch (Exception ex) { StatusText.Text = $"错误: {ex.Message}"; }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void PollSeedance(string taskId)
    {
        for (int i = 0; i < 180; i++)
        {
            await Task.Delay(3000);
            try
            {
                var poll = await ApiService.Instance.PollSeedanceTask(taskId);
                var status = (poll.DbStatus ?? "").ToUpper();
                if (status == "SUCCESS")
                {
                    Dispatcher.Invoke(() =>
                    {
                        _lastVideoUrl = poll.VideoUrl;
                        ResultPanel.Visibility = Visibility.Visible;
                        ResultLink.Text = poll.VideoUrl ?? "结果链接";
                        StatusText.Text = "生成完成";
                    });
                    return;
                }
                if (status is "FAILED" or "CANCELLED" or "ERROR")
                {
                    Dispatcher.Invoke(() => StatusText.Text = poll.ErrorMessage ?? "任务失败");
                    return;
                }
                Dispatcher.Invoke(() => StatusText.Text = poll.RhStatus ?? poll.DbStatus ?? "处理中...");
            }
            catch { }
        }
    }

    private void ResultLink_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastVideoUrl))
            try { Process.Start(new ProcessStartInfo(_lastVideoUrl) { UseShellExecute = true }); } catch { }
    }

    // ── Preset Management ──

    private void RefreshPresetList()
    {
        var presets = PresetStore.GetPresets(PresetKind.Seedance);
        PresetBox.ItemsSource = presets;
        PresetBox.SelectedIndex = -1;
        DeletePresetBtn.IsEnabled = false;
    }

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) { DeletePresetBtn.IsEnabled = false; return; }
        DeletePresetBtn.IsEnabled = true;
        var preset = PresetStore.GetPreset(id, PresetKind.Seedance);
        if (preset == null) return;

        try
        {
            var p = JsonSerializer.Deserialize<SeedanceJobParams>(preset.ParamsJson);
            if (p == null) return;
            PromptBox.Text = p.Prompt;
            SetComboByTag(ModeBox, p.Mode);
            SetComboByTag(ModelBox, p.Model);
            SetComboByTag(RatioBox, p.Ratio);
            SetComboByTag(ResolutionBox, p.Resolution);
            DurationBox.Text = p.Duration.ToString();
            CountBox.Text = p.Count.ToString();
            AudioCheck.IsChecked = p.GenerateAudio;
        }
        catch { StatusText.Text = "加载预设失败"; }
    }

    private static void SetComboByTag(ComboBox cb, string tag)
    {
        foreach (ComboBoxItem item in cb.Items)
            if (item.Tag?.ToString() == tag) { cb.SelectedItem = item; return; }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationBox.Text, out var dur)) dur = 5;
        if (!int.TryParse(CountBox.Text, out var cnt)) cnt = 1;

        var dlg = new TextInputDialog("预设名称", "请输入预设名称:");
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Answer)) return;

        var p = new SeedanceJobParams
        {
            Prompt = PromptBox.Text.Trim(),
            Mode = GetTag(ModeBox),
            Model = GetTag(ModelBox),
            Ratio = GetTag(RatioBox),
            Resolution = GetTag(ResolutionBox),
            Duration = dur,
            Count = cnt,
            GenerateAudio = AudioCheck.IsChecked ?? true
        };

        var name = dlg.Answer.Trim();
        var existing = PresetStore.FindByName(name, PresetKind.Seedance);
        if (existing != null)
        {
            var overwrite = MessageBox.Show($"已存在名为 \"{name}\" 的预设，是否覆盖？", "预设已存在",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        var preset = new Preset
        {
            Name = name,
            Kind = PresetKind.Seedance,
            ParamsJson = JsonSerializer.Serialize(p)
        };
        PresetStore.SavePreset(preset);
        RefreshPresetList();
        PresetBox.SelectedValue = preset.Id;
        StatusText.Text = $"预设 \"{preset.Name}\" 已保存";
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetBox.SelectedValue is not string id) return;
        var preset = PresetStore.GetPreset(id, PresetKind.Seedance);
        if (preset == null) return;
        var result = MessageBox.Show($"确定删除预设 \"{preset.Name}\"?", "删除预设",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        PresetStore.DeletePreset(id, PresetKind.Seedance);
        RefreshPresetList();
        StatusText.Text = $"预设 \"{preset.Name}\" 已删除";
    }

    // ── Virtual Asset Management ──

    private async Task LoadVirtualAssetsAsync()
    {
        AssetErrorText.Visibility = Visibility.Collapsed;
        BeginAssetLoading();
        try
        {
            var config = await ApiService.Instance.GetSeedanceVirtualAssetConfig();
            _assetConfigured = config.AssetApiConfigured == true;
            AssetConfigText.Text = GetAssetConfigMessage(config);
            if (!_assetConfigured)
            {
                _assetGroups.Clear();
                _selectedAssetGroupId = null;
                AssetGroupBox.ItemsSource = null;
                AssetItemsControl.Visibility = Visibility.Collapsed;
                return;
            }
            var groupsResponse = await ApiService.Instance.GetSeedanceVirtualAssetGroups();
            if (groupsResponse.Success)
            {
                _assetGroups = groupsResponse.Items ?? new();
                AssetGroupBox.ItemsSource = _assetGroups;
            }
        }
        catch (Exception ex)
        {
            ShowAssetError($"加载素材库失败: {ex.Message}");
        }
        finally
        {
            EndAssetLoading();
        }
    }

    private async Task LoadVirtualAssetItemsAsync(int groupId)
    {
        AssetErrorText.Visibility = Visibility.Collapsed;
        _loadingAssetGroupId = groupId;
        BeginAssetLoading();
        try
        {
            var response = await ApiService.Instance.GetSeedanceVirtualAssetItems(groupId);
            if (_loadingAssetGroupId != groupId) return;
            if (response.Success)
            {
                var items = response.Items ?? new();
                AssetItemsControl.ItemsSource = items;
                AssetItemsControl.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ShowAssetError(response.Message ?? "加载素材列表失败");
            }
        }
        catch (Exception ex)
        {
            if (_loadingAssetGroupId == groupId)
                ShowAssetError($"加载素材列表失败: {ex.Message}");
        }
        finally
        {
            if (_loadingAssetGroupId == groupId)
                _loadingAssetGroupId = null;
            EndAssetLoading();
        }
    }

    private void AssetGroupBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AssetGroupBox.SelectedValue is int groupId)
        {
            _selectedAssetGroupId = groupId;
            _ = LoadVirtualAssetItemsAsync(groupId);
        }
        else
        {
            _selectedAssetGroupId = null;
            AssetItemsControl.ItemsSource = null;
            AssetItemsControl.Visibility = Visibility.Collapsed;
        }
    }

    private async void RefreshGroups_Click(object sender, RoutedEventArgs e)
    {
        await LoadVirtualAssetsAsync();
    }

    private async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = NewGroupBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        AssetErrorText.Visibility = Visibility.Collapsed;
        BeginAssetLoading();
        try
        {
            var response = await ApiService.Instance.CreateSeedanceVirtualAssetGroup(name);
            if (response.Success)
            {
                NewGroupBox.Text = "";
                await LoadVirtualAssetsAsync();
                if (response.Id.HasValue)
                    AssetGroupBox.SelectedValue = response.Id.Value;
            }
            else
            {
                ShowAssetError(response.Message ?? "创建素材组失败");
            }
        }
        catch (Exception ex)
        {
            ShowAssetError($"创建素材组失败: {ex.Message}");
        }
        finally
        {
            EndAssetLoading();
        }
    }

    private async void ImportAsset_Click(object sender, RoutedEventArgs e)
    {
        if (!_selectedAssetGroupId.HasValue) { ShowAssetError("请先选择素材组"); return; }
        var name = ImportNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) { ShowAssetError("请输入素材名称"); return; }
        AssetErrorText.Visibility = Visibility.Collapsed;

        var dlg = new OpenFileDialog { Filter = "图片文件|*.png;*.jpg;*.jpeg|所有|*.*" };
        if (dlg.ShowDialog() != true) return;

        BeginAssetLoading();
        try
        {
            var data = System.IO.File.ReadAllBytes(dlg.FileName);
            var ext = System.IO.Path.GetExtension(dlg.FileName).ToLower();
            var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream" };
            var fileRef = new FileRef { Data = data, Name = System.IO.Path.GetFileName(dlg.FileName), Mime = mime };

            var response = await ApiService.Instance.ImportSeedanceVirtualAssetImage(
                _selectedAssetGroupId.Value, name, fileRef);
            if (response.Success)
            {
                ImportNameBox.Text = "";
                if (_selectedAssetGroupId.HasValue)
                    await LoadVirtualAssetItemsAsync(_selectedAssetGroupId.Value);
            }
            else
            {
                ShowAssetError(response.Message ?? "导入素材失败");
            }
        }
        catch (Exception ex)
        {
            ShowAssetError($"导入素材失败: {ex.Message}");
        }
        finally
        {
            EndAssetLoading();
        }
    }

    private void AddVirtualAsset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not SeedanceVirtualAssetItem item)
            return;

        if (!item.IsActive)
        {
            ShowAssetError("素材非 Active 状态，请先刷新素材组");
            return;
        }

        var assetUri = item.AssetUri ?? item.ArkAssetId;
        if (string.IsNullOrEmpty(assetUri))
        {
            ShowAssetError("素材无可用 URI");
            return;
        }

        if (_assets.Any(a => a.DataUrl == assetUri))
        {
            ShowAssetError("该素材已在参考列表中");
            return;
        }

        _assets.Add(new SeedanceAsset
        {
            Type = "image",
            Name = item.DisplayName ?? item.ArkAssetId ?? "虚拟素材",
            Mime = "image/png",
            Size = 1,
            DataUrl = assetUri
        });

        var tb = new TextBlock
        {
            Text = $"[虚拟] {item.DisplayName ?? item.ArkAssetId ?? "素材"}",
            FontSize = 12,
            Foreground = System.Windows.Media.Brushes.Gray
        };
        AssetsPanel.Children.Add(tb);
        AssetErrorText.Visibility = Visibility.Collapsed;
    }

    private void BeginAssetLoading()
    {
        _assetLoadingCount++;
        AssetLoadingBar.Visibility = Visibility.Visible;
    }

    private void EndAssetLoading()
    {
        _assetLoadingCount = Math.Max(0, _assetLoadingCount - 1);
        if (_assetLoadingCount == 0)
            AssetLoadingBar.Visibility = Visibility.Collapsed;
    }

    private void ShowAssetError(string message)
    {
        AssetErrorText.Text = message;
        AssetErrorText.Visibility = Visibility.Visible;
    }

    private static string GetAssetConfigMessage(SeedanceVirtualAssetConfigResponse config)
    {
        if (config.AssetApiConfigured != true)
        {
            if (config.AssetAccessKeyPresent == true && config.AssetSecretKeyPresent != true)
                return "素材库缺少 secret-key 配置";
            if (config.AssetAccessKeyPresent != true && config.AssetSecretKeyPresent == true)
                return "素材库缺少 access-key 配置";
            return "素材库未配置 AK/SK";
        }
        if (config.CosConfigured != true)
            return "素材库接口已配置，请确认 COS 可用";
        return "素材库可用，Active 素材可加入参考";
    }
}
