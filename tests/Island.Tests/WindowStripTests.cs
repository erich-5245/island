using Island.Modules.Links;

namespace Island.Tests;

/// <summary>捷徑小圓延伸列:分組、排除小圓本身的程式、放不下時的「+N」、連點輪流切換。</summary>
public sealed class WindowStripTests
{
    private static AppWindow W(long h, string exe, string title = "t") => new(new IntPtr(h), title, exe, null);

    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string Code = @"C:\Users\u\AppData\Local\Programs\Microsoft VS Code\Code.exe";
    private const string Claude = @"C:\Users\u\AppData\Local\AnthropicClaude\claude.exe";
    private const string Frame = @"C:\Windows\System32\ApplicationFrameHost.exe";

    [Fact]
    public void GroupsByExe_InZOrder_SkipsPinned_MarksActive()
    {
        var windows = new[] { W(1, Code), W(2, Chrome), W(3, Claude), W(4, Chrome), W(5, Code.ToUpperInvariant()) };
        var (shown, overflow) = WindowGroups.Build(windows, pinnedExe: Claude, foreground: new IntPtr(4));
        Assert.Empty(overflow);
        Assert.Equal(2, shown.Count);
        Assert.Equal([1L, 5L], shown[0].Windows.Select(w => w.Handle.ToInt64()));   // 大小寫不同也算同一個程式
        Assert.False(shown[0].IsActive);
        Assert.Equal([2L, 4L], shown[1].Windows.Select(w => w.Handle.ToInt64()));
        Assert.True(shown[1].IsActive);
    }

    [Fact]
    public void StoreApps_AreSeparate()
    {
        var (shown, _) = WindowGroups.Build([W(1, Frame, "設定"), W(2, Frame, "小算盤")], null, IntPtr.Zero);
        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public void Overflow_KeepsMostRecent_AndLeavesRoomForMore()
    {
        var windows = Enumerable.Range(1, 11).Select(i => W(i, $@"C:\app{i}.exe")).ToList();
        var (shown, overflow) = WindowGroups.Build(windows, null, IntPtr.Zero, max: 8);
        Assert.Equal(7, shown.Count);
        Assert.Equal(4, overflow.Count);
        Assert.Equal(1, shown[0].First.Handle.ToInt64());

        var (exact, none) = WindowGroups.Build(windows.Take(8).ToList(), null, IntPtr.Zero, max: 8);
        Assert.Equal(8, exact.Count);
        Assert.Empty(none);
    }

    [Fact]
    public void Next_CyclesThroughWindowsOfActiveApp()
    {
        var (shown, _) = WindowGroups.Build([W(1, Chrome), W(2, Chrome), W(3, Chrome), W(9, Code)], null, new IntPtr(1));
        var chrome = shown[0];
        Assert.Equal(3, WindowGroups.Next(chrome, new IntPtr(1)).Handle.ToInt64());   // 已在前面 → 最久沒用的
        Assert.Equal(9, WindowGroups.Next(shown[1], new IntPtr(1)).Handle.ToInt64()); // 不在前面 → 最近用的

        var (single, _) = WindowGroups.Build([W(1, Chrome)], null, new IntPtr(1));
        Assert.Equal(1, WindowGroups.Next(single[0], new IntPtr(1)).Handle.ToInt64());
    }

    [Fact]
    public void DominantColor_PrefersSaturatedOpaquePixels()
    {
        // BGRA:兩個白色、一個透明紅、一個不透明藍
        byte[] px = [255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 255, 0, 255, 0, 0, 255];
        var c = WindowStripView.Dominant(px);
        Assert.True(c.B > 200 && c.R < 100, $"{c}");
        Assert.Equal(System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60), WindowStripView.Dominant([0, 0, 0, 0]));
    }
}
