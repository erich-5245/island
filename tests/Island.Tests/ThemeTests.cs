using System.Windows.Media;
using Island.App.Infrastructure;

namespace Island.Tests;

public class ThemeTests
{
    [Theory]
    [InlineData("#FF2D95", 0xFF, 0x2D, 0x95)]
    [InlineData("ff2d95", 0xFF, 0x2D, 0x95)]
    [InlineData("  #0a84ff ", 0x0A, 0x84, 0xFF)]
    [InlineData("#F80", 0xFF, 0x88, 0x00)]
    public void ParsesColors(string text, byte r, byte g, byte b)
    {
        Assert.True(ThemeSettings.TryParseColor(text, out var c));
        Assert.Equal(Color.FromRgb(r, g, b), c);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    [InlineData("#FF2D95FF")]
    public void RejectsBadColors(string? text) => Assert.False(ThemeSettings.TryParseColor(text, out _));

    [Fact]
    public void Hex_RoundTrips() => Assert.Equal("#0A84FF", ThemeSettings.ToHex(Color.FromRgb(10, 132, 255)));

    [Fact]
    public void Normalize_FixesEverything()
    {
        var t = new ThemeSettings
        {
            Name = "  ",
            AccentSource = (AccentSource)99,
            AccentColor = "nope",
            CapsuleColor = "#abc",
            CapsuleOpacity = 0.1,
            Roundness = 7,
            FontFamily = null!,
            AnimationSpeed = (AnimationSpeed)(-1),
        };
        t.Normalize();
        Assert.Equal("我的主題", t.Name);
        Assert.Equal(AccentSource.Album, t.AccentSource);
        Assert.Equal("#5AC8FA", t.AccentColor);
        Assert.Equal("#AABBCC", t.CapsuleColor);
        Assert.Equal(ThemeSettings.MinOpacity, t.CapsuleOpacity);
        Assert.Equal(1, t.Roundness);
        Assert.Equal("", t.FontFamily);
        Assert.Equal(AnimationSpeed.Normal, t.AnimationSpeed);
    }

    [Fact]
    public void Presets_AreValid_AndDistinct()
    {
        Assert.Equal(ThemeSettings.Presets.Count, ThemeSettings.Presets.Select(p => p.Name).Distinct().Count());
        foreach (var p in ThemeSettings.Presets)
        {
            var copy = p.Clone();
            copy.Normalize();
            Assert.True(copy.LooksLike(p), p.Name);           // 預設主題本身就是合理的值
            Assert.True(ThemeSettings.TryParseColor(p.CapsuleColor, out var c));
            Assert.True(c.R < 0x50 && c.G < 0x50 && c.B < 0x50, $"{p.Name} 底色要夠暗,白字才看得清楚");
        }
        // 兩兩外觀不同,才能正確標示「目前的主題」
        for (int i = 0; i < ThemeSettings.Presets.Count; i++)
            for (int j = i + 1; j < ThemeSettings.Presets.Count; j++)
                Assert.False(ThemeSettings.Presets[i].LooksLike(ThemeSettings.Presets[j]));
    }

    [Fact]
    public void CapsuleSwatches_AreDark()
    {
        foreach (var hex in ThemeSettings.CapsuleSwatches)
        {
            Assert.True(ThemeSettings.TryParseColor(hex, out var c));
            Assert.True(c.R + c.G + c.B < 3 * 0x40, hex);
        }
    }

    [Fact]
    public void LooksLike_IgnoresName_AndAccentColorWhenNotFixed()
    {
        var a = new ThemeSettings { Name = "A", AccentSource = AccentSource.Album, AccentColor = "#FF0000" };
        var b = new ThemeSettings { Name = "B", AccentSource = AccentSource.Album, AccentColor = "#00FF00" };
        Assert.True(a.LooksLike(b));
        a.AccentSource = b.AccentSource = AccentSource.Fixed;
        Assert.False(a.LooksLike(b));
    }

    [Fact]
    public void Radius_FollowsRoundness()
    {
        var t = new ThemeSettings { Roundness = 1 };
        Assert.Equal(18, t.CollapsedRadius(36));
        Assert.Equal(24, t.ExpandedRadius);
        t.Roundness = 0;
        Assert.Equal(6, t.CollapsedRadius(36));
        Assert.Equal(8, t.ExpandedRadius);
    }

    [Theory]
    [InlineData(0, 0.6)]   // Fast
    [InlineData(1, 1.0)]   // Normal
    [InlineData(2, 1.6)]   // Slow
    public void SpeedFactor(int speed, double factor) =>
        Assert.Equal(factor, new ThemeSettings { AnimationSpeed = (AnimationSpeed)speed }.SpeedFactor);

    [Fact]
    public void TimeOfDay_ChangesGradually()
    {
        var day = new DateTime(2026, 11, 23);
        var morning = ThemeSettings.TimeOfDayAccent(day.AddHours(6));
        var noon = ThemeSettings.TimeOfDayAccent(day.AddHours(12));
        var evening = ThemeSettings.TimeOfDayAccent(day.AddHours(19));
        var night = ThemeSettings.TimeOfDayAccent(day.AddHours(0));
        Assert.Equal(Color.FromRgb(0xFF, 0x9F, 0x0A), morning);
        Assert.Equal(Color.FromRgb(0xBF, 0x5A, 0xF2), evening);
        Assert.Equal(Color.FromRgb(0x5E, 0x5C, 0xE6), night);
        Assert.NotEqual(morning, noon);
        Assert.Equal(night, ThemeSettings.TimeOfDayAccent(day.AddHours(23).AddMinutes(59).AddSeconds(59)));

        // 15 分鐘內變化很小(不會突然跳色)
        var a = ThemeSettings.TimeOfDayAccent(day.AddHours(7));
        var b = ThemeSettings.TimeOfDayAccent(day.AddHours(7.25));
        Assert.True(Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) < 40);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var t = new ThemeSettings
        {
            Name = "深藍金",
            AccentSource = AccentSource.TimeOfDay,
            CapsuleColor = "#0B1B36",
            CapsuleOpacity = 0.8,
            Roundness = 0.4,
            ShowBorder = false,
            FontFamily = "Microsoft JhengHei UI",
            AnimationSpeed = AnimationSpeed.Slow,
        };
        var json = t.ToJson();
        Assert.Contains("\"accentSource\": \"timeOfDay\"", json);
        Assert.Contains("深藍金", json);
        var back = ThemeSettings.FromJson(json);
        Assert.NotNull(back);
        Assert.True(back!.LooksLike(t));
        Assert.Equal("深藍金", back.Name);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"accentSource\": \"rainbow\"}")]
    public void FromJson_RejectsGarbage(string json) => Assert.Null(ThemeSettings.FromJson(json));

    [Fact]
    public void FromJson_ClampsOutOfRange()
    {
        var t = ThemeSettings.FromJson("""{ "name": "x", "capsuleOpacity": 0, "roundness": -3, "capsuleColor": "#123" }""");
        Assert.NotNull(t);
        Assert.Equal(ThemeSettings.MinOpacity, t!.CapsuleOpacity);
        Assert.Equal(0, t.Roundness);
        Assert.Equal("#112233", t.CapsuleColor);
    }

    [Fact]
    public void Settings_NormalizeTheme()
    {
        var s = new IslandSettings { Theme = null!, CustomThemes = [new ThemeSettings { Roundness = 5 }, null!] };
        s.Normalize();
        Assert.NotNull(s.Theme);
        var only = Assert.Single(s.CustomThemes);
        Assert.Equal(1, only.Roundness);
    }
}
