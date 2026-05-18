using System.Configuration;

namespace AIZhijian.Properties;

internal sealed class Settings : ApplicationSettingsBase
{
    private static readonly Settings _default = (Settings)Synchronized(new Settings());

    public static Settings Default => _default;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string CachedUsername { get => (string)this["CachedUsername"]; set => this["CachedUsername"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string CachedRole { get => (string)this["CachedRole"]; set => this["CachedRole"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public int CachedUserId { get => (int)this["CachedUserId"]; set => this["CachedUserId"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool RememberLogin { get => (bool)this["RememberLogin"]; set => this["RememberLogin"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string ApiBaseUrlOverride { get => (string)this["ApiBaseUrlOverride"]; set => this["ApiBaseUrlOverride"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string WorksRecords { get => (string)this["WorksRecords"]; set => this["WorksRecords"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string WorksFavorites { get => (string)this["WorksFavorites"]; set => this["WorksFavorites"] = value; }

    [UserScopedSetting]
    [DefaultSettingValue("3")]
    public int ConcurrencyLimit { get => (int)this["ConcurrencyLimit"]; set => this["ConcurrencyLimit"] = value; }
}
