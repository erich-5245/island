using System.IO;
using System.Windows;
using System.Windows.Media;
using Island.Modules.Clock;

namespace Island.Tests;

/// <summary>時鐘樣式:顏色解析、深淺、從背景取色、設定檔防呆。</summary>
public sealed class ClockStyleTests
{
    [Fact]
    public void Resolve_HandlesWhiteHexAndBackground()
    {
        Assert.Equal(Colors.White, ClockStyle.Resolve("white", null, 0));
        Assert.Equal(Color.FromRgb(0xFF, 0x9F, 0x0A), ClockStyle.Resolve("#FF9F0A", null, 0));
        Assert.Equal(Color.FromRgb(1, 2, 3), ClockStyle.Resolve("background", Color.FromRgb(1, 2, 3), 0));
        Assert.Equal(Colors.White, ClockStyle.Resolve("background", null, 0));   // 沒有背景圖 → 白色
        Assert.Equal(Colors.White, ClockStyle.Resolve("#12", null, 0));          // 壞掉的值 → 白色
    }

    [Fact]
    public void Adjust_LightensAndDarkensWithinLimits()
    {
        var c = Color.FromRgb(100, 100, 100);
        Assert.Equal(Colors.White, ClockStyle.Adjust(c, 1));
        Assert.Equal(c, ClockStyle.Adjust(c, 0));
        var darkest = ClockStyle.Adjust(c, -1);
        Assert.Equal(30, darkest.R);                       // 最多往黑 70%,不會整個變黑
        Assert.Equal(c, ClockStyle.Adjust(c, double.NaN));
    }

    [Fact]
    public void Readable_SampleIsBrightEnough()
    {
        // 深藍的圖 → 取出來的顏色要夠亮,在暗色遮罩上看得清楚
        var px = new byte[4 * 100];
        for (int i = 0; i < px.Length; i += 4) { px[i] = 120; px[i + 1] = 30; px[i + 2] = 10; px[i + 3] = 255; }
        var c = ClockStyle.Readable(ClockStyle.Dominant(px));
        Assert.True(c.B > c.R && c.B > 150, c.ToString());
        // 灰階圖 → 淡灰白
        var gray = ClockStyle.Readable(Color.FromRgb(60, 60, 60));
        Assert.Equal(gray.R, gray.B);
        Assert.True(gray.R > 200);
    }

    [Fact]
    public void Weight_BlackIsAlwaysHeaviest()
    {
        Assert.Equal(FontWeights.Black, ClockStyle.Weight(ClockFont.Black, 2));
        Assert.Equal(FontWeight.FromOpenTypeWeight(300), ClockStyle.Weight(ClockFont.System, 3));
        Assert.Equal(FontWeight.FromOpenTypeWeight(900), ClockStyle.Weight(ClockFont.Mono, 42));
    }

    [Fact]
    public void Options_LoadClampsBadValues()
    {
        var dir = Directory.CreateTempSubdirectory("island-clockstyle-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "options.json"),
                """{ "font": "Serif", "weight": 99, "lightness": 5, "color": "javascript:", "look": "Glass", "applyToCapsule": true }""");
            var o = ClockOptions.Load(dir);
            Assert.Equal(ClockFont.Serif, o.Font);
            Assert.Equal(9, o.Weight);
            Assert.Equal(1, o.Lightness);
            Assert.Equal(ClockStyle.White, o.Color);
            Assert.Equal(ClockLook.Glass, o.Look);
            Assert.True(o.ApplyToCapsule);

            o.Color = "#30D158";
            o.Save(dir);
            Assert.Equal("#30D158", ClockOptions.Load(dir).Color);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void GlassBrush_IsGradient_SolidIsPlain()
    {
        Assert.IsType<LinearGradientBrush>(ClockStyle.TextBrush(Colors.Orange, ClockLook.Glass));
        Assert.IsType<SolidColorBrush>(ClockStyle.TextBrush(Colors.Orange, ClockLook.Solid));
    }
}

/// <summary>選色視窗的 HSV / 色碼換算。</summary>
public sealed class ColorPickerMathTests
{
    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#30D158")]
    [InlineData("#0A84FF")]
    [InlineData("#808080")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void HsvRoundTrip(string hex)
    {
        Assert.True(Island.Core.IslandColorPicker.TryParseHex(hex, out var c));
        var (h, s, v) = Island.Core.IslandColorPicker.ToHsv(c);
        Assert.Equal(hex, Island.Core.IslandColorPicker.ToHex(Island.Core.IslandColorPicker.FromHsv(h, s, v)));
    }

    [Theory]
    [InlineData("fc0", "#FFCC00")]
    [InlineData(" #12ab9F ", "#12AB9F")]
    public void ParsesShortAndLoose(string input, string expected)
    {
        Assert.True(Island.Core.IslandColorPicker.TryParseHex(input, out var c));
        Assert.Equal(expected, Island.Core.IslandColorPicker.ToHex(c));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("zzzzzz")]
    public void RejectsBad(string input) => Assert.False(Island.Core.IslandColorPicker.TryParseHex(input, out _));
}
