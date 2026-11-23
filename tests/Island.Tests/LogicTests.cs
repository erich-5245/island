using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Island.App.Infrastructure;
using Island.App.Interop;
using Island.Modules.Media;
using Island.Modules.Timer;
using Island.Modules.Weather;

namespace Island.Tests;

public class PomodoroTests
{
    /// <summary>完整走 8 輪:專注 → 短休 ×3,第 4 輪後長休,之後回到專注。</summary>
    [Fact]
    public void FullCycle()
    {
        var phase = PomodoroPhase.Focus;
        int rounds = 0;
        var seen = new List<PomodoroPhase>();
        for (int step = 0; step < 16; step++)
        {
            if (phase == PomodoroPhase.Focus) rounds++;    // 和 TimerModule.Finish 一樣:專注結束才算一輪
            phase = TimerModule.NextPhase(phase, rounds);
            seen.Add(phase);
        }
        var expected = new[]
        {
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.LongBreak, PomodoroPhase.Focus,
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.ShortBreak, PomodoroPhase.Focus,
            PomodoroPhase.LongBreak, PomodoroPhase.Focus,
        };
        Assert.Equal(expected, seen);
        Assert.Equal(8, rounds);
    }

    [Fact]
    public void BreakAlwaysReturnsToFocus()
    {
        Assert.Equal(PomodoroPhase.Focus, TimerModule.NextPhase(PomodoroPhase.ShortBreak, 3));
        Assert.Equal(PomodoroPhase.Focus, TimerModule.NextPhase(PomodoroPhase.LongBreak, 4));
    }

    [Fact]
    public void Lengths()
    {
        Assert.Equal(TimeSpan.FromMinutes(25), TimerModule.LengthOf(PomodoroPhase.Focus));
        Assert.Equal(TimeSpan.FromMinutes(5), TimerModule.LengthOf(PomodoroPhase.ShortBreak));
        Assert.Equal(TimeSpan.FromMinutes(15), TimerModule.LengthOf(PomodoroPhase.LongBreak));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(0.2, "0:01")]       // 進位:顯示 0:00 才真的到
    [InlineData(59.5, "1:00")]
    [InlineData(61, "1:01")]
    [InlineData(25 * 60, "25:00")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3725, "1:02:05")]
    [InlineData(-5, "0:00")]
    public void Format(double seconds, string expected) =>
        Assert.Equal(expected, TimerModule.Format(TimeSpan.FromSeconds(seconds)));
}

public class RainAlertTests
{
    private static WeatherSnapshot Snap(double now = 0, int code = 1, params double[] next15) =>
        new() { PrecipitationNow = now, Code = code, Next15Min = next15 };

    [Fact]
    public void AlertsOnce_ThenResetsWhenForecastClears()
    {
        bool alerted = false;
        Assert.False(RainAlert.ShouldAlert(Snap(0, 1, 0, 0, 0, 0), ref alerted));        // 晴
        Assert.True(RainAlert.ShouldAlert(Snap(0, 1, 0, 0, 0.4, 0), ref alerted));       // 30 分內有雨 → 提醒
        Assert.False(RainAlert.ShouldAlert(Snap(0, 1, 0, 0.5, 0.4, 0), ref alerted));    // 同一場雨不再提醒
        Assert.False(RainAlert.ShouldAlert(Snap(0, 1, 0, 0, 0, 0), ref alerted));        // 轉晴 → 重設
        Assert.False(alerted);
        Assert.True(RainAlert.ShouldAlert(Snap(0, 1, 0, 0.2, 0, 0), ref alerted));       // 下一場雨可以再提醒
    }

    [Fact]
    public void NoAlertWhileAlreadyRaining()
    {
        bool alerted = false;
        Assert.False(RainAlert.ShouldAlert(Snap(1.2, 61, 1, 1, 1, 1), ref alerted));     // 正在下(降雨量)
        Assert.True(alerted);
        alerted = false;
        Assert.False(RainAlert.ShouldAlert(Snap(0, 63, 0, 1, 1, 0), ref alerted));       // 正在下(天氣代碼)
    }

    [Fact]
    public void IgnoresCurrentSlotAndBeyond30Minutes()
    {
        bool alerted = false;
        Assert.False(RainAlert.ShouldAlert(Snap(0, 1, 0.9, 0, 0, 0.9), ref alerted));    // 只看第 2、3 格
        Assert.False(RainAlert.ShouldAlert(Snap(0, 1, 0, 0.05, 0.09, 0), ref alerted));  // 低於門檻
    }
}

public class WeatherParseTests
{
    private const string Json = """
    {
      "current": { "temperature_2m": 21.4, "apparent_temperature": 20.1, "weather_code": 3, "is_day": 1, "precipitation": 0.0 },
      "daily": { "temperature_2m_max": [24.0], "temperature_2m_min": [17.5], "precipitation_probability_max": [60] },
      "hourly": {
        "time": ["2026-11-23T09:00","2026-11-23T10:00","2026-11-23T11:00","2026-11-23T12:00","2026-11-23T13:00","2026-11-23T14:00","2026-11-23T15:00","2026-11-23T16:00"],
        "temperature_2m": [18,19,20,21,22,23,22,21],
        "weather_code": [1,2,3,61,61,3,2,1],
        "precipitation_probability": [0,10,20,70,80,30,10,null]
      },
      "minutely_15": {
        "time": ["2026-11-23T09:45","2026-11-23T10:00","2026-11-23T10:15","2026-11-23T10:30","2026-11-23T10:45","2026-11-23T11:00"],
        "precipitation": [0.0, 0.0, 0.3, 0.6, 0.0, 0.0]
      }
    }
    """;

    [Fact]
    public void ParsesCurrentAndDaily()
    {
        var s = WeatherSnapshot.Parse(Json, new DateTime(2026, 11, 23, 10, 5, 0));
        Assert.Equal(21.4, s.Temperature);
        Assert.Equal(20.1, s.FeelsLike);
        Assert.Equal(3, s.Code);
        Assert.True(s.IsDay);
        Assert.Equal(24.0, s.TodayMax);
        Assert.Equal(17.5, s.TodayMin);
        Assert.Equal(60, s.TodayRainChance);
    }

    [Fact]
    public void HoursStartAfterNow_AtMostSix()
    {
        var s = WeatherSnapshot.Parse(Json, new DateTime(2026, 11, 23, 10, 5, 0));
        Assert.Equal(6, s.Hours.Count);
        Assert.Equal(11, s.Hours[0].Time.Hour);
        Assert.Equal(70, s.Hours[1].RainChance);
        Assert.Equal(0, s.Hours[5].RainChance);   // null → 0
    }

    [Fact]
    public void Next15Min_StartsAtCurrentSlot()
    {
        // 10:05 → 目前這格是 10:00,接著 10:15、10:30、10:45
        var s = WeatherSnapshot.Parse(Json, new DateTime(2026, 11, 23, 10, 5, 0));
        Assert.Equal(new[] { 0.0, 0.3, 0.6, 0.0 }, s.Next15Min);
        bool alerted = false;
        Assert.True(RainAlert.ShouldAlert(s, ref alerted));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(45, false)]
    [InlineData(51, true)]
    [InlineData(95, true)]
    public void IsWet(int code, bool wet) => Assert.Equal(wet, WeatherCodes.IsWet(code));
}

public class AlbumColorTests
{
    private static BitmapSource Solid(params Color[] stripes)
    {
        const int size = 48;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var c = stripes[x * stripes.Length / size];
            int i = (y * size + x) * 4;
            pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R; pixels[i + 3] = 255;
        }
        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bmp.Freeze();
        return bmp;
    }

    [Fact]
    public void RedCover_GivesRed()
    {
        var c = AlbumColor.Extract(Solid(Color.FromRgb(200, 30, 40)));
        Assert.NotNull(c);
        Assert.True(c!.Value.R > 150 && c.Value.G < 80 && c.Value.B < 80, c.ToString());
    }

    [Fact]
    public void GrayCover_GivesNull()
    {
        Assert.Null(AlbumColor.Extract(Solid(Color.FromRgb(128, 128, 128), Color.FromRgb(20, 20, 20), Color.FromRgb(240, 240, 240))));
    }

    [Fact]
    public void DominantHueWins()
    {
        // 3/4 藍、1/4 黃 → 藍
        var c = AlbumColor.Extract(Solid(Color.FromRgb(30, 60, 200), Color.FromRgb(30, 60, 200), Color.FromRgb(30, 60, 200), Color.FromRgb(230, 200, 30)));
        Assert.NotNull(c);
        Assert.True(c!.Value.B > c.Value.R && c.Value.B > c.Value.G, c.ToString());
    }

    [Fact]
    public void NullImage() => Assert.Null(AlbumColor.Extract(null));
}

public class HotkeyTests
{
    private const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8;

    [Theory]
    [InlineData("Ctrl+Alt+I", Ctrl | Alt, 0x49u)]
    [InlineData("ctrl + shift + F12", Ctrl | Shift, 0x7Bu)]
    [InlineData("Win+Alt+Space", Win | Alt, 0x20u)]
    [InlineData("Control+D1", Ctrl, 0x31u)]
    public void Parses(string text, uint mods, uint vk)
    {
        Assert.True(GlobalHotkey.TryParse(text, out var m, out var v));
        Assert.Equal(mods, m);
        Assert.Equal(vk, v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("I")]                // 沒有修飾鍵
    [InlineData("Ctrl+Alt")]         // 沒有主鍵
    [InlineData("Ctrl+Banana")]      // 不認得的鍵
    public void Rejects(string? text) => Assert.False(GlobalHotkey.TryParse(text, out _, out _));

    [Fact]
    public void Format_RoundTrips()
    {
        var text = GlobalHotkey.Format(ModifierKeys.Control | ModifierKeys.Alt, Key.I);
        Assert.Equal("Ctrl+Alt+I", text);
        Assert.True(GlobalHotkey.TryParse(text, out _, out _));
    }

    [Fact]
    public void Format_ModifierOnly_IsNull()
    {
        Assert.Null(GlobalHotkey.Format(ModifierKeys.Control, Key.LeftCtrl));
        Assert.Null(GlobalHotkey.Format(ModifierKeys.None, Key.A));
    }
}

public class SettingsTests
{
    [Fact]
    public void Normalize_ClampsSizes()
    {
        var s = new IslandSettings
        {
            CollapsedWidth = 10, CollapsedHeight = 500,
            PeekWidth = 5, ExpandedWidth = 99999, ExpandedHeight = 1,
        };
        s.Normalize();
        Assert.Equal(80, s.CollapsedWidth);
        Assert.Equal(80, s.CollapsedHeight);
        Assert.Equal(80, s.PeekWidth);          // 預覽不可比收合窄
        Assert.Equal(1200, s.ExpandedWidth);
        Assert.Equal(80, s.ExpandedHeight);     // 展開不可比收合矮
    }

    [Fact]
    public void Defaults_AreSane()
    {
        var s = new IslandSettings();
        s.Normalize();
        Assert.Equal("Ctrl+Alt+I", s.Hotkey);
        Assert.True(GlobalHotkey.TryParse(s.Hotkey, out _, out _));
        Assert.Contains("shelf", s.Modules);
        Assert.Equal(s.LiveOrder.Distinct().Count(), s.LiveOrder.Count);
    }
}

public class AudioMeterTests
{
    [Fact]
    public void OpensDefaultDevice_AndReadsPeakInRange()
    {
        using var meter = new AudioMeter();
        bool opened = meter.Open();          // 沒有音效裝置的電腦會是 false,也不能丟例外
        float peak = meter.Peak();
        Assert.InRange(peak, 0f, 1f);
        meter.Close();
        Assert.Equal(0f, meter.Peak());       // 關閉後讀到 0
        _ = opened;
    }
}
