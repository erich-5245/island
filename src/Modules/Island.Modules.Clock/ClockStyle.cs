using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island.Modules.Clock;

/// <summary>時間數字的字體(像 iPhone 鎖定畫面可以換)。</summary>
public enum ClockFont { System, Condensed, Black, Serif, Mono }

/// <summary>實線 = 單色;玻璃 = 上亮下透的漸層,像毛玻璃上的字。</summary>
public enum ClockLook { Solid, Glass }

/// <summary>
/// 時鐘樣式的純計算:字體、粗細、顏色(含從背景圖取色、深淺)、玻璃漸層。
/// 不碰檔案、不存狀態,方便測試;結果都是凍結的筆刷,畫面一分鐘才更新一次文字,不會多耗資源。
/// </summary>
public static class ClockStyle
{
    public const string White = "white";
    public const string FromBackground = "background";

    public static readonly (ClockFont Font, string Label)[] Fonts =
    [
        (ClockFont.System, "圓潤"),
        (ClockFont.Condensed, "窄體"),
        (ClockFont.Black, "粗黑"),
        (ClockFont.Serif, "襯線"),
        (ClockFont.Mono, "等寬"),
    ];

    /// <summary>色盤(白、黃、橘、粉、紫、藍、綠),接近 iOS 的系統色。</summary>
    public static readonly string[] Palette = ["#FFFFFF", "#FFD60A", "#FF9F0A", "#FF6482", "#BF5AF2", "#64D2FF", "#30D158"];

    public static FontFamily Family(ClockFont font) => font switch
    {
        ClockFont.Condensed => new FontFamily("Bahnschrift"),
        ClockFont.Black => new FontFamily("Segoe UI Black, Segoe UI"),
        ClockFont.Serif => new FontFamily("Cambria, Georgia"),
        ClockFont.Mono => new FontFamily("Cascadia Mono, Consolas"),
        _ => new FontFamily("Segoe UI Variable Display, Segoe UI"),
    };

    public static FontStretch Stretch(ClockFont font) => font == ClockFont.Condensed ? FontStretches.Condensed : FontStretches.Normal;

    /// <summary>窄體要大一點才跟其他字體看起來一樣份量。</summary>
    public static double SizeFactor(ClockFont font) => font switch
    {
        ClockFont.Condensed => 1.18,
        ClockFont.Mono => 0.94,
        _ => 1.0,
    };

    /// <summary>粗細 1–9(= 100–900);粗黑體固定最粗。</summary>
    public static FontWeight Weight(ClockFont font, int weight) =>
        font == ClockFont.Black ? FontWeights.Black : FontWeight.FromOpenTypeWeight(Math.Clamp(weight, 1, 9) * 100);

    public static bool TryParse(string? text, out Color color)
    {
        color = Colors.White;
        if (text is not { Length: 7 } || text[0] != '#') return false;
        if (!uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        color = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>設定的顏色("white"、"background"、"#RRGGBB")+ 深淺 → 實際顏色。</summary>
    public static Color Resolve(string? setting, Color? sampled, double lightness)
    {
        var c = setting == FromBackground ? sampled ?? Colors.White
              : TryParse(setting, out var parsed) ? parsed
              : Colors.White;
        return Adjust(c, lightness);
    }

    /// <summary>深淺:-1(往黑)… 0 … 1(往白)。往黑最多到 70%,字才不會消失在暗色背景裡。</summary>
    public static Color Adjust(Color c, double lightness)
    {
        lightness = double.IsFinite(lightness) ? Math.Clamp(lightness, -1, 1) : 0;
        if (lightness >= 0) return Lerp(c, Colors.White, lightness);
        return Lerp(c, Colors.Black, -lightness * 0.7);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>文字筆刷:實線 = 單色;玻璃 = 上面亮、下面半透明的漸層。</summary>
    public static Brush TextBrush(Color c, ClockLook look)
    {
        Brush brush = look == ClockLook.Glass
            ? new LinearGradientBrush(
                Color.FromArgb(0xF2, Lerp(c, Colors.White, 0.45).R, Lerp(c, Colors.White, 0.45).G, Lerp(c, Colors.White, 0.45).B),
                Color.FromArgb(0x8C, c.R, c.G, c.B),
                90)
            : new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    // ── 從背景圖取色 ──

    /// <summary>背景圖的代表色(已調成在暗色遮罩上看得清楚的亮度);讀不到回傳 null。</summary>
    public static Color? Sample(ImageSource? image)
    {
        if (image is not BitmapSource source) return null;
        try
        {
            var bmp = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            var px = new byte[w * h * 4];
            bmp.CopyPixels(px, w * 4, 0);
            return Readable(Dominant(px, step: 3));
        }
        catch { return null; }
    }

    /// <summary>不透明像素的加權平均,越鮮豔的像素權重越高(避免整張圖平均成灰色)。step = 每隔幾個像素取一次。</summary>
    public static Color Dominant(byte[] bgra, int step = 1)
    {
        double r = 0, g = 0, b = 0, weight = 0;
        int stride = 4 * Math.Max(1, step);
        for (int i = 0; i + 3 < bgra.Length; i += stride)
        {
            if (bgra[i + 3] < 128) continue;
            int max = Math.Max(bgra[i], Math.Max(bgra[i + 1], bgra[i + 2]));
            int min = Math.Min(bgra[i], Math.Min(bgra[i + 1], bgra[i + 2]));
            double sat = max == 0 ? 0 : (max - min) / (double)max;
            double wgt = 0.05 + sat * sat;
            b += bgra[i] * wgt; g += bgra[i + 1] * wgt; r += bgra[i + 2] * wgt;
            weight += wgt;
        }
        if (weight == 0) return Colors.White;
        return Color.FromRgb((byte)Math.Round(r / weight), (byte)Math.Round(g / weight), (byte)Math.Round(b / weight));
    }

    /// <summary>調成適合當字的顏色:亮度至少 65%,飽和度拉高一點;幾乎沒有顏色的圖就用淡灰白。</summary>
    public static Color Readable(Color c)
    {
        var (h, s, l) = ToHsl(c);
        if (s < 0.08) return FromHsl(h, 0, Math.Max(l, 0.88));
        return FromHsl(h, Math.Min(1, s * 1.25), Math.Clamp(l, 0.65, 0.82));
    }

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d < 1e-9) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s <= 0)
        {
            byte v = (byte)Math.Round(l * 255);
            return Color.FromRgb(v, v, v);
        }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1 / 2.0) return q;
            if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6;
            return p;
        }
        return Color.FromRgb(
            (byte)Math.Round(Hue(p, q, h + 1 / 3.0) * 255),
            (byte)Math.Round(Hue(p, q, h) * 255),
            (byte)Math.Round(Hue(p, q, h - 1 / 3.0) * 255));
    }
}
