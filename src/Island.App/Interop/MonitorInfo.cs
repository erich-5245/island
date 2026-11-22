using static Island.App.Interop.NativeMethods;

namespace Island.App.Interop;

/// <summary>一個實體螢幕。座標皆為實體像素(Per-Monitor V2 下系統回傳的就是實體像素)。</summary>
internal sealed record MonitorInfo(IntPtr Handle, string DeviceName, RECT Bounds, RECT WorkArea, bool IsPrimary, uint Dpi)
{
    public double Scale => Dpi / 96.0;

    public override string ToString() =>
        $"{DeviceName}{(IsPrimary ? " [主螢幕]" : "")} {Bounds} {Dpi}dpi({Scale:P0})";

    public static List<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(h, ref mi))
            {
                uint dpi = GetDpiForMonitor(h, MDT_EFFECTIVE_DPI, out var dx, out _) == 0 ? dx : 96;
                list.Add(new MonitorInfo(h, mi.szDevice, mi.rcMonitor, mi.rcWork,
                    (mi.dwFlags & MONITORINFOF_PRIMARY) != 0, dpi));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>依設定選螢幕:"primary" 或裝置名稱;找不到時退回主螢幕,再退回第一個。</summary>
    public static MonitorInfo? Select(string display)
    {
        var all = GetAll();
        if (all.Count == 0) return null;

        if (!string.Equals(display, "primary", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(display))
        {
            var match = all.FirstOrDefault(m => string.Equals(m.DeviceName, display, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return all.FirstOrDefault(m => m.IsPrimary) ?? all[0];
    }
}
