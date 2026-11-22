using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Island.App.Infrastructure;

/// <summary>%LOCALAPPDATA%\Island\settings.json 的內容。</summary>
internal sealed class IslandSettings
{
    /// <summary>
    /// 要顯示在哪個螢幕:
    /// "primary" = 跟著 Windows 主螢幕(預設);
    /// 或填裝置名稱,例如 "\\\\.\\DISPLAY2"(可用的名稱會寫在 island.log)。
    /// 指定的螢幕不存在時(例如拔掉了),自動退回主螢幕。
    /// </summary>
    public string Display { get; set; } = "primary";

    /// <summary>膠囊上緣離螢幕頂端的距離(DIP,100% 縮放下的像素)。</summary>
    public double TopOffset { get; set; } = 8;

    /// <summary>模組排列順序。沒列出但有找到的模組會排在後面。</summary>
    public List<string> Modules { get; set; } = ["clock", "shelf"];

    /// <summary>要停用的模組 Id。</summary>
    public List<string> DisabledModules { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static IslandSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<IslandSettings>(json, JsonOptions);
                if (settings is not null) return settings;
            }
        }
        catch (Exception ex)
        {
            Log.Write($"settings.json 讀取失敗,使用預設值:{ex.Message}");
            return new IslandSettings();
        }

        var defaults = new IslandSettings();
        defaults.Save();
        return defaults;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"settings.json 寫入失敗:{ex.Message}");
        }
    }
}
