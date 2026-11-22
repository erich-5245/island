using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island.Modules.Media;

/// <summary>
/// 從專輯封面取出「代表色」:把畫面縮到 24×24,依色相分 24 格,
/// 每個像素以「飽和度 × 不太暗也不太亮」加權投票,取得分最高那格的平均色。
/// 黑白或灰階封面回傳 null(外殼會使用中性色)。只在換歌時算一次,成本很低。
/// </summary>
internal static class AlbumColor
{
    private const int Sample = 24;
    private const int Buckets = 24;

    public static Color? Extract(BitmapSource? image)
    {
        if (image is null) return null;
        try
        {
            var scaled = new TransformedBitmap(image,
                new ScaleTransform((double)Sample / image.PixelWidth, (double)Sample / image.PixelHeight));
            var bgra = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int w = bgra.PixelWidth, h = bgra.PixelHeight;
            var pixels = new byte[w * h * 4];
            bgra.CopyPixels(pixels, w * 4, 0);

            var score = new double[Buckets];
            var sumR = new double[Buckets];
            var sumG = new double[Buckets];
            var sumB = new double[Buckets];

            for (int i = 0; i < pixels.Length; i += 4)
            {
                double b = pixels[i] / 255.0, g = pixels[i + 1] / 255.0, r = pixels[i + 2] / 255.0;
                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                double l = (max + min) / 2, d = max - min;
                if (d < 0.08 || l < 0.12 || l > 0.92) continue;       // 灰、太暗、太亮的不投票
                double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                double hue = max == r ? (g - b) / d + (g < b ? 6 : 0)
                           : max == g ? (b - r) / d + 2
                           : (r - g) / d + 4;
                int bucket = (int)(hue / 6 * Buckets) % Buckets;
                double weight = s * (1 - Math.Abs(l - 0.5) * 1.4);
                if (weight <= 0) continue;
                score[bucket] += weight;
                sumR[bucket] += r * weight;
                sumG[bucket] += g * weight;
                sumB[bucket] += b * weight;
            }

            int best = 0;
            for (int i = 1; i < Buckets; i++) if (score[i] > score[best]) best = i;
            // 有顏色的像素太少(例如黑白封面加一點點彩色字)→ 視為中性
            if (score[best] < w * h * 0.04) return null;

            double k = 255 / score[best];
            return Color.FromRgb((byte)(sumR[best] * k), (byte)(sumG[best] * k), (byte)(sumB[best] * k));
        }
        catch
        {
            return null;
        }
    }
}
