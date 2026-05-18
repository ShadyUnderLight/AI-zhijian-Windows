namespace AIZhijian.Services;

public static class VeoRules
{
    public static readonly List<(string Value, string Label)> Channels =
    [
        ("budget", "低价"), ("official", "RH 官方"),
        ("google", "Google 官方"), ("yunwu", "云雾API中转")
    ];

    private static readonly List<string> BudgetModels = ["fast", "pro"];
    private static readonly List<string> FullModels = ["lite", "fast", "pro"];

    public static List<string> ValidModels(string channel) => channel switch
    {
        "budget" => BudgetModels,
        "official" or "google" or "yunwu" => FullModels,
        _ => []
    };

    public static List<(string Value, string Label)> ValidModes(string channel, string model)
    {
        return (channel, model) switch
        {
            ("budget", "fast") => [("text", "文生视频"), ("image", "图生视频"), ("start_end", "首尾帧")],
            ("budget", "pro") => [("text", "文生视频"), ("start_end", "首尾帧")],
            (_, "lite") => [("text", "文生视频"), ("image", "图生视频"), ("start_end", "首尾帧")],
            (_, "fast") => [("text", "文生视频"), ("image", "图生视频"), ("start_end", "首尾帧"), ("extend", "视频扩展")],
            (_, "pro") => [("text", "文生视频"), ("image", "图生视频"), ("start_end", "首尾帧"), ("reference", "参考生视频"), ("extend", "视频扩展")],
            _ => []
        };
    }

    public static bool IsValidCombination(string channel, string model) => ValidModels(channel).Contains(model);

    public static bool SupportsDuration(string channel, string model, string mode)
    {
        if (!IsValidCombination(channel, model)) return false;
        if (channel == "budget") return false;
        if (model == "lite" && mode == "start_end") return false;
        return mode is not ("reference" or "extend");
    }

    public static bool ShouldSendDuration(string channel, string model, string mode)
    {
        if (!IsValidCombination(channel, model)) return false;
        if (mode is "reference" or "extend") return false;
        if (model == "lite" && mode == "start_end") return false;
        return true;
    }

    public static string? FixedDuration(string channel, string model, string mode)
    {
        if (!IsValidCombination(channel, model)) return null;
        if (channel == "budget" && mode is not ("reference" or "extend"))
            return "8";
        return null;
    }

    public static bool SupportsAudio(string channel, string model, string mode)
    {
        if (!IsValidCombination(channel, model)) return false;
        return channel == "official" && model != "lite" && mode != "extend";
    }

    public static bool SupportsNegativePrompt(string channel) => channel == "official";

    public static List<(string Value, string Label)> ValidResolutions(string channel, string model, string mode)
    {
        if (mode == "extend")
            return (channel == "google" || channel == "yunwu")
                ? [("720p", "720p")]
                : [("720p", "720p"), ("1080p", "1080p")];
        if ((channel is "official" or "google" or "yunwu") && model == "lite")
            return [("720p", "720p"), ("1080p", "1080p")];
        return [("720p", "720p"), ("1080p", "1080p"), ("4k", "4K")];
    }

    public static string ChannelDisplayName(string channel) => channel switch
    {
        "budget" => "低价", "official" => "RH 官方",
        "google" => "Google 官方", "yunwu" => "云雾API中转",
        _ => channel
    };
}
