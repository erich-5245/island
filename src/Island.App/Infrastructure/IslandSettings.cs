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

    // ── 尺寸(DIP,100% 縮放下的像素) ─────────────────────
    /// <summary>平常(收合)的膠囊:只顯示時間與小提示。</summary>
    public double CollapsedWidth { get; set; } = 200;
    public double CollapsedHeight { get; set; } = 36;
    /// <summary>滑鼠移過去時變寬成一排字(高度不變)。</summary>
    public double PeekWidth { get; set; } = 320;
    /// <summary>點擊後展開的大小。</summary>
    public double ExpandedWidth { get; set; } = 380;
    public double ExpandedHeight { get; set; } = 170;

    /// <summary>
    /// true = 平常完全隱藏,滑鼠推到螢幕上緣中間(熱區)才出現;
    /// false = 平常顯示只有時間的小膠囊。右鍵選單可切換。
    /// </summary>
    public bool HideWhenIdle { get; set; } = true;

    /// <summary>
    /// true = 換歌時膠囊短暫出現(約 3 秒)顯示封面 + 歌名,之後再隱藏。
    /// 右鍵選單「切歌時顯示」可切換。
    /// </summary>
    public bool AnnounceActivity { get; set; } = true;

    /// <summary>調音量 / 亮度時膠囊出現約 1.5 秒顯示長條。右鍵選單「音量亮度提示」可切換。</summary>
    public bool ShowOsd { get; set; } = true;

    /// <summary>島所在的螢幕有全螢幕程式(影片、遊戲、簡報)時自動隱藏。右鍵選單可切換。</summary>
    public bool HideInFullscreen { get; set; } = true;

    /// <summary>
    /// true = 使用軟體算圖(不建立 Direct3D 裝置,記憶體較省;膠囊一分鐘才重畫一次,影響可忽略)。
    /// 之後若展開動畫不夠順,可改成 false 使用 GPU。
    /// </summary>
    public bool SoftwareRendering { get; set; } = true;

    /// <summary>
    /// 滑鼠移過去時「即時狀態」的優先順序:第一個有活動的放中間長條,
    /// 其餘有東西可看的(最多兩個)放左右小圓。沒列出的模組排在後面。
    /// </summary>
    public List<string> LiveOrder { get; set; } = ["media", "timer", "privacy", "downloads", "battery", "weather", "shelf", "links"];

    /// <summary>不要放進小圓的模組 Id(例如 ["shelf"])。</summary>
    public List<string> LiveHidden { get; set; } = [];

    /// <summary>模組排列順序。沒列出但有找到的模組會排在後面。</summary>
    public List<string> Modules { get; set; } = ["clock", "weather", "timer", "media", "shelf", "links", "osd", "battery"];

    /// <summary>要停用的模組 Id。</summary>
    public List<string> DisabledModules { get; set; } = [];

    /// <summary>點小圓的動作:模組 Id → "tab"(打開分頁)/ "action"(直接執行)。沒有的照模組預設。</summary>
    public Dictionary<string, string> CircleActions { get; set; } = [];

    /// <summary>Ctrl+Alt+1…9 直接切到第 1…9 個捷徑。</summary>
    public bool NumberHotkeys { get; set; } = true;

    /// <summary>外觀主題(強調色、膠囊底色、外型、字型、動畫速度)。</summary>
    public ThemeSettings Theme { get; set; } = new();

    /// <summary>使用者自己存的主題(設定 → 主題 →「存成我的主題」)。</summary>
    public List<ThemeSettings> CustomThemes { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // 讓 "Ctrl+Alt+I" 這類字串照原樣寫入,方便手動修改(預設會把 + 寫成 +)
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IslandSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<IslandSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    settings.Normalize();
                    settings.Save();   // 把新版本新增的欄位補進檔案,方便手動修改
                    return settings;
                }
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

    /// <summary>修正不合理的尺寸(太小、預覽比收合窄…)。</summary>
    /// <summary>叫出 / 收起島的全域快捷鍵,例如 "Ctrl+Alt+I";空字串 = 停用。</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+I";

    internal void Normalize()
    {
        CollapsedWidth = Math.Clamp(CollapsedWidth, 80, 600);
        CollapsedHeight = Math.Clamp(CollapsedHeight, 20, 80);
        PeekWidth = Math.Clamp(PeekWidth, CollapsedWidth, 900);
        ExpandedWidth = Math.Clamp(ExpandedWidth, CollapsedWidth, 1200);
        ExpandedHeight = Math.Clamp(ExpandedHeight, CollapsedHeight, 800);
        CircleActions ??= [];
        Theme ??= new ThemeSettings();
        Theme.Normalize();
        CustomThemes ??= [];
        CustomThemes.RemoveAll(t => t is null);
        foreach (var t in CustomThemes) t.Normalize();
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
