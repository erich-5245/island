using System.Management;

namespace Island.Modules.Osd;

/// <summary>
/// 監看螢幕亮度(WMI 的 WmiMonitorBrightnessEvent)。
/// 只有筆電內建螢幕這類「由 Windows 控制亮度」的螢幕會送這個事件;
/// 外接螢幕的亮度是螢幕自己的按鈕在調,Windows 不知道,所以不會顯示。
/// 事件驅動,沒有輪詢;電腦不支援時 Start 回傳 false,什麼都不做。
/// </summary>
internal sealed class BrightnessWatcher : IDisposable
{
    private ManagementEventWatcher? _watcher;

    /// <summary>亮度 0~100(背景執行緒)。</summary>
    public event Action<int>? Changed;

    public bool Start()
    {
        try
        {
            var scope = new ManagementScope(@"root\WMI");
            _watcher = new ManagementEventWatcher(scope, new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _watcher.EventArrived += (_, e) =>
            {
                try
                {
                    var value = Convert.ToInt32(e.NewEvent["Brightness"]);
                    Changed?.Invoke(Math.Clamp(value, 0, 100));
                }
                catch { }
            };
            _watcher.Start();
            return true;
        }
        catch
        {
            _watcher?.Dispose();
            _watcher = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _watcher?.Stop(); } catch { }
        _watcher?.Dispose();
        _watcher = null;
    }
}
