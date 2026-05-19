using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AIZhijian.Models;
using AIZhijian.Services;

namespace AIZhijian.Views;

public partial class SeedancePage : UserControl
{
    private readonly List<SeedanceAsset> _assets = new();
    private List<SeedanceVirtualAssetGroup> _assetGroups = new();
    private List<SeedanceVirtualAssetItem> _assetItems = new();
    private bool _assetConfigured;
    private int? _selectedAssetGroupId;
    private int _assetLoadingCount;
    private string? _lastVideoUrl;

    public SeedancePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadVirtualAssetsAsync();
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

    // ── Virtual Asset Management ──

    private async Task LoadVirtualAssetsAsync()
    {
        BeginAssetLoading();
        try
        {
            var config = await ApiService.Instance.GetSeedanceVirtualAssetConfig();
            _assetConfigured = config.AssetApiConfigured == true;
            AssetConfigText.Text = GetAssetConfigMessage(config);
            AssetConfigText.Visibility = Visibility.Visible;
            if (!_assetConfigured)
            {
                _assetGroups.Clear();
                _assetItems.Clear();
                AssetGroupBox.ItemsSource = null;
                AssetItemsControl.Visibility = Visibility.Collapsed;
                return;
            }
            var groupsResponse = await ApiService.Instance.GetSeedanceVirtualAssetGroups();
            if (groupsResponse.Success)
            {
                _assetGroups = groupsResponse.Items ?? new();
                AssetGroupBox.ItemsSource = _assetGroups;
                AssetGroupBox.DisplayMemberPath = "DisplayName";
                AssetGroupBox.SelectedValuePath = "Id";
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
        BeginAssetLoading();
        try
        {
            var response = await ApiService.Instance.GetSeedanceVirtualAssetItems(groupId);
            if (response.Success)
            {
                _assetItems = response.Items ?? new();
                AssetItemsControl.ItemsSource = _assetItems;
                AssetItemsControl.Visibility = _assetItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ShowAssetError(response.Message ?? "加载素材列表失败");
            }
        }
        catch (Exception ex)
        {
            ShowAssetError($"加载素材列表失败: {ex.Message}");
        }
        finally
        {
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
    }

    private async void RefreshGroups_Click(object sender, RoutedEventArgs e)
    {
        await LoadVirtualAssetsAsync();
    }

    private async void CreateGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = NewGroupBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        BeginAssetLoading();
        try
        {
            var response = await ApiService.Instance.CreateSeedanceVirtualAssetGroup(name);
            if (response.Success)
            {
                NewGroupBox.Text = "";
                await LoadVirtualAssetsAsync();
                if (response.Id.HasValue)
                {
                    _selectedAssetGroupId = response.Id.Value;
                    AssetGroupBox.SelectedValue = response.Id.Value;
                    await LoadVirtualAssetItemsAsync(response.Id.Value);
                }
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

    private async void AddVirtualAsset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not SeedanceVirtualAssetItem item)
            return;

        if (!item.IsActive)
        {
            // Refresh the item if not active
            try
            {
                await ApiService.Instance.RefreshSeedanceVirtualAssetItem(item.Id);
                if (_selectedAssetGroupId.HasValue)
                    await LoadVirtualAssetItemsAsync(_selectedAssetGroupId.Value);
            }
            catch { }
            return;
        }

        var assetUri = item.AssetUri ?? item.ArkAssetId;
        if (string.IsNullOrEmpty(assetUri))
        {
            ShowAssetError("素材无可用 URI，请尝试刷新");
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
