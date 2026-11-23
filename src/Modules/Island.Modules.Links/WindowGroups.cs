namespace Island.Modules.Links;

/// <summary>小圓延伸列上的一格:同一個程式的視窗合成一格(Microsoft Store 程式每個視窗各一格)。</summary>
public sealed record WindowGroup(string Key, IReadOnlyList<AppWindow> Windows, bool IsActive)
{
    public AppWindow First => Windows[0];
}

/// <summary>
/// 小圓延伸列的純邏輯(不碰 UI、不碰 Win32,方便測試):
/// 分組、排除小圓本身的程式、最多幾格、點一下要切到哪個視窗。
/// </summary>
public static class WindowGroups
{
    public const int MaxShown = 8;

    /// <summary>Microsoft Store 程式都由這個 exe 代管,不能用 exe 分組。</summary>
    private const string FrameHost = "ApplicationFrameHost.exe";

    public static string KeyOf(AppWindow w) =>
        w.ExePath.EndsWith(FrameHost, StringComparison.OrdinalIgnoreCase)
            ? "hwnd:" + w.Handle.ToInt64()
            : w.ExePath.ToLowerInvariant();

    /// <summary>
    /// windows 依 Z 順序(最上面 = 最近用過)。每個程式一格,順序 = 那個程式最近一次用的時間。
    /// pinnedExe = 小圓本身代表的程式(已經在第一格了,不重複)。回傳顯示的格子和放不下的格子。
    /// </summary>
    public static (List<WindowGroup> Shown, List<WindowGroup> Overflow) Build(
        IReadOnlyList<AppWindow> windows, string? pinnedExe, IntPtr foreground, int max = MaxShown)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<AppWindow>>();
        foreach (var w in windows)
        {
            if (pinnedExe is not null && string.Equals(w.ExePath, pinnedExe, StringComparison.OrdinalIgnoreCase)) continue;
            var key = KeyOf(w);
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = [];
                order.Add(key);
            }
            list.Add(w);
        }
        var groups = order.Select(k => new WindowGroup(k, map[k], map[k].Any(w => w.Handle == foreground))).ToList();
        if (groups.Count <= max) return (groups, []);
        // 放不下:留最近用過的 max - 1 格,最後一格給「+N」
        return (groups.Take(max - 1).ToList(), groups.Skip(max - 1).ToList());
    }

    /// <summary>
    /// 點一下要切到哪個視窗:這個程式不在前面 → 它最近用過的視窗;
    /// 已經在前面 → 最久沒用的那個(連點就會輪過每一個視窗)。
    /// </summary>
    public static AppWindow Next(WindowGroup group, IntPtr foreground) =>
        group.IsActive && group.Windows.Count > 1 && group.Windows.Any(w => w.Handle == foreground)
            ? group.Windows[^1]
            : group.First;
}
