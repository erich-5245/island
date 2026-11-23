using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Island.App.Infrastructure;

/// <summary>強調色從哪裡來。</summary>
internal enum AccentSource
{
    /// <summary>跟著正在播放的專輯封面(沒有播放時是黑白)。</summary>
    Album,
    /// <summary>跟著 Windows 的主題色(設定 → 個人化 → 色彩)。</summary>
    Windows,
    /// <summary>固定一個顏色。</summary>
    Fixed,
    /// <summary>依時段變化:清晨橘、白天藍、傍晚紫、深夜靛。</summary>
    TimeOfDay,
    /// <summary>不使用強調色(黑白灰)。</summary>
    None,
}

internal enum AnimationSpeed { Fast, Normal, Slow }

/// <summary>
/// 外觀主題:強調色、膠囊底色、外型、字型、動畫速度。存在 settings.json 的 "theme";
/// 也可以匯出成單獨的 .json 分享給別人。純資料 + 純函式,方便測試。
/// </summary>
internal sealed class ThemeSettings
{
    public string Name { get; set; } = "經典黑";
    public AccentSource AccentSource { get; set; } = AccentSource.Album;
    /// <summary>AccentSource = Fixed 時用的顏色(#RRGGBB)。</summary>
    public string AccentColor { get; set; } = "#5AC8FA";
    /// <summary>膠囊底色(#RRGGBB)。</summary>
    public string CapsuleColor { get; set; } = "#0A0A0A";
    /// <summary>膠囊不透明度 0.6–1。</summary>
    public double CapsuleOpacity { get; set; } = 1.0;
    /// <summary>圓角程度 0–1:1 = 膠囊(完全圓),0 = 小圓角的方形。</summary>
    public double Roundness { get; set; } = 1.0;
    public bool ShowBorder { get; set; } = true;
    /// <summary>字型名稱;空字串 = 系統預設。</summary>
    public string FontFamily { get; set; } = "";
    public AnimationSpeed AnimationSpeed { get; set; } = AnimationSpeed.Normal;

    public const double MinOpacity = 0.6;

    public ThemeSettings Clone() => (ThemeSettings)MemberwiseClone();

    /// <summary>修正不合理的值(手動改 JSON 或匯入別人的主題時)。</summary>
    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "我的主題" : Name.Trim();
        if (Name.Length > 30) Name = Name[..30];
        if (!Enum.IsDefined(AccentSource)) AccentSource = AccentSource.Album;
        if (!Enum.IsDefined(AnimationSpeed)) AnimationSpeed = AnimationSpeed.Normal;
        AccentColor = TryParseColor(AccentColor, out var a) ? ToHex(a) : "#5AC8FA";
        CapsuleColor = TryParseColor(CapsuleColor, out var c) ? ToHex(c) : "#0A0A0A";
        CapsuleOpacity = double.IsFinite(CapsuleOpacity) ? Math.Clamp(CapsuleOpacity, MinOpacity, 1) : 1;
        Roundness = double.IsFinite(Roundness) ? Math.Clamp(Roundness, 0, 1) : 1;
        FontFamily = (FontFamily ?? "").Trim();
    }

    /// <summary>外觀是否相同(不比名稱),用來標示目前套用的是哪個預設主題。</summary>
    public bool LooksLike(ThemeSettings o) =>
        AccentSource == o.AccentSource &&
        (AccentSource != AccentSource.Fixed || string.Equals(AccentColor, o.AccentColor, StringComparison.OrdinalIgnoreCase)) &&
        string.Equals(CapsuleColor, o.CapsuleColor, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(CapsuleOpacity - o.CapsuleOpacity) < 0.01 &&
        Math.Abs(Roundness - o.Roundness) < 0.01 &&
        ShowBorder == o.ShowBorder &&
        string.Equals(FontFamily, o.FontFamily, StringComparison.OrdinalIgnoreCase) &&
        AnimationSpeed == o.AnimationSpeed;

    /// <summary>動畫時間的倍率。</summary>
    public double SpeedFactor => AnimationSpeed switch
    {
        AnimationSpeed.Fast => 0.6,
        AnimationSpeed.Slow => 1.6,
        _ => 1.0,
    };

    /// <summary>收合 / 預覽時膠囊的圓角(高度 h)。</summary>
    public double CollapsedRadius(double h) => 6 + (h / 2 - 6) * Roundness;

    /// <summary>展開時的圓角。</summary>
    public double ExpandedRadius => 8 + 16 * Roundness;

    // ── 預設主題 ─────────────────────────────────────────

    public static IReadOnlyList<ThemeSettings> Presets { get; } =
    [
        new() { Name = "經典黑", AccentSource = AccentSource.Album, CapsuleColor = "#0A0A0A" },
        new() { Name = "深藍金", AccentSource = AccentSource.Fixed, AccentColor = "#E8B84A", CapsuleColor = "#0B1B36" },
        new() { Name = "霓虹", AccentSource = AccentSource.Fixed, AccentColor = "#FF2D95", CapsuleColor = "#0B0614" },
        new() { Name = "森林", AccentSource = AccentSource.Fixed, AccentColor = "#5BD38A", CapsuleColor = "#0B1A12" },
        new() { Name = "日夜", AccentSource = AccentSource.TimeOfDay, CapsuleColor = "#0A0A0A" },
        new() { Name = "石墨", AccentSource = AccentSource.Windows, CapsuleColor = "#2C2C2E", Roundness = 0.55 },
        new() { Name = "玻璃", AccentSource = AccentSource.Album, CapsuleColor = "#1C1C1E", CapsuleOpacity = 0.78 },
        new() { Name = "極簡", AccentSource = AccentSource.None, CapsuleColor = "#000000", ShowBorder = false, AnimationSpeed = AnimationSpeed.Fast },
    ];

    /// <summary>色盤:設定頁的固定強調色選項。</summary>
    public static IReadOnlyList<string> AccentSwatches { get; } =
        ["#FF453A", "#FF9F0A", "#FFD60A", "#E8B84A", "#30D158", "#5BD38A", "#64D2FF", "#5AC8FA", "#0A84FF", "#5E5CE6", "#BF5AF2", "#FF2D95"];

    /// <summary>色盤:膠囊底色選項(都夠暗,白色文字看得清楚)。</summary>
    public static IReadOnlyList<string> CapsuleSwatches { get; } =
        ["#000000", "#0A0A0A", "#1C1C1E", "#2C2C2E", "#0B1B36", "#101A2E", "#0B1A12", "#1A0F0A", "#1A0B1A", "#0B0614"];

    // ── 顏色工具 ─────────────────────────────────────────

    /// <summary>接受 #RGB、#RRGGBB、RRGGBB(前後空白、大小寫都可以)。</summary>
    public static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s.Select(ch => $"{ch}{ch}"));
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>依時段的強調色(色相漸變,不是突然跳色)。</summary>
    public static Color TimeOfDayAccent(DateTime now)
    {
        // 錨點:時刻 → 顏色
        (double Hour, Color Color)[] stops =
        [
            (0, Color.FromRgb(0x5E, 0x5C, 0xE6)),    // 深夜 靛
            (6, Color.FromRgb(0xFF, 0x9F, 0x0A)),    // 清晨 橘
            (10, Color.FromRgb(0x5A, 0xC8, 0xFA)),   // 白天 藍
            (16, Color.FromRgb(0x64, 0xD2, 0xFF)),   // 下午 淺藍
            (19, Color.FromRgb(0xBF, 0x5A, 0xF2)),   // 傍晚 紫
            (24, Color.FromRgb(0x5E, 0x5C, 0xE6)),
        ];
        double h = now.TimeOfDay.TotalHours;
        for (int i = 1; i < stops.Length; i++)
        {
            if (h > stops[i].Hour) continue;
            var (h0, c0) = stops[i - 1];
            var (h1, c1) = stops[i];
            double t = (h - h0) / (h1 - h0);
            return Color.FromRgb(
                (byte)Math.Round(c0.R + (c1.R - c0.R) * t),
                (byte)Math.Round(c0.G + (c1.G - c0.G) * t),
                (byte)Math.Round(c0.B + (c1.B - c0.B) * t));
        }
        return stops[0].Color;
    }

    // ── 匯出 / 匯入 ─────────────────────────────────────

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>讀別人的主題檔;格式不對回傳 null。讀到的值一律修正到合理範圍。</summary>
    public static ThemeSettings? FromJson(string json)
    {
        try
        {
            var t = JsonSerializer.Deserialize<ThemeSettings>(json, JsonOptions);
            if (t is null) return null;
            t.Normalize();
            return t;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
