using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Island.Modules.Clock;

/// <summary>
/// 「時間」分頁的自訂背景圖。
/// - 選的圖片複製一份到 modules\clock\background.*(原檔搬走或刪掉都沒關係)。
/// - 讀進來時縮到面板大小(寬 900 px,約 1~2 MB 記憶體);要模糊的話在讀取時算一次,之後就是一張普通的圖。
/// - 不支援動態圖(GIF 只取第一格),避免一直耗 CPU。
/// </summary>
internal static class ClockBackground
{
    public const int DecodeWidth = 900;
    public static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>把使用者選的圖複製進資料夾,刪掉舊的;回傳新的檔名(只有檔名)。</summary>
    public static string Import(string source, string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.EnumerateFiles(dir, "background.*")) TryDelete(old);
        var name = "background" + Path.GetExtension(source).ToLowerInvariant();
        File.Copy(source, Path.Combine(dir, name), overwrite: true);
        return name;
    }

    public static void Remove(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var old in Directory.EnumerateFiles(dir, "background.*")) TryDelete(old);
    }

    /// <summary>讀圖(縮小、不鎖檔);blur = 先算好模糊。失敗回傳 null。</summary>
    public static ImageSource? Load(string path, bool blur)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // 讀完就關檔
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.DecodePixelWidth = DecodeWidth;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            return blur ? Blur(bmp) : bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>一次性的模糊:把圖畫進離屏點陣圖。邊緣多畫一圈避免模糊後變透明。</summary>
    private static ImageSource Blur(BitmapSource src)
    {
        int w = src.PixelWidth, h = src.PixelHeight;
        const int Radius = 14;
        var image = new Image
        {
            Source = src,
            Stretch = Stretch.Fill,
            Width = w + Radius * 4,
            Height = h + Radius * 4,
            Effect = new BlurEffect { Radius = Radius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance },
        };
        var host = new Canvas { Width = w, Height = h, ClipToBounds = true };
        Canvas.SetLeft(image, -Radius * 2);
        Canvas.SetTop(image, -Radius * 2);
        host.Children.Add(image);
        host.Measure(new Size(w, h));
        host.Arrange(new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(host);
        rtb.Freeze();
        return rtb;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
