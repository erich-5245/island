using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using static Island.App.Interop.NativeMethods;

namespace Island.App.Interop;

/// <summary>
/// 「點外面就關閉」。島是不取得焦點的視窗,收不到失焦事件,
/// 所以只在「固定展開」期間掛一個低階滑鼠掛勾(WH_MOUSE_LL),收合時立刻卸下,閒置時零成本。
/// 點在本程式的任何視窗上(膠囊本身、右鍵選單)不算外面。
/// </summary>
internal sealed class OutsideClickWatcher : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;

    private readonly Dispatcher _dispatcher;
    private readonly Action _clickedOutside;
    private readonly LowLevelMouseProc _proc;   // 保存參考,避免被 GC 回收
    private readonly uint _pid = (uint)Environment.ProcessId;
    private IntPtr _hook;

    public OutsideClickWatcher(Dispatcher dispatcher, Action clickedOutside)
    {
        _dispatcher = dispatcher;
        _clickedOutside = clickedOutside;
        _proc = HookProc;
    }

    public bool IsActive => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        using var module = Process.GetCurrentProcess().MainModule;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module?.ModuleName), 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // 掛勾必須很快返回:只判斷點擊位置屬於誰,真正的收合排到 UI 執行緒稍後做
        // 系統回呼:例外不能丟回原生程式碼(會讓整個程式結束)
        try
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
                {
                    var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    GetWindowThreadProcessId(WindowFromPoint(info.pt), out uint pid);
                    if (pid != _pid) _dispatcher.BeginInvoke(_clickedOutside);
                }
            }
        }
        catch { /* 忽略,照常交給下一個掛勾 */ }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
