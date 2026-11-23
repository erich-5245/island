using Island.App.Infrastructure;
using Island.App.Modules;
using Island.Modules.SysMon;

namespace Island.Tests;

public class SysMonTests
{
    [Fact]
    public void Cpu_FromSystemTimes()
    {
        // kernel 包含 idle:Δkernel 600(其中 idle 500)、Δuser 400 → 忙碌 = 1000 − 500 = 50%
        Assert.Equal(50, SysMath.CpuPercent(0, 0, 0, 500, 600, 400), 3);
        Assert.Equal(0, SysMath.CpuPercent(10, 10, 10, 10, 10, 10));          // 沒有經過時間
        Assert.Equal(100, SysMath.CpuPercent(0, 0, 0, 0, 300, 700), 3);
        Assert.Equal(0, SysMath.CpuPercent(100, 100, 100, 50, 200, 200));     // 計數器倒退:不算
    }

    [Fact]
    public void Rate_HandlesResetAndZeroTime()
    {
        Assert.Equal(1000, SysMath.Rate(5000, 7000, 2));
        Assert.Equal(0, SysMath.Rate(7000, 5000, 1));
        Assert.Equal(0, SysMath.Rate(0, 100, 0));
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(900, "900 B/s")]
    [InlineData(2048, "2 KB/s")]
    [InlineData(1.5 * 1024 * 1024, "1.5 MB/s")]
    [InlineData(25 * 1024 * 1024, "25 MB/s")]
    public void FormatRate(double bytes, string expected) => Assert.Equal(expected, SysMath.FormatRate(bytes));

    [Fact]
    public void Sampler_SecondSampleIsValid()
    {
        var s = new SysSampler();
        Assert.False(s.Take().Valid);       // 第一筆只是基準
        Thread.Sleep(50);
        var second = s.Take();
        Assert.True(second.Valid);
        Assert.InRange(second.CpuPercent, 0, 100);
        Assert.InRange(second.MemoryPercent, 1, 100);
    }

    [Fact]
    public void OptInModules()
    {
        var settings = new IslandSettings();
        Assert.False(ModuleLoader.IsEnabled(settings, "sysmon", defaultOn: false));
        Assert.True(ModuleLoader.IsEnabled(settings, "clock", defaultOn: true));

        ModuleLoader.SetEnabled(settings, "sysmon", defaultOn: false, on: true);
        ModuleLoader.SetEnabled(settings, "clock", defaultOn: true, on: false);
        Assert.True(ModuleLoader.IsEnabled(settings, "SysMon", false));
        Assert.False(ModuleLoader.IsEnabled(settings, "clock", true));
        Assert.Equal(["sysmon"], settings.EnabledModules);
        Assert.Equal(["clock"], settings.DisabledModules);

        ModuleLoader.SetEnabled(settings, "sysmon", defaultOn: false, on: false);
        Assert.Empty(settings.EnabledModules);
        Assert.False(ModuleLoader.IsEnabled(settings, "sysmon", false));
    }

public class ModuleDefaultsTests
{
    /// <summary>透過介面讀(外殼就是這樣讀的),子類別的設定要生效。</summary>
    [Fact]
    public void SysMon_IsOffByDefault_ThroughInterface() =>
        Assert.False(((Island.Core.IIslandModule)new SysMonModule()).EnabledByDefault);

    [Fact]
    public void Todo_IsOnByDefault() =>
        Assert.True(((Island.Core.IIslandModule)new Island.Modules.Todo.TodoModule()).EnabledByDefault);
}
}
