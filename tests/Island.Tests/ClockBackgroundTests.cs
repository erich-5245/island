using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Island.Modules.Clock;

namespace Island.Tests;

public sealed class ClockBackgroundTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("island-clock-").FullName;

    private string MakePng(string name, int w = 1600, int h = 900)
    {
        var pixels = new byte[w * h * 4];
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 200; pixels[i + 1] = 120; pixels[i + 2] = 40; pixels[i + 3] = 255; }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        var path = Path.Combine(_dir, name);
        using var f = File.Create(path);
        enc.Save(f);
        return path;
    }

    [Theory]
    [InlineData("a.JPG", true)]
    [InlineData("a.png", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.txt", false)]
    [InlineData("a", false)]
    public void Supported(string name, bool ok) => Assert.Equal(ok, ClockBackground.IsSupported(name));

    [Fact]
    public void Import_ReplacesOld_AndLoadIsScaledAndUnlocked()
    {
        var store = Path.Combine(_dir, "store");
        var first = ClockBackground.Import(MakePng("one.png"), store);
        Assert.Equal("background.png", first);
        File.WriteAllText(Path.Combine(store, "background.jpg"), "old");   // 舊的另一種副檔名
        var second = ClockBackground.Import(MakePng("two.png"), store);
        Assert.Equal(new[] { "background.png" }, Directory.GetFiles(store).Select(Path.GetFileName));

        var img = (BitmapSource)ClockBackground.Load(Path.Combine(store, second), blur: false)!;
        Assert.Equal(ClockBackground.DecodeWidth, img.PixelWidth);         // 縮小到面板大小
        File.Delete(Path.Combine(store, second));                          // 讀完不鎖檔
        ClockBackground.Remove(store);
        Assert.Empty(Directory.GetFiles(store));
    }

    [Fact]
    public void Load_BadFiles_ReturnNull()
    {
        Assert.Null(ClockBackground.Load(Path.Combine(_dir, "missing.png"), false));
        var fake = Path.Combine(_dir, "fake.png");
        File.WriteAllText(fake, "not an image");
        Assert.Null(ClockBackground.Load(fake, false));
    }

    [Fact]
    public void Blur_KeepsSize()
    {
        var path = MakePng("blur.png");
        ImageSource? result = null;
        var t = new Thread(() => result = ClockBackground.Load(path, blur: true));
        t.SetApartmentState(ApartmentState.STA);   // 模糊要用到 WPF 元件,需要 STA
        t.Start();
        t.Join();
        var bmp = Assert.IsAssignableFrom<BitmapSource>(result);
        Assert.Equal(ClockBackground.DecodeWidth, bmp.PixelWidth);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }
}
