using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;

namespace Island.App.Infrastructure;

/// <summary>
/// 全域強調色。所有主題筆刷放在 Application.Resources(<see cref="IslandTheme"/> 的鍵),
/// 用到的地方都是 DynamicResource,換掉資源就自動更新,不必逐一通知。
/// 放進資源字典的筆刷會被 WPF 凍結、不能做顏色動畫,所以漸變改成:
/// 換色時用計時器在 450 ms 內分 12 步,每一步放一支新的(凍結的)筆刷。只在換歌時跑,平常零成本。
/// 沒有強調色時是中性的黑白灰;有強調色(例如專輯封面主色)時,依同一個色相推出整組深淺。
/// </summary>
internal static class AccentTheme
{
    private const int Steps = 12;
    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(450.0 / Steps);

    private static ResourceDictionary? _resources;
    private static readonly Dictionary<string, Color> Shown = [];   // 目前畫面上的顏色
    private static Dictionary<string, Color> _from = [], _to = [];
    private static DispatcherTimer? _timer;
    private static int _step;
    private static Color? _current;

    /// <summary>在建立任何視窗之前呼叫,把中性色的筆刷放進應用程式資源。</summary>
    public static void Install(ResourceDictionary resources)
    {
        _resources = resources;
        foreach (var (key, color) in Palette(null)) Put(key, color);
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = StepInterval };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>設定強調色;null = 回到中性。色彩太淡(接近灰)時也視為中性。</summary>
    public static void Set(Color? color)
    {
        if (color is { } c && Saturation(c) < 0.18) color = null;
        if (Nullable.Equals(color, _current)) return;
        _current = color;

        _from = new Dictionary<string, Color>(Shown);
        _to = Palette(color).ToDictionary(p => p.Key, p => p.Color);
        _step = 0;
        _timer?.Start();
        Log.Write(color is null ? "強調色:中性" : $"強調色:{color}");
    }

    private static void Tick()
    {
        _step++;
        double t = (double)_step / Steps;
        t = t * (2 - t);   // ease-out
        foreach (var (key, to) in _to)
        {
            var from = _from.TryGetValue(key, out var f) ? f : to;
            Put(key, Lerp(from, to, t));
        }
        if (_step >= Steps) _timer!.Stop();
    }

    private static void Put(string key, Color color)
    {
        if (Shown.TryGetValue(key, out var old) && old == color) return;
        Shown[key] = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _resources![key] = brush;
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>由一個基準色推出整組顏色(HSL 固定亮度,只取色相與部分飽和度)。</summary>
    private static IEnumerable<(string Key, Color Color)> Palette(Color? accent)
    {
        if (accent is not { } a)
        {
            yield return (IslandTheme.Accent, Rgb(0xFF, 0xFF, 0xFF));
            yield return (IslandTheme.AccentSoft, Rgb(0xFF, 0xFF, 0xFF));
            yield return (IslandTheme.AccentDim, Rgb(0x26, 0x26, 0x26));
            yield return (IslandTheme.AccentTrack, Rgb(0x33, 0x33, 0x33));
            yield return (IslandTheme.AccentMuted, Rgb(0x9A, 0x9A, 0x9A));
            yield return (IslandTheme.PanelBackground, Rgb(0x0A, 0x0A, 0x0A));
            yield return (IslandTheme.PanelBorder, Rgb(0x1A, 0x1A, 0x1A));
            yield return (IslandTheme.CapsuleBorder, Rgb(0x0A, 0x0A, 0x0A));
            yield return (IslandTheme.OnAccent, Rgb(0x0A, 0x0A, 0x0A));
            yield break;
        }

        var (h, s, _) = ToHsl(a);
        s = Math.Clamp(s, 0.45, 0.80);
        yield return (IslandTheme.Accent, FromHsl(h, s * 0.95, 0.67));
        yield return (IslandTheme.AccentSoft, FromHsl(h, s * 0.85, 0.84));
        yield return (IslandTheme.AccentDim, FromHsl(h, s * 0.45, 0.17));
        yield return (IslandTheme.AccentTrack, FromHsl(h, s * 0.35, 0.14));
        yield return (IslandTheme.AccentMuted, FromHsl(h, s * 0.18, 0.55));
        yield return (IslandTheme.PanelBackground, FromHsl(h, s * 0.40, 0.055));
        yield return (IslandTheme.PanelBorder, FromHsl(h, s * 0.40, 0.17));
        yield return (IslandTheme.CapsuleBorder, FromHsl(h, s * 0.40, 0.26));
        yield return (IslandTheme.OnAccent, Rgb(0xFF, 0xFF, 0xFF));
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static double Saturation(Color c) => ToHsl(c).S;

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d < 1e-6) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0)
                 : max == g ? (b - r) / d + 2
                 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = l - c / 2;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
