using static Island.App.Interop.NativeMethods;

namespace Island.App.Interop;

/// <summary>
/// 維持視窗在最上層。不輪詢:只在「前景視窗切換」事件發生時重新設定 HWND_TOPMOST
/// (其他 Topmost 視窗被帶到前景時會蓋過我們,這時把自己放回最上面)。
/// 事件由系統排入 UI 執行緒的訊息佇列,閒置時零成本。
/// </summary>
internal sealed class TopmostKeeper : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly WinEventProc _callback; // 必須保存參考,避免被 GC 回收
    private IntPtr _hook;

    /// <summary>前景視窗切換時觸發(在 UI 執行緒上)。</summary>
    public event Action? ForegroundChanged;

    public TopmostKeeper(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _callback = OnForegroundChanged;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback,
            0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    public void Reassert() =>
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        Reassert();
        ForegroundChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
